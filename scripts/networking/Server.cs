using Godot;
using Google.Protobuf;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Reflection;
using static NetworkManager;

/// <summary>
/// Represents a server that handles network connections and communication.
/// </summary>
public partial class Server : Node
{

    /////////////////////////////////////// Static declarations /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public struct ConnectionData
    {
        public HSteamNetConnection connection;
        public ulong clientID;
        public ulong mostRecentTick;
        public ulong sequence;
        public bool isServer;
        public FramePacket nextFramePacket;

        public ConnectionData()
        {
            isServer = false;
            connection = HSteamNetConnection.Invalid;
            clientID = 0;
            mostRecentTick = 0;
            sequence = 0;
            nextFramePacket = new FramePacket() { Tick = Global.getTick(), Sender = Global.clientID, Sequence = sequence++ };
        }

        public ConnectionData(HSteamNetConnection connection, ulong clientID, bool isServer)
        {
            this.isServer = isServer;
            this.connection = connection;
            this.clientID = clientID;
            mostRecentTick = 0;
            sequence = 0;
            nextFramePacket = new FramePacket() { Tick = Global.getTick(), Sender = Global.clientID, Sequence = sequence++ };
        }

        public void SetMostRecentTick(ulong tick)
        {
            mostRecentTick = tick;
        }

        public void SendFramePacketAndReset()
        {
            nextFramePacket.Tick = Global.getTick();
            SendSteamMessage(connection, nextFramePacket,0,NetworkManager.k_nSteamNetworkingSend_UnreliableNoNagle);
            nextFramePacket = new FramePacket() { Tick = Global.getTick(), Sender = Global.clientID, Sequence = sequence++ };
        }
    }

    public enum GamePrivacy
    {
        None,
        Public,
        FriendsOnly,
        InviteOnly,
        Invisible,
        Offline,
    }
    /////////////////////////////////////// Config vars /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public bool bTrustClients = true;
    public GamePrivacy gamePrivacy = GamePrivacy.Public;
    public bool acceptAllConnections = true;
    public ulong FreshnessLimit = 10;

    /////////////////////////////////////// Internal vars /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// The list of client connections.
    /// </summary>
    public Dictionary<HSteamNetConnection, ConnectionData> clients = new();
    public Dictionary<ulong,HSteamNetConnection> clientLookup = new();

    /// <summary>
    /// The listen socket for incoming connections.
    /// </summary>
    public HSteamListenSocket listenSocket = new();


    /////////////////////////////////////// Events and Callbacks /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;



    /////////////////////////////////////// Constructors /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class with a local client connection.
    /// </summary>
    /// <param name="localClient">The local client connection.</param>
    public Server(HSteamNetConnection localClient)
    {
        clients.Add(localClient, new ConnectionData { connection = localClient, clientID = Global.clientID, mostRecentTick = 0, sequence = 0, isServer = true});
        clientLookup.Add(Global.clientID, localClient);
        SteamNetworkingSockets.ConfigureConnectionLanes(localClient, 2, null, null);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    public Server() { }



    /////////////////////////////////////// Godot Function Overrides /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Called when the node is ready.
    /// </summary>
    public override void _Ready()
    {
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
    }

    /// <summary>
    /// Called every frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame.</param>
    public override void _Process(double delta)
    {
        IntPtr[] messages = new IntPtr[100];
        List<FramePacket> receivedPackets = new List<FramePacket>();

        foreach (HSteamNetConnection conn in clients.Keys)
        {
            int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, 100);

            for (int j = 0; j < numMessages; j++) //interates over all messages received
            {
                if (messages[j] == IntPtr.Zero) { continue; } //Sanity check
                SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(messages[j]); //Converts the message to a C# object
                if (steamMsg.m_idxLane == 0) //Lane 0 is for frame packets. This lane is FAST but UNRELIABLE.
                {
                    FramePacket framePacket = FramePacket.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize)); //Converts the message to a C# object (it was a protobuf message inside the steam message)
                    if (clients[conn].mostRecentTick < framePacket.Tick)
                    {
                        clients[conn].SetMostRecentTick(framePacket.Tick);
                        Global.worldSim.ApplyFramePacket(framePacket);
                    }
                }
                else if (steamMsg.m_idxLane == 1) //Lane 1 is for reliable packets, like chat, handshake, admin commands, etc. This lane is SLOW but RELIABLE.
                {
                    Global.debugLog("Server - Got reliable msg");
                    ReliablePacket reliablePacket = ReliablePacket.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
                    HandleReliablePacket(reliablePacket); //Handles the reliable packet (chat, commands, etc.)
                }
                // Free the memory for the message
                SteamNetworkingMessage_t.Release(messages[j]);
            }
        }
    }

    /// <summary>
    /// Called every physics frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last physics frame.</param>
    public override void _PhysicsProcess(double delta)
    {
        if (Global.worldSim!=null)
        {
            // Send the outgoing frame packet to all clients
            foreach (HSteamNetConnection c in clients.Keys)
            {
                //Global.debugLog("server - sending frame packet to client: " + clients[c].clientID + " tick is " + Global.getTick());
                clients[c].SendFramePacketAndReset();
            }
        }
    }


    /////////////////////////////////////// Core Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



    /// <summary>
    /// Handles the SteamNetConnectionStatusChangedCallback event.
    /// </summary>
    /// <param name="event">The SteamNetConnectionStatusChangedCallback event.</param>
    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t @event)
    {

        HSteamNetConnection conn = @event.m_hConn;
        switch (@event.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                if (acceptAllConnections)
                {
                    SteamNetworkingSockets.AcceptConnection(conn);
                    Global.NetworkManager.networkDebugLog("Accepting external connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64());
                }
                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                SendPlayerJoinedMessage(getConnectionRemoteID(conn));
                CatchUpNewJoiner(conn);
                clients.Add(conn, new ConnectionData(conn,getConnectionRemoteID(conn),false));
                clientLookup.Add(getConnectionRemoteID(conn), conn);
                SteamNetworkingSockets.ConfigureConnectionLanes(conn, 2, null, null);

                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " complete!");
                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                clients.Remove(conn);
                clientLookup.Remove(getConnectionRemoteID(conn));
                SteamNetworkingSockets.CloseConnection(conn, 0, "Connection closed by peer.", true);
                SendPlayerLeftMessage(getConnectionRemoteID(conn));
                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " closed by peer.");
                break;
            default:
                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " in unknown state: " + @event.m_info.m_eState);
                break;
        }
    }

    private void HandleReliablePacket(ReliablePacket reliablePacket)
    {
        ReliablePacket outgoingReliablePacket = new ReliablePacket();
        outgoingReliablePacket.Tick = Global.getTick();
        outgoingReliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        foreach (Chat chatMessage in reliablePacket.ChatMessages)
        {
            outgoingReliablePacket.ChatMessages.Add(chatMessage);
        }
        foreach (Command command in reliablePacket.Commands)
        {

            Global.NetworkManager.networkDebugLog("Server - Received a command: " + command.Command_);
            if (!HasCommandPermission(command))
            {
                Global.NetworkManager.networkDebugLog("Server - Sender: " + reliablePacket.Sender + " does not have permissions for command: " + command.Command_);
                continue;
            }
            switch (command.Command_)
            {
                case "startgame":
                    Global.NetworkManager.networkDebugLog("Server - Received a command to launch the game");
                    outgoingReliablePacket.Commands.Add(command);
                    break;
                default:
                    Global.NetworkManager.networkDebugLog("Server - Received a command of an unexpected type: " + command.Command_);
                    break;
            }
        }
        foreach( ulong playerID in reliablePacket.PlayerJoined)
        {
            Global.debugLog("this shouldnt happen!");
            outgoingReliablePacket.PlayerJoined.Add(playerID);
        }
        foreach (ulong playerID in reliablePacket.PlayerLeft)
        {
            Global.debugLog("this shouldnt happen!");
            outgoingReliablePacket.PlayerLeft.Add(playerID);
        }
        foreach (ulong playerID in reliablePacket.PlayerList)
        {
            Global.debugLog("this shouldnt happen!");
            outgoingReliablePacket.PlayerList.Add(playerID);
        }
        foreach (HSteamNetConnection c in clients.Keys)
        {
            SendSteamMessage(c, outgoingReliablePacket,1,NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
        }    
    }

    /////////////////////////////////////// Utility Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    public void SendPlayerJoinedMessage(ulong playerID)
    {
        ReliablePacket outgoingReliablePacket = new ReliablePacket();
        outgoingReliablePacket.Tick = Global.getTick();
        outgoingReliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        outgoingReliablePacket.PlayerJoined.Add(playerID);
        foreach (HSteamNetConnection c in clients.Keys)
        {

            SendSteamMessage(c, outgoingReliablePacket, 1, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
        }
    }

    public void SendPlayerLeftMessage(ulong playerID)
    {
        ReliablePacket outgoingReliablePacket = new ReliablePacket();
        outgoingReliablePacket.Tick = Global.getTick();
        outgoingReliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        outgoingReliablePacket.PlayerLeft.Add(playerID);
        foreach (HSteamNetConnection c in clients.Keys)
        {
            SendSteamMessage(c, outgoingReliablePacket, 1, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
        }
    }

    public void CatchUpNewJoiner(HSteamNetConnection newJoiner)
    {
        ReliablePacket outgoingReliablePacket = new ReliablePacket();
        outgoingReliablePacket.Tick = Global.getTick();
        outgoingReliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        outgoingReliablePacket.PlayerJoined.Add(Global.clientID);
        foreach (ConnectionData c in clients.Values)
        {
            outgoingReliablePacket.PlayerJoined.Add(c.clientID);
        }

        SendSteamMessage(newJoiner, outgoingReliablePacket, 1, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
    }


    /// <summary>
    /// Checks if the sender has permission to execute the command.
    /// </summary>
    /// <param name="sender">The sender's ID.</param>
    /// <param name="command">The command to be executed.</param>
    /// <returns><c>true</c> if the sender has permission; otherwise, <c>false</c>.</returns>
    private bool HasCommandPermission(Command command)
    {
        // Implement your logic here to check if the sender has permission to execute the command
        // You can use the 'sender' and 'command' parameters to perform the necessary checks
        // Return true if the sender has permission, otherwise return false
        // Example implementation:
        if (true)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool IsFresh(FramePacket framePacket)
    {
        if (AbsoluteDifference(framePacket.Tick, Global.worldSim.tick) > FreshnessLimit)
        {
            Global.debugLog("This packet is too old: " + framePacket.Tick + " vs " + Global.worldSim.tick + " with a freshness limit of " + FreshnessLimit);
            return false;
        }
        if (framePacket.Tick < clients[clientLookup[framePacket.Sender]].mostRecentTick)
        {
            Global.debugLog("This packet is older than the last received packet: " + framePacket.Tick + " vs " + clients[clientLookup[framePacket.Sender]].mostRecentTick);
            return false;
        }
        return true;
    }

}

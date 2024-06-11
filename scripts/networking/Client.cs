using Godot;
using Google.Protobuf;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using static NetworkManager;


/// <summary>
/// Object representing the client-side processing of networking data. Only one of these should exist per machine.
/// </summary>
public partial class Client : Node
{
    /////////////////////////////////////// Config vars /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// The maximum number of messages the Client will attempt to handle per frame. I have no clue what the concequences of this are, in either direction.
    /// </summary>
    private int maxIncomingMessagesPerFrame = 100;
    public int nMaxMessages = 100;
    private bool bRollback = false;
    private ulong FreshnessLimit = 5;

    /////////////////////////////////////// Tracking vars /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public float incomingBandwidthUsed = 0;
    public float outgoingBandwidthUsed = 0;
    public float totalBandwidthUsed = 0;   
    public float incomingBandwidth = 0;
    public float outgoingBandwidth = 0;
    public float totalBandwidth = 0;

    /////////////////////////////////////// Events and Callbacks /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    
    public delegate void ChatMessageEventHandler(string message, ulong sender);
    public static event ChatMessageEventHandler ChatMessageReceived;

    public delegate void PlayerJoinedEventHandler(ulong playerID);
    public static event PlayerJoinedEventHandler PlayerJoined;

    public delegate void PlayerLeftEventHandler(ulong playerID);
    public static event PlayerLeftEventHandler PlayerLeft;

    public delegate void PlayerListUpdateEventHandler(List<ulong> players);
    public static event PlayerListUpdateEventHandler PlayerListUpdate;

    public delegate void NetworkCommandEventHandler(string command, ulong sender);
    public static event NetworkCommandEventHandler NetworkCommandReceived;

    /// <summary>
    /// An event that fires when the underlying steam network detects a change in connection status.
    /// </summary>
    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;

    /////////////////////////////////////// Internal State vars /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public FramePacket outgoingFramePacket = new();
    public Dictionary<ulong,FramePacket> framePacketBuffer = new();

    /// <summary>
    /// A handle for the connection to the server. Any messages from the server end up on this, any message sent on this end up at the server.
    /// </summary>
    public HSteamNetConnection connectionToServer;
    public List<ulong> peers = new();
    public ulong mostRecentTick = 0;
    public ulong sequence = 0;
    public ulong serverID = 0;
    public bool isServer = false;

    /////////////////////////////////////// Constructors /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Construct a client with a pre-existing connection object.
    /// </summary>
    /// <param name="connectionToServer">A valid and pre-connected connection object. MUST ALREADY BE IN CONNECTED STATE. Does NOT generate callbacks. </param>
    public Client(HSteamNetConnection connectionToServer)
    {
        this.connectionToServer = connectionToServer;
        serverID = NetworkManager.getConnectionRemoteID(connectionToServer);
    }

    /// <summary>
    /// Empty constructor to keep Godot happy
    /// </summary>
    public Client() { }

    /////////////////////////////////////// Godot Override Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public override void _Ready()
    {
        //Hooks up the connection status change event to a function
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
        SteamNetworkingSockets.ConfigureConnectionLanes(connectionToServer, 2, null, null);
    }

    public override void _Process(double delta)
    {
        IntPtr[] messages = new IntPtr[maxIncomingMessagesPerFrame];
        HSteamNetConnection conn = connectionToServer;
        int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, maxIncomingMessagesPerFrame); //SteamAPI call to get messages
        for (int j = 0; j < numMessages; j++) //interates over all messages received
        {
            if (messages[j] == IntPtr.Zero) { continue; } //Sanity check
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(messages[j]); //Converts the message to a C# object
            if (steamMsg.m_idxLane == 0) //Lane 0 is for frame packets. This lane is FAST but UNRELIABLE.
            {
                FramePacket framePacket = FramePacket.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize)); //Converts the message to a C# object (it was a protobuf message inside the steam message)
                incomingBandwidthUsed+=framePacket.CalculateSize();
                incomingBandwidth = ((incomingBandwidthUsed / Global.getTick()) * 60) / 1000;
                Global.worldSim.ApplyFramePacket(framePacket);
                mostRecentTick = framePacket.Tick;
            }
            else if (steamMsg.m_idxLane == 1) //Lane 1 is for reliable packets, like chat, handshake, admin commands, etc. This lane is SLOW but RELIABLE.
            {
                Global.debugLog("Client - Got reliable msg");
                ReliablePacket reliablePacket = ReliablePacket.Parser.ParseFrom(IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
                HandleReliablePacket(reliablePacket); //Handles the reliable packet (chat, commands, etc.)
            }
            // Free the memory for the message
            SteamNetworkingMessage_t.Release(messages[j]);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Global.worldSim != null)
        {
            outgoingFramePacket.Inputs.Add(Global.InputManager.localInput.Clone());
            SendSteamMessage(connectionToServer, outgoingFramePacket, 0, NetworkManager.k_nSteamNetworkingSend_UnreliableNoNagle);
            outgoingBandwidthUsed+=outgoingFramePacket.CalculateSize();
            outgoingFramePacket = new FramePacket() { Tick = Global.getTick(), Sender = Global.clientID, Sequence = sequence++ };
        }
        outgoingBandwidth = ((outgoingBandwidthUsed / Global.getTick()) * 60)/1000;
        totalBandwidthUsed = outgoingBandwidthUsed + incomingBandwidthUsed;
        totalBandwidth = outgoingBandwidth + incomingBandwidth;
    }

    /////////////////////////////////////// Core Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t param)
    {
        Global.NetworkManager.networkDebugLog("Client - connection status change. New status: " + param.m_info.m_eState);
    }

    private void HandleReliablePacket(ReliablePacket reliablePacket)
    {
        if (reliablePacket.Commands.Count > 0)
        {
            foreach (Command command in reliablePacket.Commands)
            {
                NetworkCommandReceived?.Invoke(command.Command_, command.Sender);
            }
        }
        if (reliablePacket.ChatMessages.Count > 0)
        {
            foreach (Chat chatMessage in reliablePacket.ChatMessages)
            {
                ChatMessageReceived?.Invoke(chatMessage.Message, chatMessage.Sender);
            }
        }
        if (reliablePacket.PlayerJoined.Count > 0)
        {
            foreach (ulong playerID in reliablePacket.PlayerJoined)
            {
                peers.Add(playerID);
                PlayerJoined?.Invoke(playerID);
            }
        }
        if (reliablePacket.PlayerLeft.Count > 0)
        {
            foreach (ulong playerID in reliablePacket.PlayerLeft)
            {
                peers.Remove(playerID);
                PlayerLeft?.Invoke(playerID);
            }
        }
        if (reliablePacket.PlayerList.Count > 0)
        {
            peers = reliablePacket.PlayerList.ToList();
            PlayerListUpdate?.Invoke(reliablePacket.PlayerList.ToList());
        }
    }

    /////////////////////////////////////// Utility Functions /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private bool IsFresh(FramePacket framePacket)
    {
        if (AbsoluteDifference(framePacket.Tick, Global.worldSim.tick) > FreshnessLimit)
        {
            Global.debugLog("This packet is too old: " + framePacket.Tick + " vs " + Global.worldSim.tick + " with a freshness limit of " + FreshnessLimit);
            return false;
        }
        if (framePacket.Tick < mostRecentTick)
        {
            Global.debugLog("This packet is older than the last received packet: " + framePacket.Tick + " vs " + mostRecentTick);
            return false;
        }
        return true;
    }

    public static ulong AbsoluteDifference(ulong value1, ulong value2)
    {
        return value1 > value2 ? value1 - value2 : value2 - value1;
    }

    public void SendCommandToServer(string command, string param = "")
    {
        ReliablePacket reliablePacket = new ReliablePacket();
        reliablePacket.Sender = Global.clientID;
        reliablePacket.Tick = Global.getTick();
        reliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        reliablePacket.Commands.Add(new Command() { Command_ = command, Param = param, Sender = Global.clientID });
        SendSteamMessage(connectionToServer, reliablePacket, 1, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
    }

    public void SendChatToServer(string message)
    {

       ReliablePacket reliablePacket = new ReliablePacket();
        reliablePacket.Sender = Global.clientID;
        reliablePacket.Tick = Global.getTick();
        reliablePacket.Timestamp = Time.GetUnixTimeFromSystem();
        reliablePacket.ChatMessages.Add(new Chat() { Message = message, Sender = Global.clientID });
        SendSteamMessage(connectionToServer, reliablePacket, 1, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle);
    }
}


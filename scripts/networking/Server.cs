using Godot;
using Google.Protobuf;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using static NetworkManager;
/// <summary>
/// Represents a server that handles network connections and communication.
/// </summary>
public partial class Server : Node
{

    /// <summary>
    /// The list of client connections.
    /// </summary>
    public List<HSteamNetConnection> clients = new();

    /// <summary>
    /// The listen socket for incoming connections.
    /// </summary>
    public HSteamListenSocket listenSocket = new();


    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;

    /// <summary>
    /// The outgoing frame packet.
    /// </summary>
    public FramePacket outgoingFramePacket = new();


    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class with a local client connection.
    /// </summary>
    /// <param name="localClient">The local client connection.</param>
    public Server(HSteamNetConnection localClient)
    {
        clients.Add(localClient);
        SteamNetworkingSockets.ConfigureConnectionLanes(localClient, 3, null, null);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Server"/> class.
    /// </summary>
    public Server() { }

    /// <summary>
    /// Called when the node is ready.
    /// </summary>
    public override void _Ready()
    {
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
    }

    /// <summary>
    /// Handles the SteamNetConnectionStatusChangedCallback event.
    /// </summary>
    /// <param name="event">The SteamNetConnectionStatusChangedCallback event.</param>
    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t @event)
    {
        bool acceptAllConnections = true;
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
                clients.Add(conn);
                SteamNetworkingSockets.ConfigureConnectionLanes(conn, 3, null, null);
                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " complete!");
                outgoingFramePacket.PlayerJoined.Add(NetworkManager.getConnectionRemoteID(conn));
                break;
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                clients.Remove(conn);
                outgoingFramePacket.PlayerLeft.Add(NetworkManager.getConnectionRemoteID(conn));
                SteamNetworkingSockets.CloseConnection(conn, 0, "Connection closed by peer.", true);
                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " closed by peer.");
                break;
            default:
                Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " in unknown state: " + @event.m_info.m_eState);
                break;
        }
    }

    /// <summary>
    /// Called every frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame.</param>
    public override void _Process(double delta)
    {
        IntPtr[] messages = new IntPtr[100];
        List<FramePacket> receivedPackets = new List<FramePacket>();

        for (int i = 0; i < clients.Count; i++)
        {
            HSteamNetConnection conn = clients[i];
            int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, 100);

            for (int j = 0; j < numMessages; j++)
            {
                if (messages[j] == IntPtr.Zero) { continue; }

                SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(messages[j]);
                FramePacket framePacket = FramePacket.Parser.ParseFrom(NetworkManager.IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));

                receivedPackets.Add(framePacket);

                // Free the memory for the message
                SteamNetworkingMessage_t.Release(messages[j]);
            }
        }

        // Process the received packets outside the loop
        foreach (FramePacket framePacket in receivedPackets)
        {
            handleFramePacket(framePacket);
        }
    }

    /// <summary>
    /// Called every physics frame.
    /// </summary>
    /// <param name="delta">The time elapsed since the last physics frame.</param>
    public override void _PhysicsProcess(double delta)
    {
        outgoingFramePacket.Tick = Global.getTick();
        outgoingFramePacket.Sender = Global.instance.clientID;
        foreach (HSteamNetConnection c in clients)
        {
            SendSteamMessage(c, outgoingFramePacket);
        }
        outgoingFramePacket = new FramePacket();
    }

    /// <summary>
    /// Handles the frame packet received from the client.
    /// </summary>
    /// <param name="framePacket">The frame packet received from the client.</param>
    private void handleFramePacket(FramePacket framePacket)
    {
        foreach (Chat chatMessage in framePacket.ChatMessages)
        {
            outgoingFramePacket.ChatMessages.Add(chatMessage);
        }
        foreach (Command command in framePacket.Commands)
        {
            Global.NetworkManager.networkDebugLog("Server - Received a command: " + command.Command_);
            if (!HasCommandPermission(framePacket.Sender, command))
            {
                Global.NetworkManager.networkDebugLog("Server - Sender: " + framePacket.Sender + " does not have permissions for command: " + command.Command_);
                continue;
            }
            switch (command.Command_)
            {
                case "startgame":
                    Global.NetworkManager.networkDebugLog("Server - Received a command to launch the game");
                    outgoingFramePacket.Commands.Add(new Command { Command_ = "startgame" });
                    break;
                default:
                    Global.NetworkManager.networkDebugLog("Server - Received a command of an unexpected type: " + command.Command_);
                    break;
            }
        }
    }

    /// <summary>
    /// Checks if the sender has permission to execute the command.
    /// </summary>
    /// <param name="sender">The sender's ID.</param>
    /// <param name="command">The command to be executed.</param>
    /// <returns><c>true</c> if the sender has permission; otherwise, <c>false</c>.</returns>
    private bool HasCommandPermission(ulong sender, Command command)
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
}

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
    public float incomingBandwidthUsed = 0;
    public float outgoingBandwidthUsed = 0;
    public float totalBandwidthUsed = 0;   
    public float incomingBandwidth = 0;
    public float outgoingBandwidth = 0;
    public float totalBandwidth = 0;


    public delegate void ChatMessageEventHandler(string message, ulong sender);
    public static event ChatMessageEventHandler ChatMessageReceived;

    public delegate void PlayerJoinedEventHandler(ulong playerID);
    public static event PlayerJoinedEventHandler PlayerJoined;

    public delegate void PlayerLeftEventHandler(ulong playerID);
    public static event PlayerLeftEventHandler PlayerLeft;

    public FramePacket outgoingFramePacket = new();
    public Dictionary<ulong,FramePacket> framePacketBuffer = new();

    /// <summary>
    /// The maximum number of messages the Client will attempt to handle per frame. I have no clue what the concequences of this are, in either direction.
    /// </summary>
    public int nMaxMessages = 100;

    /// <summary>
    /// A handle for the connection to the server. Any messages from the server end up on this, any message sent on this end up at the server.
    /// </summary>
    public HSteamNetConnection connectionToServer;

    public List<ulong> peers = new();

    public ulong serverID = 0;

    /// <summary>
    /// An event that fires when the underlying steam network detects a change in connection status.
    /// </summary>
    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;

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

    public override void _Ready()
    {
        //Hooks up the connection status change event to a function
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);

        SteamNetworkingSockets.ConfigureConnectionLanes(connectionToServer, 3, null, null);
    }

    /// <summary>
    /// Called by the underlying Steam API in response to any underlying connection status change
    /// </summary>
    /// <param name="param">Info on the event</param>
    /// <exception cref="NotImplementedException"></exception>
    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t param)
    {
        Global.NetworkManager.networkDebugLog("Client - connection status change. New status: " + param.m_info.m_eState);
    }

    /// <summary>
    /// This method is called once per frame and is responsible for processing network messages.
    /// </summary>
    /// <param name="delta">The time elapsed since the last frame.</param>
    public override void _Process(double delta)
    {
        //Create and allocate memory for an array of pointers
        IntPtr[] messages = new IntPtr[nMaxMessages];

        //Collect up to nMaxMessages that are waiting in the queue on the connection to the server, and load them up into our preallocated message array
        int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(connectionToServer, messages, nMaxMessages);

        //For each message, send it off to further processing
        for (int i = 0; i < numMessages; i++)
        {
            if (messages[i] == IntPtr.Zero) { continue; } //Sanity check. 
            SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
            FramePacket framePacket = FramePacket.Parser.ParseFrom(NetworkManager.IntPtrToBytes(steamMsg.m_pData, steamMsg.m_cbSize));
            switch (steamMsg.m_idxLane)
            {
                case 0:
                    handleFramePacket(framePacket);
                    break;
                default:
                    Global.NetworkManager.networkDebugLog("Client - Received a message on an unexpected lane. Lane: " + steamMsg.m_idxLane);
                    break;
            }
            //Free the memory for the message
            SteamNetworkingMessage_t.Release(messages[i]);
        }


    }

    public override void _PhysicsProcess(double delta)
    {
        outgoingFramePacket.Tick = Global.getTick();
        outgoingFramePacket.Sender = Global.instance.clientID;
        SendSteamMessage(connectionToServer, outgoingFramePacket);
        outgoingFramePacket = new FramePacket();
    }

    private void handleFramePacket(FramePacket framePacket)
    {
        foreach (Chat chatMessage in framePacket.ChatMessages)
        {
            ChatMessageReceived.Invoke(chatMessage.Message, chatMessage.Sender.SteamID);
        }
        foreach (ulong playerID in framePacket.PlayerJoined)
        {
            PlayerJoined.Invoke(playerID);
        }
        foreach (ulong playerID in framePacket.PlayerLeft)
        {
            PlayerLeft.Invoke(playerID);
        }
        if (framePacket.PlayerList.Count > 0)
        {
            peers = framePacket.PlayerList.Clone().ToList();
        }
        foreach (Command command in framePacket.Commands)
        {
            Global.NetworkManager.networkDebugLog("Client - Received a command: " + command.Command_);
            switch (command.Command_)
            {
                case "startgame":
                    Global.NetworkManager.networkDebugLog("Client - Received a command to launch the game");
                    Global.instance.StartGame();
                    break;
                default:
                    Global.NetworkManager.networkDebugLog("Client - Received a command of an unexpected type: " + command.Command_);
                    break;
            }
        }
        if (framePacket.Tick!=0)
        {
            framePacketBuffer.Add(framePacket.Tick, framePacket);
        }
    }
}


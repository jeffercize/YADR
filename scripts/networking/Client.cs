using Godot;
using Steamworks;
using System;
using System.Runtime.InteropServices;
using static NetworkManager;

/// <summary>
/// Object representing the client-side processing of networking data. Only one of these should exist per machine.
/// </summary>
public partial class Client: Node
    {

    /// <summary>
    /// The maximum number of messages the Client will attempt to handle per frame. I have no clue what the concequences of this are, in either direction.
    /// </summary>
    public int nMaxMessages = 100;

    /// <summary>
    /// A handle for the connection to the server. Any messages from the server end up on this, any message sent on this end up at the server.
    /// </summary>
    public HSteamNetConnection connectionToServer;

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
    }

    /// <summary>
    /// Empty constructor to keep Godot happy
    /// </summary>
    public Client() { }

    public override void _Ready()
    {
        //Hooks up the connection status change event to a function
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
    }

    /// <summary>
    /// Called by the underlying Steam API in response to any underlying connection status change
    /// </summary>
    /// <param name="param">Info on the event</param>
    /// <exception cref="NotImplementedException"></exception>
    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t param)
    {
        throw new NotImplementedException();
    }

    // Runs once per frame.
    public override void _Process(double delta)
    {
        //Create and allocate memory for an array of pointers
        IntPtr[] messages = new IntPtr[nMaxMessages];

        //Collect up to nMaxMessages that are waiting in the queue on the connection to the server, and load them up into our preallocated message array
        int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(connectionToServer, messages, nMaxMessages);

        //For each message, send it off to further processing
        for(int i = 0; i<numMessages; i++)
            {
            if (messages[i] == IntPtr.Zero) { continue; } //Sanity check. 
            handleNetworkData(SteamNetworkingMessage_t.FromIntPtr(messages[i]));
        }
    }

    /// <summary>
    /// Overloaded method sig just to make things easier. Takes apart the SteamMessage into its important peices and sends them on.
    /// </summary>
    /// <param name="message"></param>
    private void handleNetworkData(SteamNetworkingMessage_t message)
    {
        //Read m_cbSize bytes starting at m_pData (payload size and payload pointer) out to a managed byte array
        byte[] payload = NetworkManager.IntPtrToBytes(message.m_pData, message.m_cbSize);

        //send those pieces of the message we care about onward.
        handleNetworkData(message.m_identityPeer, (MessageType)message.m_nUserData, payload);
    }

    /// <summary>
    /// The primary network processing switch. All network messages entering the Client must pass thru here to be directed to the correct processing location.
    /// </summary>
    /// <param name="identity">Will be either a CSteamID (ulong) or an ipaddress. See SteamNetworkingIdentity</param>
    /// <param name="type">enum message type</param>
    /// <param name="data">raw bytearray of protobuf payload</param>
    private void handleNetworkData(SteamNetworkingIdentity identity, MessageType type, byte[] data)
    {

        switch (type)
        {
            case MessageType.CHAT_BASIC:
                Global.NetworkManager.networkDebugLog("Client - Chat Message Received.");
                ClientChatHandler.handleChatMessage(data);
                break;
            case MessageType.INPUT_MOVEMENTDIRECTION:
                Global.NetworkManager.networkDebugLog("Client - Input Delta Message Received.");
                break;
            case MessageType.INPUT_ACTION:
                Global.NetworkManager.networkDebugLog("Client - Action Delta Message Received.");
                ClientInputHandler.ActionDeltaMessage msg = new ClientInputHandler.ActionDeltaMessage();
                Global.ByteArrayToStructure(data, msg);
                Global.NetworkManager.networkDebugLog("Just decoded this lad, clientID: " + msg.clientID + " compared to ");
                ClientInputHandler.handleActionDeltaMessage(msg);
                break;
            case MessageType.INPUT_FULLCAPTURE:
                Global.NetworkManager.networkDebugLog("Client - Full Input Sync Message Received.");
                break;
            default:
                break;
        }
    }









}


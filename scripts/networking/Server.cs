using Godot;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using static NetworkManager;
using Google.Protobuf;

public partial class Server: Node
    {

    public List<HSteamNetConnection> clients = new();



    public HSteamListenSocket listenSocket = new();


    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;

    public delegate void NewPlayerJoinEventHandler(ulong clientID);
    public static event NewPlayerJoinEventHandler NewPlayerJoinEvent = delegate { };


    public Server(HSteamNetConnection localClient)
    {
        clients.Add(localClient);
        onPlayerJoin(Global.instance.clientID);
    }

    public Server() { }

    public override void _Ready()
    {
        SteamNetConnectionStatusChange = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(onSteamNetConnectionStatusChange);
        
    }

    private void onSteamNetConnectionStatusChange(SteamNetConnectionStatusChangedCallback_t @event)
    {
        bool acceptAllConnections = true;
        if (@event.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
        {
            HSteamNetConnection conn = @event.m_hConn;
            if (acceptAllConnections)
            {
                SteamNetworkingSockets.AcceptConnection(conn);
                clients.Add(conn);
                Global.NetworkManager.networkDebugLog("Accepting external connection.");

                onPlayerJoin((ulong)@event.m_info.m_identityRemote.GetSteamID());
                
            }
        }
    }

    public void onPlayerJoin(ulong clientID)
    {
        ServerMessagePlayerJoin message = new ServerMessagePlayerJoin();
        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = SteamFriends.GetFriendPersonaName((CSteamID)(ulong)identity.SteamID);
        message.NewPlayer = identity;

        //BroadcastMessageWithExclusion(@event.m_info.m_identityRemote, NetworkManager.MessageType.SERVER_NEWPLAYER, message.ToByteArray());
        BroadcastMessage(NetworkManager.MessageType.SERVER_NEWPLAYER, message.ToByteArray());
    }

    public void ServerSpawnPlayer(ulong clientID)
    {
        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = SteamFriends.GetFriendPersonaName((CSteamID)(ulong)identity.SteamID);
        ServerMessageSpawnPlayer message2 = new ServerMessageSpawnPlayer();
        message2.Player = identity;
        Position pos =  new Position();
        pos.X = 0;
        pos.Y = 20;
        pos.Z = 0;
        message2.Position = pos;

        BroadcastMessage(MessageType.SERVER_SPAWNPLAYER, message2.ToByteArray());

    }

    public void ServerLaunchGame()
    {
        ServerMessageLaunchGame msg = new ServerMessageLaunchGame();
        msg.Mode = 1;
        BroadcastMessage(MessageType.SERVER_LAUNCHGAME, msg.ToByteArray());
        Global.NetworkManager.networkDebugLog("Server - Broadcasting server launch command");
    }

    public override void _Process(double delta)
    {
        IntPtr[] messages = new IntPtr[100];
        foreach (HSteamNetConnection conn in clients)
        {
            int numMessages = SteamNetworkingSockets.ReceiveMessagesOnConnection(conn, messages, 100);
            for (int i = 0; i < numMessages; i++)
            {
                if (messages[i] == IntPtr.Zero) { continue; }
                handleNetworkData(SteamNetworkingMessage_t.FromIntPtr(messages[i]));
            }
        }
    }

    private void handleNetworkData(SteamNetworkingMessage_t message)
    {
        Global.debugLog("server here: got this stupid ass packet with type: " + (MessageType)message.m_nUserData);
        handleNetworkData(message.m_identityPeer, (MessageType)message.m_nUserData, NetworkManager.IntPtrToBytes(message.m_pData, message.m_cbSize));
    }


    private void handleNetworkData(SteamNetworkingIdentity identity, MessageType type, byte[] data)
    {
       
        switch (type)
        {
            case MessageType.CHAT_BASIC:
                Global.NetworkManager.networkDebugLog("Server - Chat Message Received - Broadcasting!");
                BroadcastMessage(type, data);   
                break;
            case MessageType.INPUT_MOVEMENTDIRECTION:
                Global.NetworkManager.networkDebugLog("Server - Input Delta Message Recevied!");
                //Apply this input data to my simulation of the players.

                //forward the inputs to all other players for their sims
                BroadcastMessageWithExclusion(identity, type, data);
                //BroadcastMessage(type, data);
                break;
            case MessageType.INPUT_ACTION:
                Global.NetworkManager.networkDebugLog("Server - Action Delta Message Recevied! from: " + identity.GetSteamID());
                //Apply this input data to my simulation of the players.

                //forward the inputs to all other players for their sims
                BroadcastMessageWithExclusion(identity, type, data);
                //BroadcastMessage(type, data);
                break;
            case MessageType.INPUT_FULLCAPTURE:
                Global.NetworkManager.networkDebugLog("Server - Input Sync Message Recevied!");
                //Apply this input data to my simulation of the players.

                //forward the inputs to all other players for their sims
                BroadcastMessageWithExclusion(identity, type, data);
                //BroadcastMessage(type, data);
                break;
            default:
                break;
        }
    }



    private void BroadcastMessage(MessageType type, byte[] data)
    {
        Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + type.ToString());
        foreach (HSteamNetConnection c in clients)
        {
            NetworkManager.SendSteamMessage(type,c,data);
        }
    }

    private void BroadcastMessageWithExclusion(SteamNetworkingIdentity exclude, MessageType type, byte[] data)
    {
        Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + type.ToString() + " with exclusion.");
        foreach (HSteamNetConnection c in clients)
        {
            SteamNetConnectionInfo_t info = new();
            SteamNetworkingSockets.GetConnectionInfo(c,out info);
            if (info.m_identityRemote.Equals(exclude))
            {
                continue;
            }
            NetworkManager.SendSteamMessage(type, c, data);
        }
    }

}

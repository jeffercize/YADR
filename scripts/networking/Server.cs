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
        SteamNetworkingSockets.ConfigureConnectionLanes(localClient, 3, null, null);
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
                Global.NetworkManager.networkDebugLog("Accepting external connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64());
            }
        }
        if (@event.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
        {
            HSteamNetConnection conn = @event.m_hConn;
            clients.Add(conn);
            SteamNetworkingSockets.ConfigureConnectionLanes(conn, 3, null, null);
            onPlayerJoin(conn, (ulong)@event.m_info.m_identityRemote.GetSteamID64());

            Global.NetworkManager.networkDebugLog("Connection from ID: " + @event.m_info.m_identityRemote.GetSteamID64() + " complete!");

        }
    }

    public void onPlayerJoin(HSteamNetConnection conn ,ulong clientID)
    {
        foreach (HSteamNetConnection c in clients)
        {
            if (NetworkManager.getConnectionRemoteID(c) == clientID) { continue; }
            ServerAlertNewPlayerMessage msg = new();
            Identity id = new Identity();
            id.SteamID = (long)NetworkManager.getConnectionRemoteID(c);
            id.Name = SteamFriends.GetFriendPersonaName((CSteamID)(ulong)id.SteamID);
            msg.NewPlayer = id;
            Global.NetworkManager.networkDebugLog("sending out new player notice ID: " + msg.NewPlayer.SteamID);
            SendSteamMessage(conn, WrapMessage(MessageType.ServerAlertNewPlayer, msg));
        }

        ServerAlertNewPlayerMessage message = new();
        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = SteamFriends.GetFriendPersonaName((CSteamID)(ulong)identity.SteamID);
        message.NewPlayer = identity;
        Global.NetworkManager.networkDebugLog("sending out new player notice2 ID: " + message.NewPlayer.SteamID);
        BroadcastMessage(WrapMessage(MessageType.ServerAlertNewPlayer,message));
    }


    public static ServerAlertNewPlayerMessage ConstructNewPlayerMessage(ulong clientID)
    {
        ServerAlertNewPlayerMessage msg = new();
        Identity id = new Identity();
        id.SteamID = (long)clientID;
        id.Name = SteamFriends.GetFriendPersonaName((CSteamID)(ulong)id.SteamID);
        msg.NewPlayer = id;
        return msg;
    }

    public void SendServerCommandLaunchGame()
    {
        ServerCommandLaunchGameMessage msg = new();
        msg.Mode = 1;
        BroadcastMessage(WrapMessage(MessageType.ServerCommandLaunchGame, msg));
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
                SteamNetworkingMessage_t steamMsg = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                Global.NetworkManager.networkDebugLog("SERVER: LANE:" + steamMsg.m_idxLane);
                YADRNetworkMessageWrapper wrappedMessage = DecodeSteamMessage(steamMsg, out long sender);
                handleNetworkData(wrappedMessage,sender);
            }
        }
    }

    private void handleNetworkData(YADRNetworkMessageWrapper message,long sender)
    {
        switch (message.Type)
        {

            case MessageType.ChatBasic:
                BroadcastMessage(message);
                break;


            case MessageType.InputAction:
                BroadcastMessageWithExclusion(sender, message);
                break;
            case MessageType.InputMovementDirection:
                BroadcastMessageWithExclusion(sender, message);
                break;
            case MessageType.InputLookDelta:
                BroadcastMessageWithExclusion(sender, message);
                break;
            case MessageType.InputLookDirection:
                BroadcastMessageWithExclusion(sender, message);
                break;
            case MessageType.InputFullCapture:
                BroadcastMessageWithExclusion(sender, message);
                break;

            case MessageType.PlayerStatePosition:
                Global.NetworkManager.networkDebugLog("Server - got a pos sync for: " + message.PlayerStatePosition.PlayerIdentity.SteamID);
                BroadcastMessageWithExclusion(sender, message);
                break;

            case MessageType.ServerAlertNewPlayer:
                break;

            case MessageType.ServerCommandSpawnPlayer:
                break;
            case MessageType.ServerCommandLaunchGame:
                BroadcastMessage(message);
                break;

            default:
                break;
        }
    }


    public void BroadcastMessage(YADRNetworkMessageWrapper message)
    {
        //Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + message.Type);
        foreach (HSteamNetConnection c in clients)
        {
            NetworkManager.SendSteamMessage(c,message);
        }
    }

    public void BroadcastMessageWithExclusion(long exclude, YADRNetworkMessageWrapper message)
    {
       // Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + message.Type + " while excluding ID: " + exclude);
       if (message.Type == MessageType.PlayerStatePosition)
        {
            Global.NetworkManager.networkDebugLog("GUHGUHGUHGU: " + message.PlayerStatePosition.PlayerIdentity.SteamID);
        }
        foreach (HSteamNetConnection c in clients)
        {
            SteamNetConnectionInfo_t info = new();
            SteamNetworkingSockets.GetConnectionInfo(c, out info);
            if (info.m_identityRemote.GetSteamID64() == (ulong)exclude)
            {
                continue;
            }
            NetworkManager.SendSteamMessage(c, message);
        }
    }


}

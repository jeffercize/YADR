using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using static NetworkManager;

public partial class Server: Node
    {

    public List<HSteamNetConnection> clients = new();



    public HSteamListenSocket listenSocket = new();


    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;

    public Server(HSteamNetConnection localClient)
    {
        clients.Add(localClient);

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
            if (acceptAllConnections)
            {
                SteamNetworkingSockets.AcceptConnection(@event.m_hConn);
                clients.Add(@event.m_hConn);

                Global.NetworkManager.networkDebugLog("Accepting external connection.");
            }
        }
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
                Global.NetworkManager.networkDebugLog("Server - Action Delta Message Recevied!");
                //Apply this input data to my simulation of the players.

                ClientInputHandler.ActionDeltaMessage msg = new ClientInputHandler.ActionDeltaMessage();
                Global.ByteArrayToStructure(data, msg);
                Global.NetworkManager.networkDebugLog("Server - Just decoded this lad, clientID: " + msg.clientID);


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
        Global.NetworkManager.networkDebugLog("Server starting broadcast");
        foreach (HSteamNetConnection c in clients)
        {
            NetworkManager.SendSteamMessage(type,c,data);
        }
    }

    private void BroadcastMessageWithExclusion(SteamNetworkingIdentity exclude, MessageType type, byte[] data)
    {
        Global.NetworkManager.networkDebugLog("Server starting broadcast");
        foreach (HSteamNetConnection c in clients)
        {
            if (SteamNetworkingSockets.GetConnectionUserData(c).Equals(exclude))
            {
                continue;
            }
            NetworkManager.SendSteamMessage(type, c, data);
        }
    }

}

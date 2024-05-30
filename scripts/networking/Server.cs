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
    public FramePacket outgoingFramePacket = new();

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

            Global.NetworkManager.networkDebugLog("sending out new player notice ID: ");

        }


        Global.NetworkManager.networkDebugLog("sending out new player notice2 ID: ");
        //BroadcastMessage(WrapMessage(MessageType.ServerAlertNewPlayer,message));
    }




    public void SendServerCommandLaunchGame()
    {

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
        outgoingFramePacket.Tick = 0;
        outgoingFramePacket.Sender = Global.instance.clientID;
        BroadcastMessage(outgoingFramePacket);
        outgoingFramePacket = new FramePacket();

    }

    private void handleFramePacket(FramePacket framePacket)
    {
        foreach (string chatmessage in framePacket.ChatMessages)
        {
            outgoingFramePacket.ChatMessages.Add(chatmessage);
        }
    }

    public void BroadcastMessage(IMessage message)
    {
        //Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + message.Type);
        foreach (HSteamNetConnection c in clients)
        {
            SendSteamMessage(c,message);
        }
    }

    public void BroadcastMessageWithExclusion(ulong exclude, IMessage message)
    {

        foreach (HSteamNetConnection c in clients)
        {
            SteamNetConnectionInfo_t info = new();
            SteamNetworkingSockets.GetConnectionInfo(c, out info);
            if (info.m_identityRemote.GetSteamID64() == exclude)
            {
                continue;
            }
            SendSteamMessage(c, message);
        }
    }


}

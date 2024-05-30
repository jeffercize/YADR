using Godot;
using Google.Protobuf;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using static NetworkManager;
public partial class Server : Node
{

    public List<HSteamNetConnection> clients = new();
    public HSteamListenSocket listenSocket = new();


    protected Callback<SteamNetConnectionStatusChangedCallback_t> SteamNetConnectionStatusChange;
    public FramePacket outgoingFramePacket = new();


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
                        Global.NetworkManager.networkDebugLog("Server - Received a message on an unexpected lane. Lane: " + steamMsg.m_idxLane);
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
        foreach (Chat chatMessage in framePacket.ChatMessages)
        {
            outgoingFramePacket.ChatMessages.Add(chatMessage);
        }
    }

    public void BroadcastMessage(IMessage message)
    {
        //Global.NetworkManager.networkDebugLog("Server starting broadcast of messagetype: " + message.Type);
        foreach (HSteamNetConnection c in clients)
        {
            SendSteamMessage(c, message);
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

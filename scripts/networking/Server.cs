using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
        IntPtr[] messages = new IntPtr[5];
        foreach (HSteamNetConnection c in clients)
        {
            SteamNetworkingSockets.ReceiveMessagesOnConnection(c, messages, 5);
            foreach (IntPtr msg in messages)
            {
                if (msg == IntPtr.Zero) { continue; }
                //Global.NetworkManager.networkDebugLog("Server got a message.");
                handleNetworkData(Marshal.PtrToStructure<SteamNetworkingMessage_t>(msg));
            }
        }
    }



    private void handleNetworkData(SteamNetworkingMessage_t message)
    {
        byte[] data = new byte[message.m_cbSize];
        MessageType type = NetworkManager.deconstructSteamNetworkingMessage(message, out data);
        handleNetworkData(type, data);

    }

    private void handleNetworkData(MessageType type, byte[] data)
    {

        switch (type)
        {
            case MessageType.CHAT:
                Global.NetworkManager.networkDebugLog("Server - Chat Message Received - Broadcasting!");
                BroadcastMessage(type, data);   
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
            SendSteamMessage(c, type, data);
        }
    }

    public unsafe void SendSteamMessage(HSteamNetConnection conn, MessageType type, byte[] data)
    {
        //Global.NetworkManager.networkDebugLog("Server sending message: " + data.GetStringFromUtf8());
        long result = new();
        byte[] newData = new byte[data.Length + 1];
        Buffer.BlockCopy(data, 0, newData, 1, data.Length);
        newData[0] = (byte)type;
        IntPtr ptr = Marshal.AllocHGlobal(newData.Length);
        Marshal.Copy(newData, 0, ptr, newData.Length);
        SteamNetworkingSockets.SendMessageToConnection(conn, ptr, (uint)newData.Length, NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle, out result);
    }
}

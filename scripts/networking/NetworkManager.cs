using Godot;
using Steamworks;
using System;
using System.Collections.Generic;

using System.Runtime.InteropServices;

using static Steamworks.SteamNetworkingSockets;

public partial class NetworkManager: Node
{

    public const int k_nSteamNetworkingSend_NoNagle = 1;
    public const int k_nSteamNetworkingSend_NoDelay = 4;
    public const int k_nSteamNetworkingSend_Unreliable = 0;
    public const int k_nSteamNetworkingSend_Reliable = 8;
    public const int k_nSteamNetworkingSend_UnreliableNoNagle = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_UnreliableNoDelay = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoDelay | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_ReliableNoNagle = k_nSteamNetworkingSend_Reliable | k_nSteamNetworkingSend_NoNagle;


    ushort port = 9999;

    int maxMessageQueueSize = 100;

    public bool isSteam = true;
    public bool isOffline = false;
    public bool isActive = false;
    public bool localLoopbackReady = false;
    public bool isHost = false;
    public bool isConnected = false;



    List<HSteamNetConnection> connectedClients = new();

    public Client client = null;
    public Server server = null;

    public enum NETWORK_MODE { STEAM, NONSTEAM, OFFLINE };
    private NETWORK_MODE networkMode;

    public enum MessageType { CHAT };

    public override void _Process(double delta)
    {
        
    }

    public void startGame()
    {
        Global.NetworkManager.networkDebugLog("Starting internal server...");
        HSteamNetConnection localConnection = new();
        HSteamNetConnection remoteConnection = new();
        SteamNetworkingIdentity localIdentity = new();
        SteamNetworkingIdentity remoteIdentity = new();
        CreateSocketPair(out localConnection, out remoteConnection, false, ref localIdentity, ref remoteIdentity);
        client = new Client(remoteConnection);
        AddChild(client);
        server = new Server(localConnection);
        
        AddChild(server);
        if (isOffline)
        {
            Global.NetworkManager.networkDebugLog("Offline mode - not opening listen socket.");
            return;
        }
        else if (isSteam)
        {
            Global.NetworkManager.networkDebugLog("Opening Steam listen socket");
            server.listenSocket = CreateListenSocketP2P(0, 0, null);
        }
        else if (!isSteam)
        {
            Global.NetworkManager.networkDebugLog("Opening non-Steam listen socket");
            SteamNetworkingIPAddr test = new SteamNetworkingIPAddr();
            test.Clear();
            test.m_port = port;
            server.listenSocket = CreateListenSocketIP(ref test, 0, null);
        }
        isHost = true;
        isActive = true;
        Global.NetworkManager.networkDebugLog("Internal server started.");
    }

    public void joinGame(CSteamID steamID)
    {
        if (!isSteam)
        {
            Global.debugLog("Attempt to join by SteamID failed, Steam not connected.");
            return;
        }
        if (isConnected)
        {
            Global.debugLog("Cannot join when already connected to a different host.");
            return;
        }
        if (isHost)
        {
            Global.debugLog("Cannot join when hosting a game");
            return;
        }
        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(steamID);
        client = new Client(ConnectP2P(ref identity, 0, 0, null));
        AddChild(client);
        isActive = true;
    }

    public void joinGame(uint ipAddress)
    {
        if (isConnected)
        {
            Global.debugLog("Cannot join when already connected to a different host.");
            return;
        }
        if (isHost)
        {
            Global.debugLog("Cannot join when hosting a game");
            return;
        }
        SteamNetworkingIPAddr ip = new SteamNetworkingIPAddr();
        ip.SetIPv4(ipAddress, port);
        client = new Client(ConnectByIPAddress(ref ip, 0, null));
        AddChild(client);
        isActive = true;
    }

    public void networkDebugLog(string msg)
    {
        Global.debugLog("[netID:" + Global.instance.clientID + "] " + msg);
    }

    public static MessageType deconstructSteamNetworkingMessage(SteamNetworkingMessage_t message, out byte[] data)
    {
        //Global.NetworkManager.networkDebugLog("Parsing Steam Message ----------- ");
        byte[] derefData = new byte[message.m_cbSize];
        Marshal.Copy(message.m_pData, derefData, 0, message.m_cbSize);
        MessageType type = (MessageType)derefData[0];
        //Global.NetworkManager.networkDebugLog("     Type: " + type);


        data = new byte[derefData.Length - 1];
        Buffer.BlockCopy(derefData, 1, data, 0, derefData.Length - 1);
        //Global.NetworkManager.networkDebugLog("     Data: " + data.GetStringFromUtf8());
        return type;

    }

    /*
    public static SteamNetworkingMessage_t constructSteamNetworkingMessage(MessageType type, byte[] data)
    {
        Global.NetworkManager.networkDebugLog("Attempting to build a steam message. (shitty version)");
        SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(SteamNetworkingUtils.AllocateMessage(data.Length));
        unsafe
        {
            fixed (byte* p = data)
            {
                message.m_pData = (IntPtr)p;
                message.m_cbSize = data.Length;
            }
        }

        return message;
    }

    
    public static SteamNetworkingMessage_t constructSteamNetworkingMessage(MessageType type, byte[] data)
    {
        Global.NetworkManager.networkDebugLog("Attempting to build a steam message.");
        SteamNetworkingMessage_t message;
        unsafe
        {
            message = *(SteamNetworkingMessage_t*)SteamNetworkingUtils.AllocateMessage(data.Length).ToPointer();
            fixed (byte* p = data)
            {
                message.m_pData = (IntPtr)p;
            }
        }
        message.m_nUserData = (long)type;
        return message;
    }*/
}


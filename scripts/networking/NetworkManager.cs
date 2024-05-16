using Godot;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

using static Steamworks.SteamNetworkingSockets;

/// <summary>
/// Manager class for all things networking. Singleton pattern, created and managed by Global
/// </summary>
public partial class NetworkManager: Node
{

    //Bitflags from the SteamAPI for sending messages - replicated here to make things easier.
    public const int k_nSteamNetworkingSend_NoNagle = 1;
    public const int k_nSteamNetworkingSend_NoDelay = 4;
    public const int k_nSteamNetworkingSend_Unreliable = 0;
    public const int k_nSteamNetworkingSend_Reliable = 8;
    public const int k_nSteamNetworkingSend_UnreliableNoNagle = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_UnreliableNoDelay = k_nSteamNetworkingSend_Unreliable | k_nSteamNetworkingSend_NoDelay | k_nSteamNetworkingSend_NoNagle;
    public const int k_nSteamNetworkingSend_ReliableNoNagle = k_nSteamNetworkingSend_Reliable | k_nSteamNetworkingSend_NoNagle;

    /// <summary>
    /// The port the system should use. TODO: config this.
    /// </summary>
    ushort port = 9999;

    //Bunch of state bools, TODO: this needs cleaned up and streamlined. Network state needs defined better overall
    public bool isSteam = true;
    public bool isOffline = false;
    public bool isActive = false;
    public bool localLoopbackReady = false;
    public bool isHost = false;
    public bool isConnected = false;

    /// <summary>
    /// Actual Client object. Everyone gets one of these cause there is no dedicated server.
    /// </summary>
    public Client client = null;

    /// <summary>
    /// Actual Server object. Only the host gets one (including Offline!!), will stay null for any joiners
    /// </summary>
    public Server server = null;

    public enum NETWORK_MODE { STEAM, NONSTEAM, OFFLINE };
    private NETWORK_MODE networkMode;

    public enum MessageType { 
        CHAT_BASIC,
        INPUT_MOVEMENTDIRECTION,
        INPUT_ACTION,
        INPUT_FULLCAPTURE,
        SERVER_NEWPLAYER,
        SERVER_SPAWNPLAYER,
        SERVER_LAUNCHGAME
    }

    /// <summary>
    /// Starts the game. Despite being in network manager this also starts singleplayer due to the unified internal server approach.
    /// TODO:this should be different functions or take parameters or something
    /// </summary>
    public void startServer()
    {
        if (isActive)
        {
            networkDebugLog("You are already joined or hosting and cannot start a server.");
            return;
        }

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

    public void launchGame()
    {
        if(isActive && isHost)
        {
            server.ServerLaunchGame();
        }
        else
        {
            networkDebugLog("You aren't hosting a game, you can't start!");
        }
    }

    /// <summary>
    /// Joins to a server using a steamID. This will only connect to a server hosted using the Steam Relay Network.
    /// </summary>
    /// <param name="steamID"></param>
    public void joinGame(CSteamID steamID)
    {
        networkDebugLog("Attempting to join server hosted at: " + steamID);
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
        if (GetConnectionState(client.connectionToServer) == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
        {
            AddChild(client);
            isActive = true;
            networkDebugLog("Successfully connected to server hosted at: " + steamID);
        }
        else
        {
            networkDebugLog("Connecting to server at: " + steamID + " failed!");
        }
 
    }

    public ESteamNetworkingConnectionState GetConnectionState(HSteamNetConnection conn)
    {
        SteamNetworkingSockets.GetConnectionInfo(conn, out SteamNetConnectionInfo_t info);
        return info.m_eState;
    }

    /// <summary>
    /// Joins to a server using an IP address. This will only connect to a server hosted using non-steam UDP
    /// </summary>
    /// <param name="ipAddress"></param>
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
        if (Global.enableLogging)
        {
            Global.debugLog("[netID:" + Global.instance.clientID + "] " + msg);
        }

    }

    /// <summary>
    /// Dereferences a pointer to an array of bytes.
    /// </summary>
    /// <param name="ptr">Pointer to dereference</param>
    /// <param name="cbSize">The number of bytes to read, make sure to get this right</param>
    /// <returns>a raw array of bytes of length cbSize from pointer ptr</returns>
    public static byte[] IntPtrToBytes(IntPtr ptr, int cbSize)
    {
        byte[] retval = new byte[cbSize];
        Marshal.Copy(ptr, retval, 0, cbSize);
        return retval;
    }

    /// <summary>
    /// Sends a message using the SteamNetworkingSockets library. In theory, this should be agnostic to steam vs non-steam networking.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="target"></param>
    /// <param name="data"></param>
    /// <param name="sendFlags"></param>
    /// <returns></returns>
    public static bool SendSteamMessage(MessageType type, HSteamNetConnection target, byte[] data, int sendFlags = k_nSteamNetworkingSend_ReliableNoNagle)
    {
        var msgPtrsToSend = new IntPtr[] { IntPtr.Zero };
        var ptr = IntPtr.Zero;
        try
        {
            ptr = SteamNetworkingUtils.AllocateMessage(data.Length);

            var msg = SteamNetworkingMessage_t.FromIntPtr(ptr);

            // Unfortunately, this allocates a managed SteamNetworkingMessage_t,
            // but the native message currently can't be edited via ptr, even with unsafe code
            Marshal.Copy(data, 0, msg.m_pData, data.Length);

            msg.m_nFlags = NetworkManager.k_nSteamNetworkingSend_ReliableNoNagle;
            msg.m_conn = target;
            msg.m_nUserData = (long)type;

            // Copies the bytes of the managed message back into the native structure located at ptr
            Marshal.StructureToPtr(msg, ptr, false);

            msgPtrsToSend[0] = ptr;
        }
        catch (Exception e)
        {
            // Callers only have responsibility to release the message until it's passed to SendMessages
            SteamNetworkingMessage_t.Release(ptr);
            return false;
        }

        var msgSendResult = new long[] { default };
        SteamNetworkingSockets.SendMessages(1, msgPtrsToSend, msgSendResult);
        EResult result = msgSendResult[0] >= 1 ? EResult.k_EResultOK : (EResult)(-msgSendResult[0]);

        return result == EResult.k_EResultOK;

    }
}


using Godot;
using Networking;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// This class represents the server. It does nothing but replicate messages to all connected clients.
/// Uses Valve's relay network to dodge NATs and IP addresses.
/// </summary>
public partial class SteamSocketManager : SocketManager
{
    /// <summary>
    /// If true, message senders also get a copy of their own message.
    /// </summary>
    bool LOOPBACK_MODE = true;

    Dictionary<uint, ulong> connectIDToSteamIDMap = new Dictionary<uint, ulong>();
    public SteamNetwork network;

    /// <summary>
    /// Called when a client starts connecting to this server.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="data"></param>
    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        base.OnConnecting(connection, data);//The base class will accept the connection
        Steam.debugLog("SocketManager OnConnecting");
    }

    /// <summary>
    /// Called when a client connects to this server
    /// </summary>
    /// <param name="connection">The new connection that represents to bridge between the new client and this server</param>
    /// <param name="data"></param>
    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
        Steam.debugLog("SocketManager OnConnected");
        connectIDToSteamIDMap.Add(connection.Id, data.Identity.SteamId.Value);
    }

    /// <summary>
    /// Called when a client disconnects from this server.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="data"></param>
    public override void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        base.OnDisconnected(connection, data);
        Steam.debugLog("SocketManager OnDisconnected");
    }

    /// <summary>
    /// Called when the server gets a message.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="identity"></param>
    /// <param name="data"></param>
    /// <param name="size"></param>
    /// <param name="messageNum"></param>
    /// <param name="recvTime"></param>
    /// <param name="channel"></param>
    public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        replicateMessageToConnections(data, size, connection.Id);
        Steam.debugLog("SocketManager OnMessage");
    }


    /// <summary>
    /// Takes the received message and shoots it back out to all connected clients.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="size"></param>
    /// <param name="id"></param>
    private void replicateMessageToConnections(IntPtr message, int size, uint id)
    {
        try
        {
            // Loop to only send messages to socket server members who are not the one that sent the message
            for (int i = 0; i < Connected.Count; i++)
            {
                if (LOOPBACK_MODE || (Connected.ElementAt(i).Id != id))
                {
                    Result success = Connected.ElementAt(i).SendMessage(message, size);
                    if (success != Result.OK)
                    {
                        Result retry = Connected.ElementAt(i).SendMessage(message, size); //we try twice idk
                    }
                }
            }
        }
        catch
        {
            Steam.debugLog("Unable to relay socket server message");
        }
    }

    /// <summary>
    /// Unused.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="size"></param>
    private void sendMessageToAllConnections(IntPtr message, int size)
    {
        try
        {
            for (int i = 0; i < Connected.Count; i++)
            {
                Result success = Connected.ElementAt(i).SendMessage(message, size);
                if (success != Result.OK)
                {
                    Result retry = Connected.ElementAt(i).SendMessage(message, size);
                }
            }
        }
        catch
        {
            Steam.debugLog("Unable to relay socket server message");
        }
    }
}


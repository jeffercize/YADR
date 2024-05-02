using Godot;
using Networking;
using Steamworks;
using Steamworks.Data;
using System;
using static Networking.SteamNetwork;

/// <summary>
/// This class represents the network client.
/// It receives from and sends to a single server, which replicates the messages and sends
/// them to all other connected clients. The host gets one of these too, connected to
/// their own server.
/// </summary>
public class SteamConnectionManager : ConnectionManager
{
    //Signal Handlers
    public delegate void chatMessageHandler(ulong steamID, ChatMsg msg);
    public static event chatMessageHandler chatMessage;

    public delegate void gamestateMessageHandler(ulong steamID, GamestateMsg msg);
    public static event gamestateMessageHandler gamestateMessage;

    //Internal vars
    public SteamNetwork network;


    /// <summary>
    /// The entry point of literally every networked signal into the actual game code.
    /// Determines the type of message, then sends that message as a signal to anything that subscribes to it.
    /// This method does not execute any behaviours on its own, it just sends signals/
    /// </summary>
    /// <param name="message"></param>
    /// <param name="senderSteamID"></param>
    /// <param name="type"></param>
    /// <param name="size"></param>
    public void ProcessMessage(byte[] message, ulong senderSteamID, MessageType type, int size)
    {
        switch (type)
        {
            case MessageType.Chat:
                chatMessage.Invoke(senderSteamID, new ChatMsg(message));
                break;
            case MessageType.Gamestate:
                gamestateMessage.Invoke(senderSteamID, new GamestateMsg(message));
                break;
            case MessageType.PlayerPositionSync:
                GD.Print("AH");
                break;
        }
    }

    /// <summary>
    /// Called when this client finishes connecting to a server.
    /// </summary>
    /// <param name="info"></param>
    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        Steam.debugLog("ConnectionManager OnConnected");
    }

    /// <summary>
    /// Called when this client starts connecting to a server
    /// </summary>
    /// <param name="info"></param>
    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Steam.debugLog("ConnectionManager OnConnecting");
    }

    /// <summary>
    /// Called when this client disconnects from a server.
    /// </summary>
    /// <param name="info"></param>
    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        Steam.debugLog("ConnectionManager OnDisconnected");
    }

    /// <summary>
    /// Called when this client gets a message from the server.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="size"></param>
    /// <param name="messageNum"></param>
    /// <param name="recvTime"></param>
    /// <param name="channel"></param>
    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        ProcessMessage(data, size);
        Steam.debugLog("ConnectionManager OnMessage");
    }



    //Below here lies my network code. May god have mercy on my soul.

    public bool SendMessageToSocketServer(byte[] messageToSend)
    {
        try
        {
            // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
            int sizeOfMessage = messageToSend.Length;
            IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
            System.Runtime.InteropServices.Marshal.Copy(messageToSend, 0, intPtrMessage, sizeOfMessage);
            if (network.offline) { ProcessMessage(intPtrMessage, sizeOfMessage); return true; }
            Result success = Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
            if (success == Result.OK)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                return true;
            }
            else
            {
                // RETRY
                Result retry = Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                if (retry == Result.OK)
                {
                    return true;
                }
                return false;
            }
        }
        catch (Exception e)
        {
            Steam.debugLog(e.Message);
            Steam.debugLog("Unable to send message to socket server");
            Steam.debugLog(e.ToString());
            return false;
        }
    }

    public void ProcessMessage(IntPtr data, int size)
    {
        try
        {
            byte[] message = new byte[size - 9];
            MessageType type = (MessageType)System.Runtime.InteropServices.Marshal.ReadByte(data);
            data += 1;
            byte[] senderBytes = new byte[8];
            System.Runtime.InteropServices.Marshal.Copy(data, senderBytes, 0, 8);
            data += 8;
            ulong senderSteamID = BitConverter.ToUInt64(senderBytes);
            System.Runtime.InteropServices.Marshal.Copy(data, message, 0, size - 9);
            ProcessMessage(message, senderSteamID, type, size - 9);
        }
        catch (Exception e)
        {
            Steam.debugLog("Unable to process message from socket server: \n" + e.Message +e.ToString());
        }
    }
}
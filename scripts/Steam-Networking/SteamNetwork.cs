using Godot;
using Steamworks;
using System;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Networking;

/// <summary>
/// Connects the Godot game engine into our networking code (this class will exist as a node on the SceneTree),
/// and handles some networking utility functions.
/// 
/// It also defines all of our message types.
/// </summary>
public partial class SteamNetwork : Node
{
    //Internal vars
    public bool hosting = false;
    public bool connected = false;
    public bool offline = true;
    public SteamSocketManager server;
    public SteamConnectionManager connection;

    //Some random signals idk
    public delegate void serverHostedHandler();
    public static event serverHostedHandler serverHosted;

    public delegate void connectionEstablishedHandler();
    public static event connectionEstablishedHandler connectionEstablished;

    /// <summary>
    /// The datatype that is sent into the network code to be transmitted.
    /// </summary>
    internal readonly struct NetMsg
    {
        public readonly MessageType type;
        public readonly ulong senderSteamID;
        public readonly INetMsgData data;

        public NetMsg(MessageType type, ulong senderSteamID, INetMsgData data)
        {
            this.senderSteamID = senderSteamID;
            this.type = type;
            this.data = data;
        }

        /// <summary>
        /// Transforms this message into its byte[] form, ready for transmission or storage.
        /// </summary>
        /// <returns>A new byte array. The first byte is the MessageType, 
        /// the next 8 are the sender's SteamId, and the rest is the data.</returns>
        public byte[] box()
        {
            byte[] retval = new byte[data.size() + 9];
            retval[0] = (byte)type;
            Buffer.BlockCopy(BitConverter.GetBytes(senderSteamID), 0, retval, 1, 8);
            Buffer.BlockCopy(data.box(), 0, retval, 9, data.size());
            return retval;
        }
    }

    /// <summary>
    /// Interface for the message data classes
    /// </summary>
    public interface INetMsgData
    {
        /// <summary>
        /// Returns the data as a byte[]
        /// </summary>
        /// <returns></returns>
        public byte[] box();

        /// <summary>
        /// Returns size of the data in number of bytes
        /// </summary>
        /// <returns></returns>
        public int size();
    }

    /// <summary>
    /// A chat or text network message. UTF8 encoded
    /// </summary>
    public readonly struct ChatMsg : INetMsgData
    {
        public readonly string message;


        public ChatMsg(string message)
        {
            this.message = message;
        }

        public ChatMsg(byte[] message)
        {
            this.message = Encoding.UTF8.GetString(message);
        }

        public byte[] box()
        {
            return Encoding.UTF8.GetBytes(message);
        }

        public int size()
        {
            return Encoding.UTF8.GetBytes(message).Length;
        }

    }

    public readonly struct PlayerPositionMsg : INetMsgData
    {
        public readonly Vector3 position;
        public PlayerPositionMsg(Vector3 position)
        {
            this.position = position;
        }

        public byte[] box()
        {
            return GD.VarToBytes(position);
        }

        public int size()
        {
            return GD.VarToBytes(position).Length;
        }
    }

    public readonly struct PlayerVelocityMsg : INetMsgData
    {
        public readonly Vector3 velocity;
        public PlayerVelocityMsg(Vector3 velocity)
        {
            this.velocity = velocity;
        }

        public byte[] box()
        {
            return GD.VarToBytes(velocity);
        }

        public int size()
        {
            return GD.VarToBytes(velocity).Length;
        }
    }

    public readonly struct PlayerRotationMsg : INetMsgData
    {
        public readonly Vector3 rotation;
        public PlayerRotationMsg(Vector3 rotation)
        {
            this.rotation = rotation;
        }

        public byte[] box()
        {
            return GD.VarToBytes(rotation);
        }

        public int size()
        {
            return GD.VarToBytes(rotation).Length;
        }
    }
    /// <summary>
    /// Announces a Gamestate change using the gamestate enum.
    /// </summary>
    public readonly struct GamestateMsg : INetMsgData
    {
        public readonly Main.Gamestate state;

        public GamestateMsg(int num)
        {
            state = (Main.Gamestate)num;
        }

        public GamestateMsg(byte[] bytes)
        {
            state = (Main.Gamestate)bytes[0];
        }

        public GamestateMsg(byte bte)
        {
            state = (Main.Gamestate)bte;
        }

        public GamestateMsg(Main.Gamestate state)
        {
            this.state = state;
        }

        public byte[] box()
        {
            return new byte[1] { Convert.ToByte(state) };
        }

        public int size()
        {
            return 1;
        }

        public override string ToString()
        {
            return state.ToString(); ;
        }
    }

    public readonly struct PlayerEquipmentMsg : INetMsgData
    {
        readonly EquipSlot slot;
        readonly Equipable item;
        readonly bool equipped;
        
        public PlayerEquipmentMsg(EquipSlot slot, Equipable item, bool equipped)
        {
            this.slot = slot;
            this.item = item;
            this.equipped = equipped;
        }

        public byte[] box()
        {
            throw new NotImplementedException();
        }

        public int size()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A one byte Messagetype enum.
    /// </summary>
    public enum MessageType
    {
        Chat,
        Gamestate,
        PlayerEquipment,
        PlayerPosition,
        PlayerVelocity,
        PlayerRotation,

        GameConfig,
        LobbyManagement,
        SpawnLoot,
        SpawnItem,
        SpawnNPC,
        PlayerGeneralSync,
        PlayerPositionSync,
        PlayerRotationSync,
        NonItemPhysicsSync,
        ItemPhysicsSync,
        NPCGeneralSync,
        NPCSpecialSync,
        PlayerActionSync,
        PlayerStatsSync,
        UserStatsSync,
        ProjectileSync,
        HitscanSync,

    }

    /// <summary>
    /// Wrapper for underlying networking. Creates a new client and connects to the given SteamID,
    /// </summary>
    /// <param name="id"></param>
    public void connect(SteamId id)
    {
        offline = false;
        connection = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(id);
        connected = true;
        connection.network = this;
    }

    /// <summary>
    /// Wrapper for underlying networking. Creates a new host, then creates a new client and connects to it.
    /// </summary>
    public void host()
    {
        offline = false;
        server = SteamNetworkingSockets.CreateRelaySocket<SteamSocketManager>();
        server.network = this;
        hosting = true;
        connection = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(SteamClient.SteamId);
        connection.network = this;
        connected = true;

        serverHosted.Invoke();
    }

    /// <summary>
    /// Reconfigures the networking layer to allow everything to function normally without any internet use
    /// </summary>
    public void startOffline()
    {
        server = SteamNetworkingSockets.CreateRelaySocket<SteamSocketManager>();
        server.network = this;
        connection = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(SteamClient.SteamId);
        connection.network = this;
        offline = true;
    }

    /// <summary>
    /// Check for network messages every tick.
    /// </summary>
    /// <param name="delta"></param>
    public override void _Process(double delta)
    {
        if (hosting) { server.Receive(); }
        if (connected) { connection.Receive(); }

    }

    /// <summary>
    /// Shuts down the server
    /// </summary>
    public void closeServer()
    {
        try
        {
            server.Close();
            offline = true;
        }
        catch (Exception)
        {

            Steam.debugLog("Error closing server.");
        }
    }

    /// <summary>
    /// Shuts down the client
    /// </summary>
    public void closeClient()
    {
        try
        {
            connection.Close();
            offline = true;
        }
        catch (Exception)
        {

            Steam.debugLog("Error closing connection.");
        }

    }

    /// <summary>
    /// this probably shouldnt be here idk. Sends a chat message to the server.
    /// </summary>
    /// <param name="text"></param>
    internal void sendChatMessage(string text)
    {
        NetMsg msg = new NetMsg(MessageType.Chat, SteamClient.SteamId, new ChatMsg(text));
        connection.SendMessageToSocketServer(msg.box(), 1);
    }
    public void sendMessage(MessageType type, ulong sender, INetMsgData data)
    {
        NetMsg msg = new NetMsg(type, sender, data);
        connection.SendMessageToSocketServer(msg.box(), 1);
    }
}

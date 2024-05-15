using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ClientChatHandler;
using static System.Collections.Specialized.BitVector32;



    public class ClientInputHandler
    {

    public delegate void NetworkActionDeltaEventHandler(ulong clientID, InputManager.ActionEnum action, bool newState);
    public static event NetworkActionDeltaEventHandler NetworkActionDeltaEvent = delegate { };

    public delegate void NetworkInputDeltaEventHandler(ulong clientID, InputType type, Vector2 newState);
    public static event NetworkInputDeltaEventHandler NetworkInputDeltaEvent = delegate { };

    public delegate void NetworkInputSyncEventHandler(ulong clientID, Vector2 direction, Vector2 lookDelta, Vector3 lookDirection, Dictionary<InputManager.ActionEnum, bool> actions);
    public static event NetworkInputSyncEventHandler NetworkInputSyncEvent = delegate { };

    static bool disable = false;

    public enum InputType
    {
        MOVEDIRECTION,
        LOOKDELTA,
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct ActionDeltaMessage
    {
       public ulong clientID;
       public InputManager.ActionEnum action;
       public bool newState;

        public ActionDeltaMessage deserialize<ActionDeltaMessage>(byte[] bytes)
        {
            throw new NotImplementedException();
        }

    }


    public struct InputDeltaMessage
    {
        public ulong clientID;
        public InputType type;
        public Vector2 newState;
    }


    public struct InputSyncMessage
    {
        public ulong clientID;
        public Vector2 direction;
        public Vector2 lookDelta;
        public Vector3 lookDirection;
        public byte[] actionsSerialized;
    }


    internal static void handleInputSyncMessage(ulong clientID, InputSyncMessage message)
    {
        Global.NetworkManager.networkDebugLog("Got an Input sync message.");
        Dictionary<InputManager.ActionEnum, bool> actions = JsonSerializer.Deserialize< Dictionary<InputManager.ActionEnum, bool>>(message.actionsSerialized.GetStringFromUtf8());
        NetworkInputSyncEvent.Invoke(clientID, message.direction, message.lookDelta, message.lookDirection, actions);
    }


    internal static void handleInputDeltaMessage(InputDeltaMessage message)
    {
        if (message.type == InputType.LOOKDELTA) { return; }
        Global.NetworkManager.networkDebugLog("Got an Input delta message.");
        NetworkInputDeltaEvent.Invoke(message.clientID, message.type, message.newState);
    }

    internal static void handleActionDeltaMessage(ActionDeltaMessage message)
    {
        Global.NetworkManager.networkDebugLog("Got an action delta message from player: " + message.clientID);
        NetworkActionDeltaEvent.Invoke(message.clientID, message.action, message.newState);
    }


    internal static void CreateAndSendInputSyncMessage(ulong clientID, PlayerInputData input)
    {
        if (!Global.NetworkManager.isActive || disable) { return; }

       InputSyncMessage msg = new InputSyncMessage();
        msg.clientID = clientID;


        msg.actionsSerialized = JsonSerializer.Serialize(input.actionStates).ToUtf8Buffer();


        Global.NetworkManager.networkDebugLog("CLIENT - Sending InputSync Message");

        byte[] data = Global.StructureToByteArray(msg);

        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_FULLCAPTURE, Global.NetworkManager.client.connectionToServer, data);

    }

    internal static void CreateAndSendActionDeltaMessage(ulong clientID, InputManager.ActionEnum action, bool newState)
    {
        if (!Global.NetworkManager.isActive || disable) { return; }

        ActionDeltaMessage msg = new ActionDeltaMessage();
        msg.clientID = clientID;
        msg.action = action;
        msg.newState = newState;
         

        Global.NetworkManager.networkDebugLog("CLIENT - Sending Action Delta Message from id:" +msg.clientID);

        byte[] data = Global.StructureToByteArray(msg);
        ActionDeltaMessage fuck = new ActionDeltaMessage();
        Global.ByteArrayToStructure(data, fuck);
        Global.NetworkManager.networkDebugLog("fuck: " + fuck.clientID);

        
        int size = Marshal.SizeOf(msg);
        byte[] arr = new byte[size];
        IntPtr ptr = IntPtr.Zero;
        ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(msg, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);



        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_ACTION, Global.NetworkManager.client.connectionToServer, data);
    }

    internal static void CreateAndSendInputDeltaMessage(ulong clientID, InputType type, Vector2 newState)
    {
        if (!Global.NetworkManager.isActive || disable) { return; }

        InputDeltaMessage msg = new InputDeltaMessage();
        msg.clientID = clientID;
        msg.type = type;
        msg.newState = newState;


        Global.NetworkManager.networkDebugLog("CLIENT - Sending Input Delta Message");

        byte[] data = Global.StructureToByteArray(msg);
        //Global.debugLog("bet im dead");
        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_MOVEMENTDIRECTION, Global.NetworkManager.client.connectionToServer ,data);
    }

}

public interface INetworkMessage
{

    public byte[] serialize();
    public T deserialize<T>(byte[] bytes);

    public int getSize();
}
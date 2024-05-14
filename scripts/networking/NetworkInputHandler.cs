using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static NetworkChatHandler;
using static System.Collections.Specialized.BitVector32;



    public class NetworkInputHandler
    {

    public delegate void NetworkActionDeltaEventHandler(ulong clientID, InputManager.ActionEnum action, bool newState);
    public static event NetworkActionDeltaEventHandler NetworkActionDeltaEvent = delegate { };

    public delegate void NetworkInputDeltaEventHandler(ulong clientID, InputType type, Vector2 newState);
    public static event NetworkInputDeltaEventHandler NetworkInputDeltaEvent = delegate { };

    public delegate void NetworkInputSyncEventHandler(ulong clientID, PlayerInputData input);
    public static event NetworkInputSyncEventHandler NetworkInputSyncEvent = delegate { };

    public enum InputType
    {
        MOVEDIRECTION,
        LOOKDELTA,
    }

  

    public struct ActionDeltaMessage
    {
       public ulong clientID;
       public InputManager.ActionEnum action;
       public bool newState;
    }

    public struct InputDeltaMessage
    {
        public ulong clientID;
        public InputType type;
        public Vector2 newState;
    }


    internal static void handleInputSyncMessage(ulong clientID, PlayerInputData input)
    {
        Global.NetworkManager.networkDebugLog("Got an Input sync message.");
        NetworkInputSyncEvent.Invoke(clientID, input);
    }


    internal static void handleInputDeltaMessage(InputDeltaMessage message)
    {
        if (message.type == InputType.LOOKDELTA) { return; }
        Global.NetworkManager.networkDebugLog("Got an Input delta message.");
        NetworkInputDeltaEvent.Invoke(message.clientID, message.type, message.newState);
    }

    internal static void handleActionDeltaMessage(ActionDeltaMessage message)
    {
        Global.NetworkManager.networkDebugLog("Got an action delta message.");
        NetworkActionDeltaEvent.Invoke(message.clientID, message.action, message.newState);
    }


    internal static void CreateAndSendInputSyncMessage(ulong clientID, PlayerInputData input)
    {
        if (!Global.NetworkManager.isActive) { return; }
        Global.NetworkManager.networkDebugLog("CLIENT - Sending Input Sync Message");
        //Global.NetworkManager.client.SendSteamMessage(NetworkManager.MessageType.FULLINPUTCAPTURE);

    }

    internal static void CreateAndSendActionDeltaMessage(ulong clientID, InputManager.ActionEnum action, bool newState)
    {
        if (!Global.NetworkManager.isActive) { return; }

        ActionDeltaMessage msg = new ActionDeltaMessage();
        msg.clientID = clientID;
        msg.action = action;
        msg.newState = newState;


        Global.NetworkManager.networkDebugLog("CLIENT - Sending Action Delta Message");

        byte[] data = Global.StructureToByteArray(msg);
        //Global.debugLog("bet im dead");
        Global.NetworkManager.client.SendSteamMessage(NetworkManager.MessageType.ACTIONDELTA,data);


    }

    internal static void CreateAndSendInputDeltaMessage(ulong clientID, InputType type, Vector2 newState)
    {
        if (!Global.NetworkManager.isActive) { return; }

        InputDeltaMessage msg = new InputDeltaMessage();
        msg.clientID = clientID;
        msg.type = type;
        msg.newState = newState;


        Global.NetworkManager.networkDebugLog("CLIENT - Sending Input Delta Message");

        byte[] data = Global.StructureToByteArray(msg);
        //Global.debugLog("bet im dead");
        Global.NetworkManager.client.SendSteamMessage(NetworkManager.MessageType.INPUTDELTA, data);


    }

}


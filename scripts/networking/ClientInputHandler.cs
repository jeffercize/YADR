using Godot;
using NetworkMessages;
using System.Collections.Generic;
using Google.Protobuf;

public class ClientInputHandler
    {

    public delegate void NetworkInputActionEventHandler(InputActionMessage message);
    public static event NetworkInputActionEventHandler NetworkInputActionEvent = delegate { };

    public delegate void NetworkInputMovementDirectionEventHandler(InputMovementDirectionMessage message);
    public static event NetworkInputMovementDirectionEventHandler NetworkInputMovementDirectionEvent = delegate { };

    public delegate void NetworkInputFullCaptureEventHandler(InputFullCaptureMessage message);
    public static event NetworkInputFullCaptureEventHandler NetworkInputFullCaptureEvent = delegate { };

    static bool disable = false;

    public enum InputType
    {
        MOVEDIRECTION,
        LOOKDELTA,
    }


    internal static void handleInputSyncMessage(byte[] data)
    {
        InputFullCaptureMessage message = InputFullCaptureMessage.Parser.ParseFrom(data);
        Global.NetworkManager.networkDebugLog("Got an input full capture message from player: " + message.InputOf.SteamID);
        NetworkInputFullCaptureEvent.Invoke(message);
    }


    internal static void HandleInputMovementDirectionMessage(byte[] data)
    {
        InputMovementDirectionMessage message = InputMovementDirectionMessage.Parser.ParseFrom(data);
        Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " just changed movement direction.");
        NetworkInputMovementDirectionEvent.Invoke(message);
    }

    internal static void HandleInputActionMessage(byte[] data)
    {
        InputActionMessage message = InputActionMessage.Parser.ParseFrom(data);
        Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " just did action: " + message.Action.ActionType.ToString());
        NetworkInputActionEvent.Invoke(message);
    }


    internal static void CreateAndSendInputSyncMessage(ulong clientID, PlayerInputData input)
    {
        if (!Global.NetworkManager.isActive ) { return; }
        
        InputFullCaptureMessage message = new InputFullCaptureMessage();

        Identity identity = new Identity();
        identity.Name = Global.instance.clientName;
        identity.SteamID = (long)clientID;
        message.InputOf = identity;

        DirectionVector movementDirectionVector = new DirectionVector();
        movementDirectionVector.X = input.direction.X;
        movementDirectionVector.Y = input.direction.Y;
        message.MovementDirection = movementDirectionVector;

        foreach (ActionType action in input.actionStates.Keys)
        {
            ActionMessage actionMessage = new ActionMessage();
            actionMessage.ActionType = action;
            actionMessage.ActionState = input.actionStates[action];
            message.Actions.Add(actionMessage);
        }


        byte[] bytes = message.ToByteArray();
        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_FULLCAPTURE, Global.NetworkManager.client.connectionToServer, bytes);
    }

    internal static void CreateAndSendActionDeltaMessage(ulong clientID, ActionType type, ActionState state)
    {
        if (!Global.NetworkManager.isActive) { return; }

        InputActionMessage msg = new InputActionMessage();

        ActionMessage actionMessage = new ActionMessage();
        ActionType actionType = type;
        ActionState actionState = state;
        actionMessage.ActionState = actionState;
        actionMessage.ActionType = actionType;

        msg.Action = actionMessage;

        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = Global.instance.clientName;

        msg.InputOf = identity;

        byte[] bytes = msg.ToByteArray();

        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_ACTION, Global.NetworkManager.client.connectionToServer, bytes);
    }

    internal static void CreateAndSendInputMovementDirectionMessage(ulong clientID, Vector2 newState)
    {
        if (!Global.NetworkManager.isActive) { return; }
        
        InputMovementDirectionMessage msg = new InputMovementDirectionMessage();

        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = Global.instance.clientName;
        msg.InputOf = identity;
        
        DirectionVector directionVector = new DirectionVector();
        directionVector.X = newState.X;
        directionVector.Y = newState.Y;
        msg.Direction = directionVector;

        byte[] bytes = msg.ToByteArray();

        NetworkManager.SendSteamMessage(NetworkManager.MessageType.INPUT_MOVEMENTDIRECTION, Global.NetworkManager.client.connectionToServer ,bytes);
    }

}


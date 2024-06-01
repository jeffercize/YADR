public class ClientInputHandler
{
    /*
    public delegate void NetworkInputActionEventHandler(InputActionMessage message);
    public static event NetworkInputActionEventHandler NetworkInputActionEvent = delegate { };

    public delegate void NetworkInputMovementDirectionEventHandler(InputMovementDirectionMessage message);
    public static event NetworkInputMovementDirectionEventHandler NetworkInputMovementDirectionEvent = delegate { };

    public delegate void NetworkInputLookDeltaEventHandler(InputLookDeltaMessage message);
    public static event NetworkInputLookDeltaEventHandler NetworkInputLookDeltaEvent = delegate { };

    public delegate void NetworkInputLookDirectionEventHandler(InputLookDirectionMessage message);
    public static event NetworkInputLookDirectionEventHandler NetworkInputLookDirectionEvent = delegate { };

    public delegate void NetworkInputFullCaptureEventHandler(InputFullCaptureMessage message);
    public static event NetworkInputFullCaptureEventHandler NetworkInputFullCaptureEvent = delegate { };

    static bool disable = false;



    internal static void handleInputSyncMessage(InputFullCaptureMessage message)
    {
        Global.NetworkManager.networkDebugLog("Got an input full capture message from player: " + message.InputOf.SteamID);
        NetworkInputFullCaptureEvent.Invoke(message);
    }


    internal static void HandleInputMovementDirectionMessage(InputMovementDirectionMessage message)
    {
        Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " just changed movement direction.");
        NetworkInputMovementDirectionEvent.Invoke(message);
    }

    internal static void HandleInputActionMessage(InputActionMessage message)
    {
        Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " just did action: " + message.Action.ActionType.ToString());
        NetworkInputActionEvent.Invoke(message);
    }

    internal static void HandleInputLookDeltaMessage(InputLookDeltaMessage message)
    {
        //Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " just did action: " + message.Action.ActionType.ToString());
        NetworkInputLookDeltaEvent.Invoke(message);
    }

    internal static void HandleInputLookDirectionMessage(InputLookDirectionMessage message)
    {
        //Global.NetworkManager.networkDebugLog("Player: " + message.InputOf.Name + " DirectionSYNC ");
        NetworkInputLookDirectionEvent.Invoke(message);
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

        NetworkManager.SendSteamMessage(Global.NetworkManager.client.connectionToServer, MessageType.InputFullCapture, message);
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

        NetworkManager.SendSteamMessage( Global.NetworkManager.client.connectionToServer, MessageType.InputAction,msg);
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

        NetworkManager.SendSteamMessage(Global.NetworkManager.client.connectionToServer, MessageType.InputMovementDirection, msg);
    }

    internal static void CreateAndSendInputLookDeltaMessage(ulong clientID, Vector2 delta)
    {
        if (!Global.NetworkManager.isActive) { return; }

        InputLookDeltaMessage msg = new InputLookDeltaMessage();

        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = Global.instance.clientName;
        msg.InputOf = identity;

        DirectionVector directionVector = new DirectionVector();
        directionVector.X = delta.X;
        directionVector.Y = delta.Y;
        msg.Delta = directionVector;

        NetworkManager.SendSteamMessage(Global.NetworkManager.client.connectionToServer, MessageType.InputLookDelta, msg);
    }

    internal static void CreateAndSendInputLookDirectionMessage(ulong clientID, Vector3 direction)
    {
        if (!Global.NetworkManager.isActive) { return; }

        InputLookDirectionMessage msg = new InputLookDirectionMessage();

        Identity identity = new Identity();
        identity.SteamID = (long)clientID;
        identity.Name = Global.instance.clientName;
        msg.InputOf = identity;

        DirectionVector directionVector = new DirectionVector();
        directionVector.X = direction.X;
        directionVector.Y = direction.Y;
        directionVector.Z = direction.Z;
        msg.Direction = directionVector;

        NetworkManager.SendSteamMessage(Global.NetworkManager.client.connectionToServer, MessageType.InputLookDirection, msg);
    }

<<<<<<< HEAD
    */
=======

>>>>>>> refs/remotes/origin/master
}


using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Net;

public partial class InputManager: Node
{
    /*
    public delegate void InputEventHandler(ulong clientID, ActionMessage actionMessage);
    public static event InputEventHandler InputEvent = delegate { };

    public PlayerInputData localInput = new();

    public Dictionary<ulong,PlayerInputData> remoteInputs = new Dictionary<ulong,PlayerInputData>();

    public float mouseSens = .1f;


    double InputSyncTimer = 1f;
    double InputSyncCounter = 0f;



    public void BindRemoteClientInput(ulong remoteClient, Player player)
    {
        remoteInputs.Add(remoteClient, new PlayerInputData());
        player.input = remoteInputs[remoteClient];
        player.clientID = remoteClient;
    }

    public override void _PhysicsProcess(double delta)
    {
        InputSyncCounter += delta;
        if (InputSyncCounter > InputSyncTimer)
        {
            //ClientInputHandler.CreateAndSendInputSyncMessage(Global.instance.clientID, localInput);
            InputSyncCounter = 0;
        }

    }

    public override void _Process(double delta)
    {
        
    }

    public override void _Ready()
    {
        SetProcessInput(false);
        ClientInputHandler.NetworkInputActionEvent += onNetworkInputActionEvent;
        ClientInputHandler.NetworkInputMovementDirectionEvent += onNetworkInputMovementDirectionEvent;
        ClientInputHandler.NetworkInputFullCaptureEvent += onNetworkInputFullCaptureEvent;
        ClientInputHandler.NetworkInputLookDeltaEvent += onNetworkInputLookDeltaEvent;
        ClientInputHandler.NetworkInputLookDirectionEvent += onNetworkInputLookDirectionEvent;
    }

    private void onNetworkInputLookDirectionEvent(InputLookDirectionMessage message)
    {
        if (remoteInputs.TryGetValue((ulong)message.InputOf.SteamID, out PlayerInputData value))
        {
            value.lookDirection.X = message.Direction.X;
            value.lookDirection.Y = message.Direction.Y;
            value.lookDirection.Z = message.Direction.Z;
        }
    }

    private void onNetworkInputLookDeltaEvent(InputLookDeltaMessage message)
    {
        if (remoteInputs.TryGetValue((ulong)message.InputOf.SteamID, out PlayerInputData value))
        {
            value.lookDelta.X = message.Delta.X;
            value.lookDelta.Y = message.Delta.Y;
        }
    }

    private void onNetworkInputFullCaptureEvent(InputFullCaptureMessage message)
    {
        if (remoteInputs.TryGetValue((ulong)message.InputOf.SteamID, out PlayerInputData value))
        {
            value.lookDirection.X = message.LookDirection.X;
            value.lookDirection.Y = message.LookDirection.Y;
            value.lookDirection.Z = message.LookDirection.Z;
            value.lookDelta.X = message.LookDelta.X;
            value.lookDelta.Y = message.LookDelta.Y;
            value.direction.X = message.MovementDirection.X;
            value.direction.Y = message.MovementDirection.Y;

            foreach (ActionMessage actionMessage in message.Actions)
            {
                value.actionStates[actionMessage.ActionType] = actionMessage.ActionState;
            }
        }
    }

    private void onNetworkInputMovementDirectionEvent(InputMovementDirectionMessage message)
    {
        if (remoteInputs.TryGetValue((ulong)message.InputOf.SteamID, out PlayerInputData value))
        {
            value.direction.X = message.Direction.X;
            value.direction.Y = message.Direction.Y;
        }
    }

    private void onNetworkInputActionEvent(InputActionMessage message)
    {
        if(remoteInputs.TryGetValue((ulong)message.InputOf.SteamID, out PlayerInputData value))
        {
            value.actionStates[message.Action.ActionType] = message.Action.ActionState;
        }
        InputEvent.Invoke((ulong)message.InputOf.SteamID, message.Action);
    }

    public override void _Input(InputEvent @event)
    {
        checkMouse(@event);
        checkControllerAxis(@event);
        checkDirectionKeys(@event);
        foreach(ActionType action in Enum.GetValues(typeof(ActionType)))
        {
            checkAction(@event, action);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {

    }

    private void checkDirectionKeys(InputEvent @event)
    {
        if (@event.IsAction("moveForward") || @event.IsAction("moveBackward") || @event.IsAction("moveLeft") || @event.IsAction("moveRight"))
        {
            localInput.direction = Input.GetVector("moveForward", "moveBackward", "moveLeft", "moveRight");
            if (!@event.IsEcho())
            {
                ClientInputHandler.CreateAndSendInputMovementDirectionMessage(Global.instance.clientID, localInput.direction);
            }
        }

    }

    private void checkAction(InputEvent @event, ActionType action)
    {
        string actionName = Enum.GetName(typeof(ActionType), action);

        if (@event.IsAction(actionName))
        {
            ActionMessage msg = new ActionMessage();
            msg.ActionType = action;
            if (@event.IsActionPressed(actionName))
            {
                localInput.actionStates[action] = ActionState.Pressed;
                msg.ActionState = ActionState.Pressed;
                InputEvent.Invoke(Global.instance.clientID, msg);
                ClientInputHandler.CreateAndSendActionDeltaMessage(Global.instance.clientID, action, ActionState.Pressed);
            }
            else if (@event.IsActionReleased(actionName))
            {
                localInput.actionStates[action] = ActionState.Released;
                msg.ActionState = ActionState.Released;
                InputEvent.Invoke(Global.instance.clientID, msg);
                ClientInputHandler.CreateAndSendActionDeltaMessage(Global.instance.clientID, action, ActionState.Released);
            }
        }
    }
    private void checkMouse(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouse && Input.MouseMode==Input.MouseModeEnum.Captured)
        {
            localInput.lookDelta = mouse.Relative * mouseSens;
            //ClientInputHandler.CreateAndSendInputLookDeltaMessage(Global.instance.clientID, localInput.lookDelta);
        }
    }

    private void checkControllerAxis(InputEvent @event)
    {
        //throw new NotImplementedException();
    }


    */
}
using Godot;
using System;
using System.Collections.Generic;
using System.Net;

public partial class InputManager: Node
{

    public delegate void InputEventHandler(ulong clientID, InputManager.ActionEnum action, bool newState);
    public static event InputEventHandler InputEvent = delegate { };

    public static string[] actionList = { "sprint", "scope", "aim", "inventory", "jump", "crouch", "prone", "walk", "leanleft", "leanright", "interact" };

    public enum ActionEnum
    {
        jump,
        sprint,
        walk,
        crouch,
        prone,
        aim,
        leanleft,
        leanright,
        scope,
        interact,
        inventory,
    }

    public PlayerInputData localInput = new();

    public Dictionary<ulong,PlayerInputData> remoteInputs = new Dictionary<ulong,PlayerInputData>();

    public float mouseSens = .1f;


    double InputSyncTimer = 1f;
    double InputSyncCounter = 0f;



    public void BindRemoteClientInput(ulong remoteClient, BasePlayer player)
    {
        remoteInputs.Add(remoteClient, new PlayerInputData());
        player.input = remoteInputs[remoteClient];
    }

    public override void _PhysicsProcess(double delta)
    {
        InputSyncCounter += delta;
        if (InputSyncCounter > InputSyncTimer)
        {
            //NetworkInputHandler.CreateAndSendInputSyncMessage(Global.instance.clientID, localInput);
            InputSyncCounter = 0;
        }

    }

    public override void _Process(double delta)
    {
        
    }

    public override void _Ready()
    {
        ClientInputHandler.NetworkActionDeltaEvent += onActionDelta;
        ClientInputHandler.NetworkInputDeltaEvent += onInputDelta;
        ClientInputHandler.NetworkInputSyncEvent += onInputSync;
    }

    public override void _Input(InputEvent @event)
    {
        checkMouse(@event);
        checkControllerAxis(@event);
        checkDirectionKeys(@event);
        foreach(InputManager.ActionEnum action in Enum.GetValues(typeof(ActionEnum)))
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
                ClientInputHandler.CreateAndSendInputDeltaMessage(Global.instance.clientID, ClientInputHandler.InputType.MOVEDIRECTION, localInput.direction);
            }
        }

    }

    private void checkAction(InputEvent @event, InputManager.ActionEnum action)
    {
        if (@event.IsAction(action.ToString()))
        {
            if (@event.IsActionPressed(Enum.GetName( typeof(InputManager.ActionEnum),action)))
            {
                localInput.actionStates[action] = true;
                InputEvent.Invoke(Global.instance.clientID, action, true);
                ClientInputHandler.CreateAndSendActionDeltaMessage(Global.instance.clientID, action, true);
            }
            else if (@event.IsActionReleased(Enum.GetName(typeof(InputManager.ActionEnum), action)))
            {
                localInput.actionStates[action] = false;
                InputEvent.Invoke(Global.instance.clientID, action, false);
                ClientInputHandler.CreateAndSendActionDeltaMessage(Global.instance.clientID, action, false);
            }
        }
    }
    private void checkMouse(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouse && Input.MouseMode==Input.MouseModeEnum.Captured)
        {
            localInput.lookDelta = mouse.Relative * mouseSens;
            //NetworkInputHandler.CreateAndSendInputDeltaMessage(Global.instance.clientID, NetworkInputHandler.InputType.LOOKDELTA,localInput.lookDelta);
        }
    }

    private void checkControllerAxis(InputEvent @event)
    {
        //throw new NotImplementedException();
    }

    private void onInputSync(ulong clientID, Vector2 direction, Vector2 lookDelta, Vector3 lookDirection, Dictionary<InputManager.ActionEnum, bool> actions)
    {
        if (remoteInputs.TryGetValue(clientID, out PlayerInputData input))
        {
            remoteInputs[clientID].direction = direction;
            remoteInputs[clientID].lookDelta = lookDelta;
            remoteInputs[clientID].actionStates = new Dictionary<InputManager.ActionEnum, bool>(actions);
            remoteInputs[clientID].lookDirection = lookDirection; 
        }
    }

    private void onInputDelta(ulong clientID, ClientInputHandler.InputType type, Vector2 newState)
    {
        if (remoteInputs.TryGetValue(clientID, out PlayerInputData input))
        {
            switch (type)
            {
                case ClientInputHandler.InputType.MOVEDIRECTION:
                        input.direction = newState;
                    break;
                case ClientInputHandler.InputType.LOOKDELTA:
                        input.direction = newState;
                    break;
                default:
                    break;
            }
        }
    }

    private void onActionDelta(ulong clientID, InputManager.ActionEnum action, bool newState)
    {
        Global.NetworkManager.networkDebugLog("InputChad here, just got an action delta for player: " + clientID);
        if (remoteInputs.TryGetValue(clientID, out PlayerInputData remoteInput))
        {
            Global.NetworkManager.networkDebugLog("     Yeah we got that player in our files. Applying action state.");
            remoteInput.actionStates[action] = newState;
            InputEvent.Invoke(clientID, action, newState);
        }

    }

}
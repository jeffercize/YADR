using Godot;
using System;
using System.Collections.Generic;
using System.Net;

public partial class InputManager: Node
{
    public delegate void InputStateChangeEventHandler(string clientID, string action, bool state, Dictionary<string, bool> inputStates);
    public static event InputStateChangeEventHandler InputStateChange = delegate { };

    public string[] actionList = { "sprint", "scope", "aim", "inventory", "jump", "crouch", "prone", "walk", "leanleft", "leanright", "interact" };

    public Dictionary<string, bool> inputStates = new Dictionary<string, bool>();
    public Vector2 inputDirection = Vector2.Zero;
    public Vector2 lookDelta = Vector2.Zero;


    public float mouseSens = 1f;

    public override void _Input(InputEvent @event)
    {
        checkMouse(@event);
        checkControllerAxis(@event);
        checkDirectionKeys(@event);
        foreach(string action in actionList)
        {
            checkAction(@event, action);
        }
    }

    private void checkDirectionKeys(InputEvent @event)
    {
        if (@event.IsAction("moveForward") || @event.IsAction("moveBackward") || @event.IsAction("moveLeft") || @event.IsAction("moveRight"))
        {
            inputDirection = Input.GetVector("moveForward", "moveBackward", "moveLeft", "moveRight");
        }
    }

    private void checkAction(InputEvent @event, string action)
    {
        if (@event.IsAction(action))
        {
            if (@event.IsActionPressed(action))
            {
                inputStates[action] = true;
                InputStateChange.Invoke(Global.instance.clientID, action, true, inputStates);
            }
            else if (@event.IsActionReleased(action))
            {
                inputStates[action] = false;
                InputStateChange.Invoke(Global.instance.clientID, action, false, inputStates);
            }
        }
    }
    private void checkMouse(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouse)
        {
            lookDelta = mouse.Relative * mouseSens;
        }
    }

    private void checkControllerAxis(InputEvent @event)
    {
        //throw new NotImplementedException();
    }

}
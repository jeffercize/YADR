using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;

public partial class InputManager : Node
{
    public float mouseSens = .1f;

    public PlayerInput localInput = new PlayerInput();

    public delegate void InputActionEventHandler(StringName action,bool pressed);
    public event InputActionEventHandler InputActionEvent;

    public override void _Ready()
    {
        SetProcessInput(false);
        localInput.MovementDirection = new Vec2() { X = 0, Y = 0 };
        localInput.LookDelta = new Vec2() { X = 0, Y = 0 };
        localInput.LookDirection = new Vec2() { X = 0, Y = 0};
        localInput.ClientID = Global.clientID;
    }


    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouse && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Vector2 mouseDelta = (mouse.ScreenRelative * mouseSens);
            localInput.LookDelta = new Vec2() { X = mouseDelta.X, Y = mouseDelta.Y };
            localInput.LookDirection = new Vec2() { X = localInput.LookDirection.X - mouseDelta.X, Y = localInput.LookDirection.Y - mouseDelta.Y };
        }

        if (@event.IsAction("moveForward") || @event.IsAction("moveBackward") || @event.IsAction("moveLeft") || @event.IsAction("moveRight"))
        {
            Vector2 movementDirection = Input.GetVector("moveForward", "moveBackward", "moveLeft", "moveRight");
            localInput.MovementDirection = new Vec2() { X = movementDirection.X, Y = movementDirection.Y };
        }

        foreach (StringName action in InputMap.GetActions())
        {
            if (@event.IsActionPressed(action))
            {
                Global.debugLog("Action Pressed: " + action);
                if (localInput.Actions.ContainsKey(action))
                {
                   localInput.Actions[action] = true;
                }
                else
                {
                    localInput.Actions.Add(action, true);
                }
                InputActionEvent?.Invoke(action, true);
            }
            else if (@event.IsActionReleased(action))
            {
                Global.debugLog("Action Released: " + action);
                if (localInput.Actions.ContainsKey(action))
                {
                    localInput.Actions[action] = false;
                }
                else
                {
                    localInput.Actions.Add(action, false);
                }
                InputActionEvent?.Invoke(action, false);
            }
        }
    }
}
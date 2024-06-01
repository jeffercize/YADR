using Godot;
using NetworkMessages;
using System;
using Action = NetworkMessages.Action;

public partial class InputManager : Node
{
    public float mouseSens = .1f;
    public PlayerInput frameInput = new PlayerInput();

    public override void _PhysicsProcess(double delta)
    {
        if (Global.NetworkManager.client!=null)
        {
            Global.NetworkManager.client.outgoingFramePacket.Inputs.Add(frameInput.Clone());
        }
        
    }

    public override void _Process(double delta)
    {
        
    }

    public override void _Ready()
    {
        SetProcessInput(false);
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
            Vector2 dir = Input.GetVector("moveForward", "moveBackward", "moveLeft", "moveRight");
            frameInput.MovementDirection = new Vec2() { X = dir.X, Y= dir.Y };
        }

    }

    private void checkAction(InputEvent @event, ActionType action)
    {
        string actionName = Enum.GetName(typeof(ActionType), action);

        if (@event.IsAction(actionName))
        {
            if (@event.IsActionPressed(actionName))
            {
                Global.debugLog(actionName + " Pressed.");
                frameInput.Actions.Add(new Action() { ActionType = action, ActionState = ActionState.Pressed });

            }
            else if (@event.IsActionReleased(actionName))
            {
                Global.debugLog(actionName + " Released.");
                frameInput.Actions.Add(new Action() { ActionType = action, ActionState = ActionState.Released });
            }
        }
    }
    private void checkMouse(InputEvent @event)
    {

        if (@event is InputEventMouseMotion mouse && Input.MouseMode==Input.MouseModeEnum.Captured)
        {
            Vector2 mouseDelta = (mouse.Relative * mouseSens);
            frameInput.LookDelta = new Vec2() { X = mouseDelta.X, Y = mouseDelta.Y } ;
        }
    }

    private void checkControllerAxis(InputEvent @event)
    {
        //throw new NotImplementedException();
    }


    
}
using Godot;
using System.Net;

public partial class PlayerInput: Node
{
    public string pid = "";

    public float mouseSens = 0.1f;

    public Vector2 direction = new(0f, 0f);
    public Vector2 lookDelta = new(0f, 0f);

    public bool fire = false;
    public bool jump = false;
    public bool sprint = false;
    public bool aim = false;
    public bool inventory = false;
    public bool menu = false;
    public bool crouch = false;
    public bool prone = false;
    public bool sneak = false;

    public int counter = 0;

    public delegate void input_OpenInventoryEventHandler();
    public static event input_OpenInventoryEventHandler input_OpenInventory = delegate { };

    public delegate void input_jumpEventHandler();
    public static event input_jumpEventHandler input_jump = delegate { };

    public PlayerInput(string pid)
    {
        this.pid = pid;
    }

    public override void _Ready()
    {
        //If this PlayerInput is NOT me (check pid?), then disable input handling
    }

    public override void _Process(double delta)
    {
        lookDelta = Vector2.Zero;

        if (Input.IsActionJustPressed("jump"))
        {
            input_jump.Invoke();
        }

    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            lookDelta = mouseMotion.Relative * mouseSens;
            //lookDelta += Input.GetVector("lookLeft", "lookRight", "lookUp", "lookDown"); Controller Support
        }
        else if (@event.IsActionPressed("OpenInventory"))
        {
            input_OpenInventory.Invoke();
        }
        if (Input.MouseMode == Input.MouseModeEnum.Captured && @event.IsActionPressed("fire"))
        {
            Steam.debugLog(">>>");
            fire = true;
        }
        else if (Input.MouseMode == Input.MouseModeEnum.Captured && @event.IsActionReleased("fire"))
        {
            fire = false;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {


    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event.IsAction("moveForward") || @event.IsAction("moveBackward") || @event.IsAction("moveLeft") || @event.IsAction("moveRight"))
        {
            direction = Input.GetVector("moveForward", "moveBackward", "moveLeft", "moveRight");
            //direction += Input.GetVector(); Controller Support 
        }



        else if (@event.IsActionPressed("scope"))
        {
            aim = true;
        }
        else if (@event.IsActionReleased("scope"))
        {
            aim = false;
        }


        else if (@event.IsActionPressed("sprint"))
        {
            sprint = true;
        }
        else if (@event.IsActionReleased("sprint"))
        {
            sprint = false;
        }
    }


}
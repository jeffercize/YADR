using Godot;
using GodotPlugins.Game;
using Networking;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime;
using System.Transactions;

public partial class Player : Character
{

    public Vector3 newVelocity = Vector3.Zero;
    public Inventory inventory;
    public Equipment equipment;
    public Item heldItem = Item.NONE;
    public Node3D holdPoint;
    RayCast3D pointing;

    public Main.Gamestate lastKnownGamestate;


    //Internal vars
    private Camera3D camera;
    private Vector2 lookDelta;
    public Friend account;
    public bool isMe;
    public gameUI gameUI;
    Vector2 inputDir = Vector2.Zero;



    public PlayerInput input;

    [Export]
    public string steamName;

    //Movement

    public const float jumpSpeed = 7.5f;

    [Export]
    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();


    const float maxSpeed = 5.0f;

    float maxSpeedX = maxSpeed;
    float maxSpeedZ = maxSpeed;


    const float airControlMod = 0.5f;

    float airControlModX = airControlMod;
    float airControlModZ = airControlMod;


    const float accelerationSpeed = 150.0f;

    float accelerationSpeedX = accelerationSpeed;
    float accelerationSpeedZ = accelerationSpeed;



    const float decelerationSpeed = 150.0f;

    float decelerationSpeedZ = decelerationSpeed;
    float decelerationSpeedX = decelerationSpeed;

    //Jumping
    public Vector3 jumpVelocity = new Vector3(0, 5, 0);
    public int jumps = 2;
    public int maxJumps = 2;
    public bool jumping;
    public int coyoteTimer = 0;
    public int coyoteMax = 30;
    public bool jumpMomentumCancel = true;
    public bool canJump = true;
    public bool inAir = false;



    //Camera
    public float fov = 90;
    public float sprintfov = 120;
    public float scopefov = 70;

    public const float cameraSens = .6f;

    public const float negativeVerticalLookLimit = -90; //In degrees
    public const float positiveVerticalLookLimit = 90; //In degrees
    public const float cameraSensX = cameraSens;
    public const float cameraSensY = cameraSens;
    public const float mouseSens = 1f;




    public Player(Friend f)
    {
        account = f;
        isMe = f.IsMe;
    }

    public Player() { }

    public Player(ulong senderSteamID)
    {
        account = new Friend(senderSteamID);
    }

    public void init(Friend f)
    {
        account = f;
        isMe = f.IsMe;
        Name = f.Id.Value.ToString();
        steamName = f.Name;
        Position = new Vector3(0, 0, 0);
    }

    public override void _Ready()
    {

        inventory = GetNode<Inventory>("Inventory");
        equipment = GetNode<Equipment>("Equipment");

        //Every player has an input object, gets data from input if local, or network if remote
        input = new PlayerInput(this.account.Id.ToString());
        input.Name = "InputOf:" + account.Name;
        AddChild(input);

        holdPoint = GetNode<Node3D>("holdPoint");



        if (isMe)
        {
            camera = GetNode<Camera3D>("pov");
            camera.Name = "CameraOf_" + account.Name;
            camera.Current = true;

            gameUI = ResourceLoader.Load<PackedScene>("res://scenes/ui/gameUI.tscn").Instantiate<gameUI>();
            gameUI.Name = "gameUI";
            gameUI.player = this;
            GetNode("/root/main/ui").AddChild(gameUI);

            Global.ui.gameUI = gameUI;

            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        pointing = GetNode<RayCast3D>("CameraOf_Canb/pointing");
        PlayerInput.input_jump += jump;

        EquipSlot.equipMessage += onEquip;
        EquipSlot.unequipMessage += onUnequip;
    }

    private void onUnequip(EquipSlot slot, Item item)
    {
        if (slot.connectedEquipment == equipment)
        {
            if (slot.Name.Equals("weapon1"))
            {
                heldItem = Item.NONE;

            }
        }
    }

    private void onEquip(EquipSlot slot, Item item)
    {
        if (slot.connectedEquipment == equipment)
        {
            if (slot.Name.Equals("weapon1"))
            {
                heldItem = item;
                holdPoint.AddChild(item);
                item.GlobalTransform = holdPoint.GlobalTransform;
            }
        }
    }




    //MOVEMENT SHIT ///////////////////////////////////////////////
    public override void _PhysicsProcess(double delta)
    {
        inputDir = input.direction;

        //Collect our current Input direction (drop the Y piece), and multiply it by our Transform Basis, rotating it so that forward input becomes forward in the direction we are facing
        Vector3 dir = Transform.Basis * new Vector3(inputDir.Y, 0, inputDir.X);

        //Create our desired vector, the direction we're going at max speed
        Vector3 targetVec = new Vector3(dir.X * maxSpeedX, 0, dir.Z * maxSpeedZ);


        //I honestly have no idea what the dot operator does. This uses our acceleration if we're inputing in the direction of motion, otherwise use our deceleration
        if (dir.Dot(newVelocity) > 0)
        {
            //Yeah, I learned ternary operators, what are you gonna do about it.
            newVelocity.X = Mathf.MoveToward(Velocity.X, targetVec.X, accelerationSpeedX * (float)delta * (IsOnFloor() ? 1 : airControlModX));
            newVelocity.Z = Mathf.MoveToward(Velocity.Z, targetVec.Z, accelerationSpeedZ * (float)delta * (IsOnFloor() ? 1 : airControlModZ));
        }
        else
        {
            newVelocity.X = Mathf.MoveToward(Velocity.X, targetVec.X, decelerationSpeedX * (float)delta * (IsOnFloor() ? 1 : airControlModX));
            newVelocity.Z = Mathf.MoveToward(Velocity.Z, targetVec.Z, decelerationSpeedZ * (float)delta * (IsOnFloor() ? 1 : airControlModZ));
        }


        //Lets get the Y component sorted - gravity and jumping
        if (!IsOnFloor())
        {
            if (inAir == false)
            {
                inAir = true;
            }
            //Gravity
            newVelocity.Y -= gravity * (float)delta;

            //If we walked off an edge (not jumping) and we are outside the coyote timer, we lose a jump
            if (jumping == false)
            {
                //Increment timer for how long since we left the ground
                if (coyoteTimer <= coyoteMax)
                {
                    coyoteTimer += 1;
                }

                //Lose a jump if at max
                if (coyoteTimer == coyoteMax)
                {
                    jumps -= 1;
                }

            }
        }
        //On floor, reset everything
        else
        {
            //inAir is a latch to help resolve frame perfect input issues
            if (inAir)
            {
                inAir = false;
                coyoteTimer = 0;
                jumps = maxJumps;
                jumping = false;
            }

        }

        //Upate our vector and shoot it off to the physics engine
        Velocity = newVelocity;
        MoveAndSlide();
        newVelocity = Velocity;
    }


    public void jump()
    {
        if (jumps > 0)
        {
            jumping = true;
            jumps -= 1;

            //nullify the down velocity (Y) if using momentum cancel
            if (jumpMomentumCancel && Velocity.Y < 0)
            {
                newVelocity.Y = 0;
            }

            newVelocity += jumpVelocity;
        }
    }

    //CAMERA SHIT////////////////////////////////////////////////
    public override void _Process(double delta)
    {
        if (isMe)
        {
            //Rotates the camera on X (Up/Down) and clamps so it doesnt go too far.
            camera.Rotation = new Vector3((float)Mathf.Clamp(camera.Rotation.X - input.lookDelta.Y * delta, Mathf.DegToRad(negativeVerticalLookLimit), Mathf.DegToRad(positiveVerticalLookLimit)), 0, 0);
        }

        //Rotates the entire player (camera is child, so it comes along) on Y (left/right)
        Rotation = new Vector3(0, Rotation.Y - input.lookDelta.X * (float)delta, 0);


        Node3D pointed = pointing.GetCollider() as Node3D;
        if (pointed != null)
        {
            if (pointing.GetCollider() is Node3D)
            {
                GetNode<Label>("hud/debugPanel/debugLabel").Text = "Pointing: " + pointed.Name;
            }
            else
            {
                GetNode<Label>("hud/debugPanel/debugLabel").Text = "Pointing: None";
            }
        }
        else
        {
            GetNode<Label>("hud/debugPanel/debugLabel").Text = "Pointing: None";
        }


        if (Input.IsActionJustPressed("interact"))
        {
            Global.debugLog("Interact Pressed");
            //GetNode<Label>("hud/bottomRight/equipped").Text = "Equipped: " + pointed.Name;

            if (pointed is Item item)
            {
                //Item item = (Item)pointed.GetParent();
                Global.debugLog("Picked up Item");
                inventory.autoPlaceItem(item);
                item.GetParent().RemoveChild(item);
            }


        }



        if (heldItem != Item.NONE)
        {
            if (isMe)
            {
                //draw held item on hud
            }

            //draw held item in world
        }


    }
    // OTHER SHIT ///////////////////////////////////////////////

    public void createBind(string newAction, Key key)
    {
        InputMap.AddAction(newAction);
        InputEventKey e = new InputEventKey();
        e.Keycode = key;
        InputMap.ActionAddEvent(newAction, e);
    }

    //Some dumb debug shit I'm going to forget to turn off
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed("DEBUG_capture"))
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        if (Input.IsActionJustPressed("DEBUG_uncapture"))
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        /*if (Input.IsActionJustPressed("DEBUG_cam"))
        {
            if (camera.Current == false)
            {
                camera.Current = true;
                GetNode<Camera3D>("/root/game/debugCam").Current = false;
            }
            else
            {
                camera.Current = false;
                GetNode<Camera3D>("/root/game/debugCam").Current = true;
            }
        }*/
    }

    // override object.Equals
    public override bool Equals(object obj)
    {
        //       
        // See the full list of guidelines at
        //   http://go.microsoft.com/fwlink/?LinkID=85237  
        // and also the guidance for operator== at
        //   http://go.microsoft.com/fwlink/?LinkId=85238
        //

        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        // TODO: write your implementation of Equals() here
        Player other = obj as Player;
        return other.account.Id.Value == account.Id.Value;
    }

    // override object.GetHashCode
    public override int GetHashCode()
    {
        // TODO: write your implementation of GetHashCode() here
        return account.Id.Value.GetHashCode();
    }
}

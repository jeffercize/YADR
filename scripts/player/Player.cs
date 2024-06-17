using Godot;
using ImGuiGodot.Internal;
using ImGuiNET;
using NetworkMessages;
using System;
public partial class Player : Character
{

    public Player() { }
    public Player(ulong clientID) { this.clientID = clientID; }

    public Player(PlayerState playerState)
    {
        this.desiredState = playerState;
    }

    public PlayerState desiredState = new() { ClientID = Global.clientID, PhysObj = new() { Position = new() { X = 0,Y=0,Z=0 } } };

    //Core properties of Player
    public Equipment equipment;
    public Inventory inventory;
    public Health health;
    public ulong clientID = 0;
    public bool spawned = false;
    public bool isMe = false;
    //Movement Vars

    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    const float maxSpeed = 5.0f;
    float maxSpeedX = maxSpeed;
    float maxSpeedZ = maxSpeed;

    const float maxSpeedDefault = 5.0f;
    float maxSpeedDefaultX = maxSpeedDefault;
    float maxSpeedDefaultZ = maxSpeedDefault;

    const float maxSpeedAim = 3.0f;
    float maxSpeedAimX = maxSpeedAim;
    float maxSpeedAimZ = maxSpeedAim;

    const float maxSpeedSprint = 10.0f;
    float maxSpeedSprintX = 5.0f;
    float maxSpeedSprintZ = maxSpeedSprint;

    const float airControlMod = 0.5f;
    float airControlModX = airControlMod;
    float airControlModZ = airControlMod;

    const float accelerationSpeed = 50.0f;
    float accelerationSpeedX = accelerationSpeed;
    float accelerationSpeedZ = accelerationSpeed;

    const float decelerationSpeed = 50.0f;
    float decelerationSpeedZ = decelerationSpeed;
    float decelerationSpeedX = decelerationSpeed;

    //Jumping vars
    public Vector3 jumpVelocity = new Vector3(0, 5, 0);
    public int jumps = 2;
    public int maxJumps = 2;
    public bool jumping;
    public int coyoteTimer = 0;
    public int coyoteMax = 30;
    public bool jumpMomentumCancel = true;
    public bool canJump = true;
    public bool inAir = false;
    public bool jumpButtonReady = true;

    //Camera
    public Node3D pov;
    public Camera3D cam;
    public RayCast3D pointing;
    public float fov = 80;
    public float sprintfov = 90;
    public float scopefov = 60;

    public const float cameraSens = .6f;

    public const float negativeVerticalLookLimit = -90; //In degrees
    public const float positiveVerticalLookLimit = 90; //In degrees
    public const float cameraSensX = cameraSens;
    public const float cameraSensY = cameraSens;
    public const float mouseSens = 1f;


    //Viewmodel stuff
    public Item rightHeldItem = Item.NONE;
    public Item leftHeldItem = Item.NONE;
    public Node3D leftHoldPoint;
    public Vector3 leftHoldPointOriginPos;
    public Node3D rightHoldPoint;
    public Vector3 rightHoldPointOriginPos;


    public Vector3 leftGunTargetPos;
    public float leftGunTargetRecoilRotation;

    public Vector3 leftGunOriginPos;
    public float leftGunOriginRecoilRotation;


    public override void _Ready()
    {
        Global.debugLog("PlayerID: " + clientID + " has arrived on the scenetree.");

        inventory = GetNode<Inventory>("Inventory");
        inventory.spatialParent = this;
        equipment = GetNode<Equipment>("Equipment");
        equipment.connectedCharacter = this;
        pov = GetNode<Node3D>("pov");
        pointing = GetNode<RayCast3D>("pov/pointing");


        leftHoldPoint = GetNode<Node3D>("pov/leftHoldPoint");
        rightHoldPoint = GetNode<Node3D>("pov/rightHoldPoint");
        leftHoldPointOriginPos = leftHoldPoint.Position;
        rightHoldPointOriginPos = rightHoldPoint.Position;

 
        Equipment.equipMessage += onEquip;
        Equipment.unequipMessage += onUnequip;



        Global.debugLog("Testing to see if this player is me. PlayerID: " + clientID + " VS. my globalID: " + Global.clientID);
        if (Global.instance.isMe(clientID))
        {
            isMe = true;
            Global.debugLog("woah this is me");
            cam = new Camera3D();
            pov.AddChild(cam);
            cam.Current = true;
            Global.UIManager.connectToPlayer(this);
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
            Global.InputManager.SetProcessInput(true);
            Global.InputManager.InputActionEvent += OnInputEvent;
        }
    }

    private void OnInputEvent(StringName action, bool pressed)
    {
        switch (action)
        {
            case "Jump":
                if (pressed)
                {
                    if (jumpButtonReady)
                    {
                        onJumpPressed();
                    }
                    jumpButtonReady = false;
                }
                else
                {
                    jumpButtonReady = true;
                }
                break;
            default:
                break;
        }
    }

    private void onUnequip(Equipment equipment, string slotName, Item item)
    {
        throw new NotImplementedException();
    }

    private void onEquip(Equipment equipment, string slotName, Item item)
    {
        throw new NotImplementedException();
    }

    public void onJumpReleased()
    {

    }
    public void onJumpPressed()
    {
        Vector3 newVelocity = Velocity;
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
        Velocity = newVelocity;
    }
    //
    //CAMERA SHIT////////////////////////////////////////////////
    public override void _Process(double delta)
    {
        if (isMe)
        {
            pov.Rotation = new Vector3((float)Mathf.Clamp(pov.Rotation.X - Global.InputManager.mouseDelta.Y * delta, Mathf.DegToRad(negativeVerticalLookLimit), Mathf.DegToRad(positiveVerticalLookLimit)), 0, 0);
            Rotation = new Vector3(0, Rotation.Y - Global.InputManager.mouseDelta.X * (float)delta, 0);
            Global.InputManager.mouseDelta.X = 0;
            Global.InputManager.mouseDelta.Y = 0;
        }

    }



    //MOVEMENT SHIT ///////////////////////////////////////////////
    public override void _PhysicsProcess(double delta)
    {
        Vector3 newVelocity = Velocity;
        Vector3 dir = Vector3.Zero;
        if (isMe)
        {
            //Collect our current Input direction (drop the Y piece), and multiply it by our Transform Basis, rotating it so that forward input becomes forward in the direction we are facing
            dir = Transform.Basis * new Vector3(Global.InputManager.movementDirection.Y * maxSpeedX, 0, Global.InputManager.movementDirection.X * maxSpeedZ);
        }
        else
        {
            dir = PredictInput();
        }
        //Create our desired vector, the direction we're going at max speed
        Vector3 targetVec = new Vector3(dir.X, 0, dir.Z);

        //Set the velocity
        newVelocity.X = targetVec.X;
        newVelocity.Z = targetVec.Z;
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

        Velocity = newVelocity;
        MoveAndSlide();
    }

    private Vector3 PredictInput()
    {
        return Vector3.Zero;
    }

    internal void IterativeSync()
    {
        this.Position = this.Position.Lerp(Global.Vec3ToVector3(desiredState.PhysObj.Position), 0.5f);
        this.Rotation = this.Rotation.Lerp(new Vector3(0, desiredState.PhysObj.Rotation.Y, 0),0.5f);
        this.pov.Rotation = this.pov.Rotation.Lerp(new Vector3(desiredState.PhysObj.Rotation.X, 0, 0),0.5f);

        this.Velocity = this.Velocity.Lerp(Global.Vec3ToVector3(desiredState.PhysObj.LinearVelocity), 0.5f);
    }

    internal PlayerState ToNetworkMessage()
    {
        PlayerState state = new PlayerState(); 
        //Global.debugLog("Packing player non-physics state for local player");
        state.Playerhealth = health?.ToNetworkMessage();
        state.Equipment = equipment?.ToNetworkMessage();
        state.Inventory = inventory?.ToNetworkMessage();
        state.ClientID = clientID;
        PhysicsObject physObj = new PhysicsObject();
        //Global.debugLog("Packing player physics state for local player");
        physObj.Position = new Vec3() { X = Position.X, Y = Position.Y, Z = Position.Z };
        physObj.Rotation = new Vec3() { X = pov.Rotation.X, Y = Rotation.Y, Z = 0 };
     //   physObj.Scale = new Vec3() { X = Scale.X, Y = Scale.Y, Z = Scale.Z };
      //  physObj.LinearVelocity = new Vec3() { X = Velocity.X, Y = Velocity.Y, Z = Velocity.Z };
        state.PhysObj = physObj;
        return state;
    }


    internal void HardSync()
    {
    //    this.inventory= desiredState.inventory;
   //     this.equipment = desiredState.equipment;
    //    this.health = desiredState.health;
    }
}


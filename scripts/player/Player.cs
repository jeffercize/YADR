using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    public partial class Player: Character
    {

    public Player() { }
    public Player(ulong clientID) { this.clientID = clientID; }


    //Core properties of Player
    public Equipment equipment;
    public Inventory inventory;
    public PlayerInputData input;
    public ulong clientID = 0;
    public bool spawned = false;
    public bool isMe = false;
    //Movement Vars
    public Vector3 newVelocity = Vector3.Zero;
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

        InputManager.InputEvent += onInputEvent;
        Equipment.equipMessage += onEquip;
        Equipment.unequipMessage += onUnequip;

        Global.debugLog("Testing to see if this player is me. PlayerID: " + clientID + " VS. my globalID: " + Global.instance.clientID);
        if (Global.instance.isMe(clientID))
        {
            isMe = true;
            Global.debugLog("woah this is me");
            cam = new Camera3D();
            pov.AddChild(cam);
            cam.Current = true;
            Global.UIManager.connectToPlayer(this);
            Input.MouseMode = Input.MouseModeEnum.Captured;
            Global.InputManager.SetProcessInput(true);
        }
        else
        {
            ClientInputHandler.NetworkInputLookDirectionEvent += onNetworkInputLookDirectionEvent;
        }

    }

    private void onNetworkInputLookDirectionEvent(InputLookDirectionMessage message)
    {
        if ((ulong)message.InputOf.SteamID == clientID) { return; }
        pov.Rotation = new Vector3(message.Direction.X, 0, 0);

        //Rotates the entire player (camera is child, so it comes along) on Y (left/right)
        Rotation = new Vector3(0, message.Direction.Y, 0);

    }

    private void onUnequip(Equipment equipment, string slotName, Item item)
    {
        throw new NotImplementedException();
    }

    private void onEquip(Equipment equipment, string slotName, Item item)
    {
        throw new NotImplementedException();
    }

    private void onInputEvent(ulong clientID, ActionMessage actionMessage)
    {
        if (clientID==this.clientID && actionMessage != null)
        {
            if (actionMessage.ActionType == ActionType.Jump && actionMessage.ActionState == ActionState.Pressed)
            {
                onJump();
            }
        }
    }

    public void ForceSyncState(Player state)
    {

    }


    public void onJump()
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
    //
    //CAMERA SHIT////////////////////////////////////////////////
    public override void _Process(double delta)
    {

        if (isMe)
        {
            //Rotates the camera on X (Up/Down) and clamps so it doesnt go too far.
            pov.Rotation = new Vector3((float)Mathf.Clamp(pov.Rotation.X - input.lookDelta.Y * delta, Mathf.DegToRad(negativeVerticalLookLimit), Mathf.DegToRad(positiveVerticalLookLimit)), 0, 0);

            //Rotates the entire player (camera is child, so it comes along) on Y (left/right)
            Rotation = new Vector3(0, Rotation.Y - input.lookDelta.X * (float)delta, 0);
            input.lookDelta = Vector2.Zero;
            ClientInputHandler.CreateAndSendInputLookDirectionMessage(Global.instance.clientID, new Vector3(pov.Rotation.X, Rotation.Y, 0));
        }
        // debugPointer();

        if (leftHeldItem != Item.NONE)
        {
            RayCast3D ray = leftHeldItem.GetNode<RayCast3D>("barrelRay");
            if (ray != null && ray.IsColliding())
            {
                float distance = ray.GetCollisionPoint().DistanceTo(ray.GlobalPosition);
                float target = MathF.Abs(ray.TargetPosition.Y);
                float diff = target - distance;
                Vector3 targetVec = new Vector3(leftHoldPointOriginPos.X, leftHoldPointOriginPos.Y, leftHoldPointOriginPos.Z + diff);
                float targetZ = Mathf.Lerp(leftHoldPoint.Position.Z, targetVec.Z, .3f);
                Vector3 newVec = new Vector3(leftHoldPointOriginPos.X, leftHoldPointOriginPos.Y, targetZ);
                leftHoldPoint.Position = newVec;
            }
            else if (ray != null && !ray.IsColliding())
            {
                float targetZ = Mathf.Lerp(leftHoldPoint.Position.Z, leftHoldPointOriginPos.Z, .05f);
                Vector3 newVec = new Vector3(leftHoldPointOriginPos.X, leftHoldPointOriginPos.Y, targetZ);
                leftHoldPoint.Position = newVec;
            }
        }

        if (rightHeldItem != Item.NONE)
        {
            RayCast3D ray = rightHeldItem.GetNode<RayCast3D>("barrelRay");
            if (ray != null && ray.IsColliding())
            {
                float distance = ray.GetCollisionPoint().DistanceTo(ray.GlobalPosition);
                float target = MathF.Abs(ray.TargetPosition.Y);
                float diff = target - distance;
                Vector3 targetVec = new Vector3(rightHoldPointOriginPos.X, rightHoldPointOriginPos.Y, rightHoldPointOriginPos.Z + diff);
                float targetZ = Mathf.Lerp(rightHoldPoint.Position.Z, targetVec.Z, .3f);
                Vector3 newVec = new Vector3(rightHoldPointOriginPos.X, rightHoldPointOriginPos.Y, targetZ);
                rightHoldPoint.Position = newVec;
            }
            else if (ray != null && !ray.IsColliding())
            {
                float targetZ = Mathf.Lerp(rightHoldPoint.Position.Z, rightHoldPointOriginPos.Z, .05f);
                Vector3 newVec = new Vector3(rightHoldPointOriginPos.X, rightHoldPointOriginPos.Y, targetZ);

                rightHoldPoint.Position = newVec;
            }


        }
    }




    //MOVEMENT SHIT ///////////////////////////////////////////////
    public override void _PhysicsProcess(double delta)
    {
        handleInputDirection(delta);
        handleJumpingAndFalling(delta);
        handleLookingDirection(delta);

        //Upate our vector and shoot it off to the physics engine
        Velocity = newVelocity;
        MoveAndSlide();

        //Velocity is updated by the physics engine at this point, store it to modify next frame.
        newVelocity = Velocity;

        //MP Sync 
        if (Global.NetworkManager.isActive)
        {

        }
    }

    private void handleLookingDirection(double delta)
    {
        pov.Rotation = new Vector3((float)Mathf.Clamp(pov.Rotation.X - input.lookDelta.Y * delta, Mathf.DegToRad(negativeVerticalLookLimit), Mathf.DegToRad(positiveVerticalLookLimit)), 0, 0);
        //Rotates the entire player (camera is child, so it comes along) on Y (left/right)
        Rotation = new Vector3(0, Rotation.Y - input.lookDelta.X * (float)delta, 0);
        input.lookDelta = Vector2.Zero;
    }

    private void handleJumpingAndFalling(double delta)
    {
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
    }

    private void handleInputDirection(double delta)
    {
        //Collect our current Input direction (drop the Y piece), and multiply it by our Transform Basis, rotating it so that forward input becomes forward in the direction we are facing
        Vector3 dir = Transform.Basis * new Vector3(input.direction.Y * maxSpeedX, 0, input.direction.X * maxSpeedZ);

        //Create our desired vector, the direction we're going at max speed
        Vector3 targetVec = new Vector3(dir.X, 0, dir.Z);

        //Set the velocity
        newVelocity.X = targetVec.X;
        newVelocity.Z = targetVec.Z;
    }
}


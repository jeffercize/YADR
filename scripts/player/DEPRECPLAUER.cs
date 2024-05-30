using Godot;
using System;

[GlobalClass]
public partial class DEPRECPLAUER : Character
{

    public Vector3 newVelocity = Vector3.Zero;


    public Inventory inventory;
    public Equipment equipment;


    public Item rightHeldItem = Item.NONE;
    public Item leftHeldItem = Item.NONE;
    public Node3D leftHoldPoint;
    public Vector3 leftHoldPointOriginPos;
    public Node3D rightHoldPoint;
    public Vector3 rightHoldPointOriginPos;
    RayCast3D pointing;



    //Internal vars
    private Camera3D camera;
    public PlayerInputData input;

    //Movement

    public const float jumpSpeed = 7.5f;

    public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    const float maxSpeed = 5.0f;

    float maxSpeedX = maxSpeed;
    float maxSpeedZ = maxSpeed;

    const float maxSpeedDefault = 5.0f;

    float maxSpeedDefaultX = maxSpeedDefault;
    float maxSpeedDefaultZ = maxSpeedDefault;

    const float maxSpeedAim = 3.0f;

    [Export]
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
    public float fov = 80;
    public float sprintfov = 90;
    public float scopefov = 60;

    public const float cameraSens = .6f;

    public const float negativeVerticalLookLimit = -90; //In degrees
    public const float positiveVerticalLookLimit = 90; //In degrees
    public const float cameraSensX = cameraSens;
    public const float cameraSensY = cameraSens;
    public const float mouseSens = 1f;




    public Vector3 leftGunTargetPos;
    public float leftGunTargetRecoilRotation;

    public Vector3 leftGunOriginPos;
    public float leftGunOriginRecoilRotation;





    public void init()
    {


        Position = new Vector3(0, 0, 0);
    }

    public override void _Ready()
    {

        inventory = GetNode<Inventory>("Inventory");
        inventory.spatialParent = this;
        equipment = GetNode<Equipment>("Equipment");
        equipment.connectedCharacter = this;

        leftHoldPoint = GetNode<Node3D>("pov/leftHoldPoint");
        rightHoldPoint = GetNode<Node3D>("pov/rightHoldPoint");
        leftHoldPointOriginPos = leftHoldPoint.Position;
        rightHoldPointOriginPos = rightHoldPoint.Position;

        camera = GetNode<Camera3D>("pov");
        //camera.Name = "CameraOf_" + account.Name;
        camera.Current = true;




        Input.MouseMode = Input.MouseModeEnum.Captured;


        pointing = GetNode<RayCast3D>("pov/pointing");



        Equipment.equipMessage += onEquip;
        Equipment.unequipMessage += onUnequip;



    }








    //MOVEMENT SHIT ///////////////////////////////////////////////
    public override void _PhysicsProcess(double delta)
    {

        //Collect our current Input direction (drop the Y piece), and multiply it by our Transform Basis, rotating it so that forward input becomes forward in the direction we are facing
        Vector3 dir = Transform.Basis * new Vector3(input.direction.Y * maxSpeedX, 0, input.direction.X * maxSpeedZ);

        //Create our desired vector, the direction we're going at max speed
        Vector3 targetVec = new Vector3(dir.X, 0, dir.Z);

        newVelocity.X = targetVec.X;
        newVelocity.Z = targetVec.Z;
        /*
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
        */

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




    //CAMERA SHIT////////////////////////////////////////////////
    public override void _Process(double delta)
    {

        //Rotates the camera on X (Up/Down) and clamps so it doesnt go too far.
        camera.Rotation = new Vector3((float)Mathf.Clamp(camera.Rotation.X - input.lookDelta.Y * delta, Mathf.DegToRad(negativeVerticalLookLimit), Mathf.DegToRad(positiveVerticalLookLimit)), 0, 0);


        //Rotates the entire player (camera is child, so it comes along) on Y (left/right)
        Rotation = new Vector3(0, Rotation.Y - input.lookDelta.X * (float)delta, 0);
        input.lookDelta = Vector2.Zero;
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


        /*
        if ()
        {
            camera.Fov = Mathf.Lerp(camera.Fov, scopefov, 0.3f);
            maxSpeedZ = maxSpeedAimZ;
            maxSpeedX = maxSpeedAimX;
        }
        else if (input.inputData.sprint)
        {
            camera.Fov = Mathf.Lerp(camera.Fov, sprintfov, 0.3f);
            maxSpeedZ = maxSpeedSprintZ;
            maxSpeedX = maxSpeedSprintX;
        }
        else
        {
            camera.Fov = Mathf.Lerp(camera.Fov, fov, 0.3f);
            maxSpeedZ = maxSpeedDefaultZ;
            maxSpeedX = maxSpeedDefaultX;
        }
        if (fireCounter > 0)
        {
            fireCounter -= delta;
        }
        */
        if (rightHeldItem is Gun gun)
        {
            if (pointing.IsColliding())
            {
                gun.RotationDegrees = gun.RotationDegrees.Lerp(gun.RotationDegrees.Rotated(Vector3.Up, gun.GetNode<Node3D>("barrel").Position.AngleTo(pointing.GetCollisionPoint())), .15f);

            }
            else
            {
                gun.RotationDegrees = rightHoldPoint.RotationDegrees;
            }


        }

    }

    private void debugPointer()
    {
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
    }




    // Input Event Handlers

    private void onFireStop()
    {

    }

    private void onFire()
    {

    }

    private void onInteract()
    {
        Node3D pointed = pointing.GetCollider() as Node3D;
        if (pointed is Item item)
        {
            //Item item = (Item)pointed.GetParent();
            Global.debugLog("Picked up Item");
            inventory.autoPlaceItem(item);
            item.GetParent().RemoveChild(item);
        }
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

    //Other event handlers

    private void onUnequip(Equipment equipment, string slotName, Item item)
    {
        if (this.equipment == equipment)
        {
            Global.debugLog("My equipment has a change - UNEQUIP");
            if (slotName.Equals("leftHand"))
            {
                item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                item.Freeze = false;
                item.CollisionLayer = 1;
                Global.debugLog("My equipment has a change - UNEQUIP LEFT HAND");
                leftHeldItem = Item.NONE;
                foreach (Node child in leftHoldPoint.GetChildren())
                {
                    if (!child.Name.Equals("leftArm"))
                    {
                        leftHoldPoint.RemoveChild(child);
                    }

                }

            }
            else if (slotName.Equals("rightHand"))
            {
                Global.debugLog("My equipment has a change - UNEQUIP RIGHT HAND");
                item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                item.Freeze = false;
                item.CollisionLayer = 1;
                rightHeldItem = Item.NONE;
                foreach (Node child in rightHoldPoint.GetChildren())
                {
                    if (!child.Name.Equals("rightArm"))
                    {
                        rightHoldPoint.RemoveChild(child);
                    }

                }

            }
        }
    }

    private void onEquip(Equipment equipment, string slotName, Item item)
    {
        if (this.equipment == equipment)
        {
            Global.debugLog("My equipment has a change - EQUIP");
            if (slotName.Equals("leftHand"))
            {
                leftHeldItem = item;
                Global.debugLog("My equipment has a change - EQUIP LEFT HAND");
                leftHoldPoint.AddChild(item);
                item.GlobalTransform = leftHoldPoint.GlobalTransform;
                item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                item.Freeze = true;
                item.CollisionLayer = 0;


            }
            else if (slotName.Equals("rightHand"))
            {
                rightHeldItem = item;
                Global.debugLog("My equipment has a change - EQUIP RIGHT HAND");

                rightHoldPoint.AddChild(item);

                item.GlobalTransform = rightHoldPoint.GlobalTransform;
                item.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                item.Freeze = true;
                item.CollisionLayer = 0;

            }
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
    }


}

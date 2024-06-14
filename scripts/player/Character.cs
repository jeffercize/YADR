using Godot;
using NetworkMessages;

public partial class Character : CharacterBody3D
{
    public ulong entityID { get; set; }

    public void AssignEntityID()
    {

    }

    public PhysicsObject GetState()
    {
        PhysicsObject state = new PhysicsObject();
        state.Position = new Vec3() {  X = GlobalPosition.X, Y = GlobalPosition.Y, Z = GlobalPosition.Z };
        state.Rotation = new Vec3() { X = GlobalRotation.X, Y = GlobalRotation.Y, Z = GlobalRotation.Z };
        state.Scale = new Vec3() { X = Scale.X, Y = Scale.Y, Z = Scale.Z };
        state.LinearVelocity = new Vec3() { X = Velocity.X, Y = Velocity.Y, Z = Velocity.Z };
        state.AngularVelocity = new Vec3() { X = 0, Y = 0, Z = 0 };

        return state;
    }

    public bool LerpToState(Vec3 position, Vec3 rotation, Vec3 scale, Vec3 linearVelocity, Vec3 angularVelocity)
    {
        Vector3 pos = new Vector3(position.X, position.Y, position.Z);
        pos.Lerp(GlobalPosition, 0.5f);
        this.GlobalPosition = pos;

        Vector3 rot = new Vector3(rotation.X, rotation.Y, rotation.Z);
        rot.Lerp(GlobalRotation, 0.5f);
        this.GlobalRotation = rot;

        Vector3 scl = new Vector3(scale.X, scale.Y, scale.Z);
        scl.Lerp(Scale, 0.5f);
        this.Scale = scl;

        Vector3 linVel = new Vector3(linearVelocity.X, linearVelocity.Y, linearVelocity.Z);
        linVel.Lerp(Velocity, 0.5f);
        this.Velocity = linVel;

        Vector3 angVel = new Vector3(angularVelocity.X, angularVelocity.Y, angularVelocity.Z);
       // this.angu = angVel;

        return true;
    }
}
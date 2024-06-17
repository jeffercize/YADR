using Godot;
using Google.Protobuf;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class  WorldObject : RigidBody3D
{
    public ulong entityID { get; set; }
    public ulong ownerID { get; set; }
    public ulong type { get; set; }

    public WorldObject desiredState { get; set; }

    public WorldObject(GameObject obj)
    {
        FromNetworkMessage(obj);    
    }

    public void FromNetworkMessage(GameObject message)
    {
        this.entityID = message.Id;
        this.ownerID = message.Owner;
        this.type = message.Type;
        this.GlobalPosition = new Vector3(message.PhysObj.Position.X, message.PhysObj.Position.Y, message.PhysObj.Position.Z);
        this.GlobalRotation = new Vector3(message.PhysObj.Rotation.X, message.PhysObj.Rotation.Y, message.PhysObj.Rotation.Z);
        this.Scale = new Vector3(message.PhysObj.Scale.X, message.PhysObj.Scale.Y, message.PhysObj.Scale.Z);
        this.LinearVelocity = new Vector3(message.PhysObj.LinearVelocity.X, message.PhysObj.LinearVelocity.Y, message.PhysObj.LinearVelocity.Z);
        this.AngularVelocity = new Vector3(message.PhysObj.AngularVelocity.X, message.PhysObj.AngularVelocity.Y, message.PhysObj.AngularVelocity.Z);
    }

    public GameObject ToNetworkMessage()
    {
        GameObject message = new GameObject();
        message.Id = this.entityID;
        message.Owner = this.ownerID;
        message.Type = this.type;
        message.PhysObj = new PhysicsObject();
        message.PhysObj.Position = new Vec3() { X = GlobalPosition.X, Y = GlobalPosition.Y, Z = GlobalPosition.Z };
        message.PhysObj.Rotation = new Vec3() { X = GlobalRotation.X, Y = GlobalRotation.Y, Z = GlobalRotation.Z };
        message.PhysObj.Scale = new Vec3() { X = Scale.X, Y = Scale.Y, Z = Scale.Z };
        message.PhysObj.LinearVelocity = new Vec3() { X = LinearVelocity.X, Y = LinearVelocity.Y, Z = LinearVelocity.Z };
        message.PhysObj.AngularVelocity = new Vec3() { X = AngularVelocity.X, Y = AngularVelocity.Y, Z = AngularVelocity.Z };
        return message;
    }

    public void IterativeSync(WorldObject desired)
    {
        this.GlobalPosition = this.GlobalPosition.Slerp(desired.GlobalPosition, 0.5f);
        this.GlobalRotation = this.GlobalRotation.Slerp(desired.GlobalRotation, 0.5f);
        this.Scale = this.Scale.Slerp(desired.Scale, 0.5f);
        this.LinearVelocity = this.LinearVelocity.Slerp(desired.LinearVelocity, 0.5f);
        this.AngularVelocity = this.AngularVelocity.Slerp(desired.AngularVelocity, 0.5f);
    }

    public void IterativeSync()
    {
        IterativeSync(desiredState);
    }

    public void HardSync(WorldObject desired)
    {
        this.GlobalPosition = desired.GlobalPosition;
        this.GlobalRotation = desired.GlobalRotation;
        this.Scale = desired.Scale;
        this.LinearVelocity = desired.LinearVelocity;
        this.AngularVelocity = desired.AngularVelocity;
    }

    public void HardSync()
    {
        HardSync(desiredState);
    }


}




using Godot;
using NetworkMessages;
using System.Numerics;

public partial interface INetworkableWorldObject : INetworkableEntity
{


    public bool LerpToState(Vec3 position, Vec3 rotation, Vec3 scale, Vec3 linearVelocity, Vec3 angularVelocity);

    public PhysicsObject GetState();

}


using Godot;
using Godot.Collections;

[GlobalClass]
public partial class Gun : Weapon
{
    [Export]
    public float shotCooldown;

    [Export]
    public int damage;

    public float range = 9999;

    public override void _Process(double delta)
    {

    }

    public void fire()
    {

        //Global.debugLog("bang");
        PhysicsDirectSpaceState3D phys = GetWorld3D().DirectSpaceState;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(this.GetNode<Marker3D>("barrel").GlobalPosition, this.GetNode<Marker3D>("barrel").ToGlobal(this.GetNode<Marker3D>("barrel").Position + new Vector3(0, 0, -range)));
        query.HitFromInside = true;
        query.Exclude = new Array<Rid> { this.GetRid(), Global.UIManager.player.GetRid() };
        Dictionary result = phys.IntersectRay(query);
        if (result.TryGetValue("position", out Variant pos))
        {
            Global.debugLog(pos.ToString());
        }
        else
        {
            Global.debugLog("NONE");
        }
        if (result.TryGetValue("collider", out Variant value))
        {
            if (value.AsGodotObject() is Node3D node)
            {
                Global.debugLog(node.Name);
                GpuParticles3D spark = ResourceLoader.Load<PackedScene>("res://scenes/bulletImpact.tscn").Instantiate<GpuParticles3D>();
                AddChild(spark);
                spark.GlobalPosition = result["position"].AsVector3();
                spark.Emitting = true;
            }
        }





    }
}

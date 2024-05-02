using Godot;
using System;

[GlobalClass]
public partial class Gun : Weapon
{
    [Export]
    public int shotCooldown;

    [Export]
    public int damage;

    public int getFireratePerSecond()
    {
        return 60 / shotCooldown;
    }

    public int getFireratePerMinute()
    {
        return getFireratePerSecond()*60;
    }

    public void fire()
    {
        Global.debugLog("bang");
    }
}

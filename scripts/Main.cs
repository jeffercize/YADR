using Godot;
using System;
using Steamworks;

/// <summary>
/// This is the first node loaded after autoloads+engine stuff. Gets the ball rolling.
/// </summary>
public partial class Main : Node
{



    public override void _Ready()
    {

        Global.UIManager.AddChild(ResourceLoader.Load<PackedScene>("res://scenes/ui/MainMenu.tscn").Instantiate());
        // If Steam is running and we get connected, then we're using Steam. Otherwise, bail use an offline fallback.
        if (Global.SteamManager.steamRunning)
        {
            Global.instance.platform = Global.PLATFORM.STEAM;
            Global.instance.clientID = (ulong)SteamUser.GetSteamID();
            Global.instance.clientName = SteamFriends.GetPersonaName();
        }


      

    }





    //Global inputs
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("quit")) { GetTree().Quit(); }
    }

}

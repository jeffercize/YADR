using Godot;
using System;
using Steamworks;

/// <summary>
/// This is the first node loaded after autoloads+engine stuff. Gets the ball rolling.
/// Largely deprecated now, with the Global autoload taking on most of the work.
/// Still, this node will be on the SceneTree at all times:
/// 
/// root
///     SteamManager
///     Global
///     main
///         other managers
///         etc
///         
/// </summary>
public partial class Main : Node
{

    public override void _Ready()
    {

        Global.UIManager.AddChild(ResourceLoader.Load<PackedScene>("res://scenes/ui/MainMenu.tscn").Instantiate());
        // If Steam is running and we get connected, then we're using Steam. Otherwise, bail use an offline fallback.
        //TODO: See SteamManager - steam state mangement needs fixed
        if (Global.SteamManager.steamRunning)
        {
            Global.debugLog("Setting Steam Vars");
            Global.instance.platform = Global.PLATFORM.STEAM;
            Global.instance.clientID = (ulong)SteamUser.GetSteamID();
            Global.instance.clientName = SteamFriends.GetPersonaName();
        }

    }
}
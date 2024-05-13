using Godot;
using Steamworks;
using System;
using System.Diagnostics;

public partial class SteamManager : Node
{
    public static SteamManager instance;
    public AppId_t AppID = (AppId_t)480;
    public bool steamRunning = false;
    protected Callback<SteamRelayNetworkStatus_t> SteamRelayNetworkStatusChange;

    public override void _Ready()
    {
        instance = this;
        SteamRelayNetworkStatusChange = Callback<SteamRelayNetworkStatus_t>.Create(OnSteamRelayNetworkStatus);

        init();
    }

    private void OnSteamRelayNetworkStatus(SteamRelayNetworkStatus_t param)
    {

    }

    public override void _Process(double delta)
    {
        if (steamRunning)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public void init()
    {
        try
        {
            if (AppID!= (AppId_t)480 && SteamAPI.RestartAppIfNecessary(AppID))
            {
                GetTree().Quit();
            }
        }
        catch (System.DllNotFoundException e)
        {
            Global.debugLog("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + e);
            GetTree().Quit();
            return;
        }
        steamRunning = SteamAPI.Init();
        if (!steamRunning)
        {
            Global.debugLog("Steam init failed.");
        }
        SteamNetworkingUtils.InitRelayNetworkAccess();
        debugLog("Steam connection complete.");
    }
    public static void debugLog(string msg)
    {
        Global.debugLog("[STEAM] " + msg);
    }
    public override void _ExitTree()
    {
        SteamAPI.Shutdown();
    }
}
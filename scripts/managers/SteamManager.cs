using Godot;
using Steamworks;

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
        //????
    }

    public override void _Process(double delta)
    {
        if (steamRunning)
        {
            //Check for the underlying SteamAPI sending out events once per frame.
            SteamAPI.RunCallbacks();
        }
    }

    public void init()
    {
        //Basic Steam DRM check, also as a byproduct checks that the SteamAPI DLL is correct.
        try
        {
            //If steam isn't running, close the game, launch steam, then relaunch the game thru steam.
            //This is the most DRM we're going to do
            //This entire mechanism is disabled if AppID is currently set to 480 (SpaceWar)
            if (AppID != (AppId_t)480 && SteamAPI.RestartAppIfNecessary(AppID))
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

        //At this point the SteamAPI DLL is loaded, try to connect to Steam
        steamRunning = SteamAPI.Init();
        if (!steamRunning)
        {
            Global.debugLog("Steam init failed.");
        }

        //This just preps the underlying system for networking, which saves time later. It doesnt do much on it's own.
        SteamNetworkingUtils.InitRelayNetworkAccess();

        SteamDebugLog("Steamworks connection complete.");
    }
    public static void SteamDebugLog(string msg)
    {
        Global.debugLog("[STEAM] " + msg);
    }

    public static SteamNetworkingIdentity GetSelfIdentity()
    {
        SteamNetworkingIdentity id = new SteamNetworkingIdentity();
        SteamNetworkingSockets.GetIdentity(out id);
        return id;
    }

    //In theory this should trigger whenever SteamManager gets removed from the SceneTree, which, in theory, should only happen when the game closes.
    public override void _ExitTree()
    {
        //Gracefully disconnect from steamworks
        SteamAPI.Shutdown();
    }
}
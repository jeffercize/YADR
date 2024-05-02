using Godot;
using System;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using Godot.Collections;

/// <summary>
/// Autoloaded Singleton, gets loaded before anything else by engine. Steam needs to init here to hook everything correctly.
/// Also has some utility functions and DRM would go here
/// </summary>
public partial class Steam : Node
{
    //Configure me!
    static readonly AppId appId = 480;
    static readonly bool enableLogging = true;
    public Friend self;
    public Dictionary<ulong, Godot.Image> avatars;

    public override void _Ready()
    {
        initSteam();
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }
    public void initSteam()
    {
        try
        {
            SteamClient.Init(appId);
        }
        catch (Exception e)
        {
            debugLog(e.Message);
        }
        self = new Friend(SteamClient.SteamId);
    }
    public static void debugLog(string msg)
    {
        if (enableLogging)
        {
            GD.Print("[STEAM-DEBUG] " + msg);
        }
    }

    public Friend getSelf()
    {
        return self;
    }
    private async Task<Steamworks.Data.Image?> GetSteamAvatar(SteamId steamId)
    {
        try
        {
            // Get Avatar using await
            return await SteamFriends.GetLargeAvatarAsync(steamId);
        }
        catch (Exception e)
        {
            // If something goes wrong, log it
            debugLog(e.Message);
            return null;
        }
    }

    public async Task<Godot.Image> getAvatar(SteamId steamId)
    {
        var avatar = GetSteamAvatar(steamId);
        await Task.WhenAll(avatar);
        return Godot.Image.CreateFromData((int)avatar.Result.Value.Width, (int)avatar.Result.Value.Height, false, Godot.Image.Format.Rgba8, avatar.Result.Value.Data);
    }
}

using Godot;
using Steamworks;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public partial class Global : Node
{
    public static Global instance;

    /// <summary>
    ///  Stores a reference to the UIManager node, grabbed from the main scene when Global is autoloaded. All UI is handled thru this node, and all UI should appear as a child of this node.
    /// </summary>
    public static UIManager UIManager;

    /// <summary>
    ///  Stores a reference to the LevelManager node, grabbed from the main scene when Global is autoloaded. The 3D world is handled thru this node, and all 3D objects should appear as a child of this node.
    /// </summary>
    public static LevelManager LevelManager;

    /// <summary>
    ///  Stores a reference to the NetworkManager node, grabbed from the main scene when Global is autoloaded. All networking is handled thru this node. NOTE: THIS INCLUDES SINGLEPLAYER (virtual server).
    /// </summary>
    public static NetworkManager NetworkManager;

    /// <summary>
    ///  Stores a reference to the InputManager node, grabbed from the main scene when Global is autoloaded.
    /// </summary>
    public static InputManager InputManager;

    /// <summary>
    ///  Stores a reference to the AudioManager node, grabbed from the main scene when Global is autoloaded
    /// </summary>
    public static AudioManager AudioManager;

    /// <summary>
    ///  Stores a reference to the AudioManager node, grabbed from the main scene when Global is autoloaded
    /// </summary>
    public static SteamManager SteamManager;

    /// <summary>
    ///  Stores a reference to the AudioManager node, grabbed from the main scene when Global is autoloaded
    /// </summary>
    public static PlayerManager PlayerManager;
    /// <summary>
    /// If true, print debug logs to console during runtime.
    /// </summary>
    static readonly public bool enableLogging = true;

    /// <summary>
    /// Enum listing valid platforms
    /// </summary>
    public enum PLATFORM
    {
        NONE,
        STEAM,
        ITCH,
        EPIC,
        OTHER
    }

    /// <summary>
    /// What platform this game instance is currently using. Set during Global autoload.
    /// </summary>
    public PLATFORM platform = PLATFORM.NONE;


    /// <summary>
    /// A numerical ID for the client. Should be unique.
    /// </summary>
    public ulong clientID;

    /// <summary>
    /// Text name for this client. Probably breaks if using esoteric fonts, weird unicode stuff, or right to left languages.
    /// </summary>
    public string clientName;


    // This is technically the first non-engine code that is ran in the entire game. The Steam singleton onready goes first but it shouldnt do anything.
    public override void _Ready()
	{
        //Grab instances of our nodes from the main scene tree.
        instance = this;
		UIManager = GetNode<UIManager>("../main/UIManager");
		LevelManager = GetNode<LevelManager>("../main/LevelManager");
        NetworkManager = GetNode<NetworkManager>("../main/NetworkManager");
        AudioManager = GetNode<AudioManager>("../main/AudioManager");
        InputManager = GetNode<InputManager>("../main/InputManager");
        SteamManager = GetNode<SteamManager>("../SteamManager");
        PlayerManager = GetNode<PlayerManager>("../main/PlayerManager");
        
    }

    public static void debugLog(string msg)
    {
        if (enableLogging)
        {
            GD.Print("[DEBUG] " + msg);
        }
    }

    /// <summary>
    /// DEPRECATED - preprotobuff
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static byte[] StructureToByteArray(object obj)
    {
        int len = Marshal.SizeOf(obj);

        byte[] arr = new byte[len];

        IntPtr ptr = Marshal.AllocHGlobal(len);

        Marshal.StructureToPtr(obj, ptr, true);

        Marshal.Copy(ptr, arr, 0, len);

        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    /// <summary>
    /// DEPRECATED - preprotobuff
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytearray"></param>
    /// <param name="obj"></param>
    public static void ByteArrayToStructure<T>(byte[] bytearray, T obj)
    {
        int len = Marshal.SizeOf(obj);

        IntPtr i = Marshal.AllocHGlobal(len);

        Marshal.Copy(bytearray, 0, i, len);

        obj = Marshal.PtrToStructure<T>(i);

        Marshal.FreeHGlobal(i);
    }

    /// <summary>
    /// DEPRECATED - preprotobuff
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="str"></param>
    /// <returns></returns>
    public static byte[] getBytes<T>(T str)
    {
        int size = Marshal.SizeOf(str);
        byte[] arr = new byte[size];
        IntPtr ptr = IntPtr.Zero;
        ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(str, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);
        return arr;
    }

    /// <summary>
    /// DEPRECATED - preprotobuff
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static T fromBytes<T>(byte[] bytes)
    {
        T str;
        int size = 1;
        IntPtr ptr= IntPtr.Zero;
        ptr = Marshal.AllocHGlobal(size);
        Marshal.Copy(bytes, 0, ptr, size);
        return default;
    }

}

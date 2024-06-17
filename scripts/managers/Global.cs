using Godot;
using NetworkMessages;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class Global : Node
{
    public static Global instance;

    /// <summary>
    /// Stores a reference to the UIManager node, grabbed from the main scene when Global is autoloaded. All UI is handled thru this node, and all UI should appear as a child of this node.
    /// </summary>
    public static UIManager UIManager;



    /// <summary>
    /// Stores a reference to the NetworkManager node, grabbed from the main scene when Global is autoloaded. All networking is handled thru this node. NOTE: THIS INCLUDES SINGLEPLAYER (virtual server).
    /// </summary>
    public static NetworkPeer NetworkPeer;

    /// <summary>
    /// Stores a reference to the InputManager node, grabbed from the main scene when Global is autoloaded.
    /// </summary>
    public static InputManager InputManager;


    /// <summary>
    /// Stores a reference to the AudioManager node, grabbed from the main scene when Global is autoloaded
    /// </summary>
    public static AudioManager AudioManager;

    /// <summary>
    /// Stores a reference to the AudioManager node, grabbed from the main scene when Global is autoloaded
    /// </summary>
    public static SteamManager SteamManager;

    public static WorldSim WorldSim;

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
    public static ulong clientID;

    /// <summary>
    /// Text name for this client. Probably breaks if using esoteric fonts, weird unicode stuff, or right to left languages.
    /// </summary>
    public static string clientName;

    // This is technically the first non-engine code that is ran in the entire game. The Steam singleton onready goes first but it shouldnt do anything.
    public override void _Ready()
    {
        //Grab instances of our nodes from the main scene tree.
        instance = this;
        UIManager = GetNode<UIManager>("../main/UIManager");
        NetworkPeer = GetNode<NetworkPeer>("../main/NetworkPeer");
        AudioManager = GetNode<AudioManager>("../main/AudioManager");
        InputManager = GetNode<InputManager>("../main/InputManager");
        SteamManager = GetNode<SteamManager>("../SteamManager");

        Global.debugLog("About to make WorldSim");
        Global.WorldSim = new WorldSim();
        Global.debugLog("Made WorldSim");

    }

    /// <summary>
    /// Prints a debug log message to the console during runtime if enableLogging is true.
    /// </summary>
    /// <param name="msg">The debug log message.</param>
    public static void debugLog(string msg)
    {
        if (enableLogging)
        {
            GD.Print("[DEBUG] " + msg);
        }   


    }

    /// <summary>
    /// Starts the game by creating a new instance of the WorldSim node and adding it as a child.
    /// </summary>
    public void StartGame(string command, ulong id)
    {

    }

    public static Vector3 Vec3ToVector3(Vec3 vec)
    {
        return new Vector3(vec.X, vec.Y, vec.Z);
    }

    public static Vec3 Vector3ToVec3(Vector3 vec)
    {
        return new Vec3() { X = vec.X, Y = vec.Y, Z = vec.Z };
    }

    public static ulong getTick()
    {
        return 0;
    }

    /// <summary>
    /// Converts a structure object to a byte array.
    /// </summary>
    /// <param name="obj">The structure object to convert.</param>
    /// <returns>The byte array representation of the structure object.</returns>
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
    /// Converts a byte array to a structure object.
    /// </summary>
    /// <typeparam name="T">The type of the structure object.</typeparam>
    /// <param name="bytearray">The byte array to convert.</param>
    /// <param name="obj">The structure object to populate with the converted data.</param>
    public static void ByteArrayToStructure<T>(byte[] bytearray, T obj)
    {
        int len = Marshal.SizeOf(obj);

        IntPtr i = Marshal.AllocHGlobal(len);

        Marshal.Copy(bytearray, 0, i, len);

        obj = Marshal.PtrToStructure<T>(i);

        Marshal.FreeHGlobal(i);
    }

    /// <summary>
    /// Converts a structure object to a byte array.
    /// </summary>
    /// <typeparam name="T">The type of the structure object.</typeparam>
    /// <param name="str">The structure object to convert.</param>
    /// <returns>The byte array representation of the structure object.</returns>
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
    /// Converts a byte array to a structure object.
    /// </summary>
    /// <typeparam name="T">The type of the structure object.</typeparam>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>The structure object populated with the converted data.</returns>
    public static T fromBytes<T>(byte[] bytes)
    {
        T str;
        int size = 1;
        IntPtr ptr = IntPtr.Zero;
        ptr = Marshal.AllocHGlobal(size);
        Marshal.Copy(bytes, 0, ptr, size);
        return default;
    }

    /// <summary>
    /// Checks if the given client ID matches the client ID of this instance.
    /// </summary>
    /// <param name="clientID">The client ID to compare.</param>
    /// <returns>True if the client ID matches, false otherwise.</returns>
    internal bool isMe(ulong clientID)
    {
        return clientID == Global.clientID;
    }


}

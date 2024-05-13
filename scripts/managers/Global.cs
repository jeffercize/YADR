using Godot;
using Steamworks;
using System;
using System.Diagnostics;

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
    public string clientID;

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







       
	}

    public static void debugLog(string msg)
    {
        if (enableLogging)
        {
            GD.Print("[DEBUG] " + msg);
        }
    }


}

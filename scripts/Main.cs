using Godot;
using Steamworks;
using Networking;
using static Networking.SteamNetwork;
using System;

/// <summary>
/// This is the first node loaded after autoloads+engine stuff. Gets the ball rolling.
/// </summary>
public partial class Main : Node
{
    public CanvasLayer uiLayer;
    public SteamLobby lobby;
    public SteamNetwork network;
    public Node players;
    public Node3D level;
    public ulong steamID;

    /// <summary>
    /// One byte (63) gamestate enum
    /// </summary>
    public enum Gamestate
    {
        ERROR = 0,
        PREGAME = 1,
        LOBBY = 2,
        LOBBYSTART = 20,
        LOADING = 3,
        LOADWAITING = 30,
        GAME = 5,
        ENDSCREEN = 6,
    }

    public override void _Ready()
    {

        
        //Set the internal node vars to match main.tscn
        uiLayer = GetNode<CanvasLayer>("ui");
        level = GetNode<Node3D>("level");
        players = GetNode("players");

        //Launch a Steam lobby behind the scenes, this creates a lobby and puts the player in it
        lobby = new SteamLobby();
        lobby.Name = "SteamLobby";
        AddChild(lobby);

        //Start setting up the network, we need this even if we are offline
        network = new SteamNetwork();
        network.Name = "SteamNetwork";
        AddChild(network);


        SteamConnectionManager.gamestateMessage += onGamestateMessage;

        //Load Mainmenu
        clearUI();
        uiLayer.AddChild(loadUIFromName("MainMenu.tscn"));
    }



    //Global inputs
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("quit")) { GetTree().Quit(); }
    }




    private void onGamestateMessage(ulong senderSteamID, GamestateMsg msg)
    {
        Player tempPlayer;
        switch (msg.state)
        {
            
            case Gamestate.ERROR:
                break;
            
            case Gamestate.PREGAME:
                break;
            
            case Gamestate.LOBBYSTART:
                
                Steam.debugLog("Host started game.");
                clearUI();
                clearLevel();
                
                //TODO: Show Loading screen
                
                foreach (Friend f in lobby.lobbyMembers)
                {

                    tempPlayer = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
                    tempPlayer.init(f);
                    //players.AddChild(tempPlayer);
                    tempPlayer.Position = new Vector3(500, 25, 500);


                    Node3D car = ResourceLoader.Load<PackedScene>("res://import/vehicles/car_base.tscn").Instantiate<Node3D>();
                    //Global.level.AddChild(car);
                    //car.Position = new Vector3(500, 25, 500);
                }


                //TODO: load startarea
                //Global.level.AddChild(ResourceLoader.Load<PackedScene>("res://scenes/proc.tscn").Instantiate());
                Global.level.AddChild(ResourceLoader.Load<PackedScene>("res://scenes/TerrainGeneration.tscn").Instantiate());
                //Tell everyone I'm done loading
                network.sendMessage(MessageType.Gamestate, steamID, new GamestateMsg(Gamestate.LOADWAITING));
                break;
            
            case Gamestate.LOADWAITING:
                Steam.debugLog("Player: " + senderSteamID + " has finished loading!");
                if ( players.FindChild(senderSteamID.ToString()) is Player p )
                {
                    p.lastKnownGamestate = Gamestate.LOADWAITING;
                }
                foreach (Player pp in players.GetChildren())
                {
                    if (pp.lastKnownGamestate != Gamestate.LOADWAITING) //someone isnt done loading
                    {
                        return;
                    }
                }
                //everyone finished loading
                //TODO: Remove loading screen
                Steam.debugLog("All players have finished loading!");
                break;
            
            case Gamestate.GAME:
                break;
            
            case Gamestate.ENDSCREEN:
                break;
            
            
            default:
                Steam.debugLog("GAMESTATE ERROR: " + msg.state.ToString());
                throw new Exception();
        }

    }



    /// <summary>
    /// Called when we should start hosting/join the server
    /// </summary>
    public void networkStart()
    {
        if (lobby.lobby.IsOwnedBy(SteamClient.SteamId))//you are the host.
        {
            network.host();
            Steam.debugLog("I am the host, attempting to establish the server.");
            lobby.lobby.SetData("networkStart", "true");
        }
        else//you are not the host.
        {
            Steam.debugLog("I am NOT the host, attempting to join the server.");
            network.connect(lobby.lobby.Owner.Id);
        }
    }

    /// <summary>
    /// Called when we should actually start the real game
    /// </summary>
    public void gameLaunch()
    {
        if (network.offline)
        {
            network.startOffline();
        }
        if (network.hosting || network.offline)
        {
            network.connection.SendMessageToSocketServer(new NetMsg(MessageType.Gamestate, SteamClient.SteamId, new GamestateMsg(Main.Gamestate.LOBBYSTART)).box());
        }
    }
    //Below are a bunch of trash helper functions I'm too lazy to document.
    public void switchUIControl(Control to)
    {
        clearUI();
        uiLayer.AddChild(to);
    }
    public void switchUIPath(string fullPath)
    {
        clearUI();
        uiLayer.AddChild(loadUIFromPath(fullPath));
    }
    public void switchUIName(string uiName)
    {
        clearUI();
        uiLayer.AddChild(loadUIFromName(uiName));
    }
    public void clearUI()
    {
        foreach (Node c in uiLayer.GetChildren())
        {
            uiLayer.RemoveChild(c);
            c.QueueFree();
        }
    }
    public Control loadUIFromName(string uiName)
    {
        return ResourceLoader.Load<PackedScene>("res://scenes/ui/" + uiName).Instantiate<Control>();
    }
    public Control loadUIFromPath(string fullPath)
    {
        return ResourceLoader.Load<PackedScene>("fullPath").Instantiate<Control>();
    }
    public void switchLevelNode3D(Node3D to)
    {
        clearLevel();
        level.AddChild(to);
    }
    public void switchLevelPath(string fullPath)
    {
        clearLevel();
        level.AddChild(loadLevelFromPath(fullPath));
    }
    public void switchLevelName(string uiName)
    {
        clearLevel();
        level.AddChild(loadLevelFromName(uiName));
    }
    public void clearLevel()
    {
        foreach (Node c in level.GetChildren())
        {
            level.RemoveChild(c);
            c.QueueFree();
        }
    }
    public Node3D loadLevelFromName(string levelName)
    {
        return ResourceLoader.Load<PackedScene>("res://scenes/" + levelName).Instantiate<Node3D>();
    }
    public Node3D loadLevelFromPath(string fullPath)
    {
        return ResourceLoader.Load<PackedScene>("fullPath").Instantiate<Node3D>();
    }
}

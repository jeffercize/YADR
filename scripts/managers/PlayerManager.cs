using Godot;
using Godot.Collections;
using System;

public partial class PlayerManager : Node3D
{
    public Dictionary<ulong, Player> players = new Dictionary<ulong, Player>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	
	
	}

    double PlayerStateSyncTimer = 1f;
    double PlayerStateSyncCounter = 0f;
    bool hostSync =false;
    public override void _PhysicsProcess(double delta)
    {
        if (Global.NetworkManager.isHost && hostSync)
        {
            PlayerStateSyncCounter += delta;
            if (PlayerStateSyncCounter > PlayerStateSyncTimer)
            {
                foreach (Player player in players.Values)
                {
                    //ClientPlayerStateHandler.CreateAndSendPlayerPositionMessage(player);
                    Global.NetworkManager.networkDebugLog("Client/Host - Sending Position Sync for Player: " + player.clientID);
                }
                PlayerStateSyncCounter = 0;
            }
        }


    }


    public Player CreateAndRegisterNewPlayer(ulong clientID)
	{
        Player player = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        if (Global.instance.clientID == clientID)
        {
            Global.debugLog("Registering new local player with ID: " + clientID);
            player.clientID = Global.instance.clientID;
            //player.input = Global.InputManager.localInput;
        }
        else
        {
            Global.debugLog("Registering new remote player with ID: " + clientID);
            //Global.InputManager.BindRemoteClientInput(clientID, player);
        }

        players.Add(clientID, player);
		return player;
    }


    public void SpawnPlayer(Player player, Vector3 GlobalPosition)
    {
        Global.debugLog("Spawning player: " + player.clientID);
        AddChild(player);
        player.GlobalPosition = GlobalPosition;
        player.spawned = true;
    }

    public void SpawnAll()
    {
        Global.debugLog("Spawning all known players. Num: " + players.Count);
        Vector3 offset = Vector3.Zero;
        foreach (Player player in players.Values)
        {
            SpawnPlayer(player, offset);
            offset.X += 20;
        }
    }
}

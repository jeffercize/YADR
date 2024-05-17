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




    public Player CreateAndRegisterNewPlayer(ulong clientID)
	{
        Player player = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        if (Global.instance.clientID == clientID)
        {
            player.clientID = Global.instance.clientID;
            player.input = Global.InputManager.localInput;
        }
        else
        {
            Global.InputManager.BindRemoteClientInput(clientID, player);
        }
        Global.debugLog("Registering new player with ID: " + clientID);
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

using Godot;
using Godot.Collections;
using System;

public partial class PlayerManager : Node3D
{
    Dictionary<ulong, BasePlayer> players = new Dictionary<ulong, BasePlayer>();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	
	
	}

    public void SpawnLocalPlayer()
    {

        Player player = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();

        player.input = Global.InputManager.localInput;
        AddChild(player);
        player.Position = new Vector3(10, 10, 0);
        Input.MouseMode = Input.MouseModeEnum.Captured;


        //debug
        
        AddChild(GenerateRemotePlayer(Global.instance.clientID));
    }


    public static BasePlayer GenerateRemotePlayer(ulong clientID)
	{
        BasePlayer remotePlayer = ResourceLoader.Load<PackedScene>("res://scenes/BasePlayer.tscn").Instantiate<BasePlayer>();
        Global.InputManager.BindRemoteClientInput(clientID, remotePlayer);
		return remotePlayer;
    }

}

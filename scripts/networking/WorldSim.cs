using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class WorldSim : Node3D
{
    public Player localPlayer;
    public Dictionary<ulong, Player> remotePlayers = new();



    public Dictionary<ulong, WorldObject> LocalManagedObjects = new();
    public Dictionary<ulong, WorldObject> RemoteManagedObjects = new();




    public ulong tick = 0;
    public override void _Ready()
    {
        Global.debugLog("WorldSim Ready");
        NetworkPeer.StateMessageReceivedEvent += OnStateMessageReceived;
    }

    public void SpawnLocalObject(WorldObject obj)
    {
        LocalManagedObjects.Add(obj.entityID, obj);
        AddChild(obj);
    }

    public void SpawnRemoteObject(WorldObject obj)
    {
        RemoteManagedObjects.Add(obj.entityID, obj);
        AddChild(obj);
    }

    private void OnStateMessageReceived(State state)
    {

        foreach (GameObject obj in state.GameObjects)
        {
            if (LocalManagedObjects.ContainsKey(obj.Id))
            {
                Global.debugLog("SYNC ERROR: Someone is making a claim on an object that we own: " + obj.Id);
            }
            else if (RemoteManagedObjects.ContainsKey(obj.Id))
            {
                RemoteManagedObjects[obj.Id].desiredState = new WorldObject(obj);
            }
            else
            {
                SpawnRemoteObject(new WorldObject(obj));
            }
        }

        foreach (PlayerState playerState in state.States)
        {
            if (playerState.ClientID == Global.clientID)
            {
                Global.debugLog("SYNC ERROR: Someone is making a claim on ME!!");
            }
            else if (remotePlayers.ContainsKey(playerState.ClientID))
            {
                remotePlayers[playerState.ClientID].desiredState = playerState;
            }
            else
            {
                SpawnRemotePlayer(GenerateRemotePlayer(playerState.ClientID));
                remotePlayers[playerState.ClientID].desiredState = playerState;
            }
        }
    }

    public void SpawnRemotePlayer(Player player)
    {
        remotePlayers.Add(player.clientID, player);
        AddChild(player);
        player.spawned = true;
        localPlayer.GlobalPosition = new Vector3(0, 20, 0);
    }

    public void SpawnLocalPlayer(Player player)
    {
        localPlayer = player;
        AddChild(player);
        player.spawned = true;
        localPlayer.GlobalPosition = new Vector3(0, 10, 0);
    }

    public Player GenerateLocalPlayer()
    {
        Player local = GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        local.clientID = Global.clientID;
        return local;
    }

    public Player GenerateRemotePlayer(ulong clientID)
    {
        Player remote = GD.Load<PackedScene>("res://scenes/Player.tscn").Instantiate<Player>();
        remote.clientID = clientID;
        return remote;
    }

    public void loadScene(string scenePath)
    {
        PackedScene scene = GD.Load<PackedScene>(scenePath);
        Node3D sceneInstance = (Node3D)scene.Instantiate();
        AddChild(sceneInstance);
    }   

    public override void _Process(double delta)
    {
        foreach (WorldObject obj in RemoteManagedObjects.Values)
        {
           obj.IterativeSync();
        }
        foreach(Player player in remotePlayers.Values)
        {
           player.IterativeSync();
           player.HardSync();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        State state = new State();
        state.Sender= Global.clientID;
        state.Timestamp = Time.GetUnixTimeFromSystem();
        state.Tick = Global.getTick();
        foreach (WorldObject obj in LocalManagedObjects.Values)
        {
            state.GameObjects.Add(obj.ToNetworkMessage());
        }


        state.States.Add(localPlayer.ToNetworkMessage());

        //Global.debugLog("Sending local player + object state with " + state.GameObjects.Count + " objects at tick: " + tick);
        Global.NetworkPeer.MessageAllPeers(state);
        tick++;
    }
}


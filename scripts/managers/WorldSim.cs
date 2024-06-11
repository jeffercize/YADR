using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public partial class WorldSim : Node3D
{
    public ulong tick = 0;
    public double tickTimer = 0;
    public static int tickRate = ProjectSettings.GetSetting("physics/common/physics_ticks_per_second").AsInt32();
    public double tickTime = 1 / tickRate;
    public const int rollbackTicks = 3;

    public Node3D currentScene;
    public ulong missedPackets = 0;

    public Dictionary<ulong, INetworkableEntity> serverManagedObjects;
    public Dictionary<ulong, INetworkableEntity> clientManagedObjects;

    public Dictionary<ulong, Player> players = new();
    public Dictionary<ulong, PlayerInput> inputs = new();

    public Dictionary<ulong, List<FramePacket>> PacketBuffer = new();


    public Player Self;

    public RandomNumberGenerator rng = new RandomNumberGenerator();
    public ulong idSequenceNumber = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        rng.Seed = 12345;
        PacketBuffer[0] = new List<FramePacket>();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
        tick++;
        PacketBuffer[tick] = new List<FramePacket>();
        
        if (tick % 300 == 0)
        {
            Global.debugLog("Tick: " + tick);
            Global.debugLog(Global.NetworkManager.client.totalBandwidth.ToString());
        }

        foreach (Player player in players.Values)
        {
            if (player.spawned)
            {
                 player.Tick(inputs[player.clientID],delta);
                player.lastFrameInput = inputs[player.clientID];
            }
        }

        if (Global.NetworkManager.isHost)
        {
            foreach (Server.ConnectionData c in Global.NetworkManager.server.clients.Values)
            {
                foreach (Player player in players.Values)
                {
                    PlayerState state = new();
                    state.ClientID = player.clientID;
                    state.PhysicsObject = player.GetState();
                    state.Playerhealth = player.GetHealth();
                    state.Equipment = player.GetEquipment();
                    state.Inventory = player.GetInventory();
                    c.nextFramePacket.States.Add(state);
                }
                foreach (PlayerInput input in inputs.Values)
                {
                    c.nextFramePacket.Inputs.Add(input);
                }

            }


        }

    }

    public override void _Process(double delta)
    {
        inputs[Global.clientID] = Global.InputManager.localInput;
    }

    public bool loadScene(string uri)
    {
        try
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>(uri, "PackedScene", ResourceLoader.CacheMode.Reuse);
            currentScene = scene.Instantiate<Node3D>();
            AddChild(currentScene);
            return true;
        }
        catch
        {
            throw;
        }
    }

    public bool clearScenes()
    {
        foreach (Node3D scene in GetChildren())
        {
            scene.QueueFree();
        }
        return true;
    }

    public bool preloadScene(string uri)
    {
        try
        {
            PackedScene preloaded = ResourceLoader.Load<PackedScene>(uri, "PackedScene", ResourceLoader.CacheMode.Replace);
            return preloaded.CanInstantiate();
        }
        catch
        {
            return false;
        }

    }

    public Player CreateNewPlayer(ulong clientID)
    {
        Global.debugLog("Creating new player: " + clientID);
        
       Player player = GD.Load<PackedScene>("scenes/Player.tscn").Instantiate<Player>();
        player.clientID = clientID;
        return player;
    }

    public void RegisterPlayer(Player player)
    {
        Global.debugLog("Registering player: " + player.clientID);
        players.Add(player.clientID, player);
        player.entityID = idSequenceNumber++;
        if (player.clientID == Global.clientID)
        {
            Self = player;
            Global.debugLog("Registered local player");
        }
        else
        {
            Global.debugLog("Registered remote player: " + player.clientID);
        }
    }

    public void UnregisterPlayer(Player player)
    {
        Global.debugLog("Unregistering player: " + player.clientID);
        players.Remove(player.clientID);
    }

    public void SpawnPlayer(Player player, Vector3 GlobalPosition)
    {
        Global.debugLog("Spawning player: " + player.clientID);
        AddChild(player);
        player.GlobalPosition = GlobalPosition;
        player.spawned = true;
        if (player.clientID == Global.clientID)
        {
//            clientManagedObjects.Add(player.entityID, player);
        }
        else
        {
      //      serverManagedObjects.Add(player.entityID, player);
        }
    }

    public void DespawnPlayer(Player player)
    {
        Global.debugLog("Despawning player: " + player.clientID);
        RemoveChild(player);
        player.spawned = false;
    }

    public void SpawnAll()
    {
        Global.debugLog("Spawning all known players. Num: " + players.Count);
        Vector3 offset = Vector3.Zero;
        offset.Y = 10;
        foreach (Player player in players.Values)
        {
            SpawnPlayer(player, offset);
            offset.Y += 10;
        }
    }

    internal void ApplyFramePacket(FramePacket framePacket)
    {
        foreach (PlayerInput input in framePacket.Inputs)
        {
            if (input.ClientID!=Global.clientID)
            {
                inputs[input.ClientID] = input;
            }
        }
        foreach (PlayerState state in framePacket.States)
        {
            if (state.ClientID != Global.clientID)
            {
                players[state.ClientID].LerpToState(state.PhysicsObject.Position, state.PhysicsObject.Rotation, state.PhysicsObject.Scale, state.PhysicsObject.LinearVelocity, state.PhysicsObject.AngularVelocity);
            }
        }
    }
}



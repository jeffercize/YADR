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

    public Dictionary<ulong, INetworkManagedEntity> serverManagedObjects;
    public Dictionary<ulong, INetworkManagedEntity> clientManagedObjects;

    public Dictionary<ulong, Player> players = new();
    public Player Self;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {

        if (tick % 300 == 0)
        {
            Global.debugLog("Tick: " + tick);
        }

        Global.debugLog("Sending off inputs for tick: " + tick);

        if (Global.NetworkManager.client.framePacketBuffer.TryGetValue(tick - rollbackTicks, out FramePacket packet))
        {
            Global.debugLog("Found packet at tick: " + (tick - rollbackTicks));
            Global.NetworkManager.client.framePacketBuffer.Remove(tick - rollbackTicks);
            foreach (PlayerInput input in packet.Inputs)
            {
                if (players.TryGetValue(input.ClientID.SteamID, out Player player))
                {
                    if (input.ClientID.SteamID == Global.instance.clientID)
                    {
                        continue;
                    }
                    player.input =input;
                }
            }
        }
        else
        {
            if (tick == 0)
            {
                tick++;
                return;
            }
            missedPackets++;
            Global.debugLog("Missed a packet at tick: " + (tick - rollbackTicks) + " Total dropped: " + missedPackets + " Percent Dropped: " + (float)missedPackets / (float)tick);

        }

        tick++;
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
        if (player.clientID == Global.instance.clientID)
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
        }
    }

}



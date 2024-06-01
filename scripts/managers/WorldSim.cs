using Godot;
using NetworkMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    public partial class WorldSim: Node3D
    {
        public ulong tick = 0;
        public double tickTimer = 0;
        public static int tickRate = ProjectSettings.GetSetting("physics/common/physics_ticks_per_second").AsInt32();
        public double tickTime = 1/tickRate;

    public ulong missedPackets = 0;

        public Dictionary<ulong, INetworkManagedEntity> serverManagedObjects;
        public Dictionary<ulong, INetworkManagedEntity> clientManagedObjects;

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
        }

        // Called every frame. 'delta' is the elapsed time since the previous frame.
        public override void _PhysicsProcess(double delta)
        {

            if (tick%300==0)
            {
                Global.debugLog("Tick: "+tick);
            }

            if(Global.NetworkManager.client.framePacketBuffer.TryGetValue(tick, out FramePacket packet))
            {
                Global.NetworkManager.client.framePacketBuffer.Remove(tick);
            }
            else
            {
                if (tick==0)
                {
                tick++;
                return;
                }
                missedPackets++;
                Global.debugLog("Missed a packet at tick: "+tick +" Total dropped: " + missedPackets + " Percent Dropped: " + (float)missedPackets/(float)tick); 

            }


            tick++;
        }


    }
    


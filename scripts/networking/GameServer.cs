using Godot;
using System;

    public partial class GameServer: Node
    {


    public long tickCount = 0;
    public const int ticksPerSecond = 60;
    public const double secondsPerTick = 1 / ticksPerSecond;
    private double accumulator = 0f;
    public override void _Process(double delta)
    {
        accumulator += delta;
        if (accumulator >= 1f / ticksPerSecond)
        {
            Tick();
            accumulator = 0f;
        }
    }

    private void Tick()
    {
        tickCount++;
        Time.GetTicksUsec();
    }

}


using Godot;
using System;
using System.Diagnostics;

public partial class Global : Node
{

	public static ui ui;
	public static level level;


    static readonly public bool enableLogging = true;

    public override void _Ready()
	{
		ui = GetNode<ui>("../main/ui");
		level = GetNode<level>("../main/level");
	}

    public static void debugLog(string msg)
    {
        if (enableLogging)
        {
            GD.Print("[DEBUG] " + msg);
        }
    }
}

using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Equipment : Node
{
    public Dictionary<String, EquipSlot> equipSlots = new();
    private string[] slotStrings = { "helmet", "bodyArmor", "weapon1", "weapon2", "face", "boot", "helmetAttachment", "glove", "backpack", "quick1", "quick2", "quick3" };

    public Character connectedCharacter;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        foreach (string s in slotStrings)
        {

        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}
}

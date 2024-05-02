using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Equipment : Node
{
    public List<String> defaultSlotNames = new List<string> { "head", "chest", "back", "leftHand", "rightHand" };
    public Dictionary<String, Equipable> slots = new();
    public Dictionary<String, List<Equipable.EquipType>> slotTypes = new();
    public Character connectedCharacter;

    public delegate void equipMessageHandler(EquipSlot slot, Item item);
    public static event equipMessageHandler equipMessage = delegate { };

    public delegate void unequipMessageHandler(EquipSlot slot, Item item);
    public static event unequipMessageHandler unequipMessage = delegate { };

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        initDefaultSlots();
    }

    private void initDefaultSlots()
    {
        foreach (String s in defaultSlotNames)
        {
            slots.Add(s, Equipable.NONE);
        }

        slotTypes.Add("head", new List<Equipable.EquipType>{ Equipable.EquipType.HELMET });

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

    internal void equip(string slotName, Equipable item)
    {
        if (slots.TryGetValue("slotName", out Equipable currentEquip))
        {
            if (currentEquip == Equipable.NONE)
            {
                slots.Remove(slotName);
                slots.TryAdd(slotName, item);
            }
        }
    }

    internal void unequip(StringName name, Item dragItem)
    {
        throw new NotImplementedException();
    }
}

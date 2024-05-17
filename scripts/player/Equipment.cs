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

    public delegate void equipMessageHandler(Equipment equipment, string slotName, Item item);
    public static event equipMessageHandler equipMessage = delegate { };

    public delegate void unequipMessageHandler(Equipment equipment, string slotName, Item item);
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
            slots.Add(s, null);
        }

        slotTypes["head"] = new List<Equipable.EquipType>{ Equipable.EquipType.HELMET };
        slotTypes["leftHand"] = new List<Equipable.EquipType> { Equipable.EquipType.GUNWEAPON };
        slotTypes["rightHand"] = new List<Equipable.EquipType> { Equipable.EquipType.GUNWEAPON };
        slotTypes["chest"] = new List<Equipable.EquipType>();
        slotTypes["back"] = new List<Equipable.EquipType>();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

    public bool equip(string slotName, Equipable newEquip)
    {
        Global.debugLog("attempting to equip " + newEquip.Name +" in slot " + slotName);
        if (!slots.ContainsKey(slotName))
        {
            Global.debugLog("Equip fail. slot doesnt exist.");
            return false;
        }
        slots.TryGetValue(slotName, out Equipable temp);
        if (temp == null) 
        {
            Global.debugLog("Equipping, slot is nulled.");
            slots[slotName] = newEquip;
            equipMessage.Invoke(this, slotName, newEquip);
            return true;
        }
        if (temp == Equipable.NONE)
        {
            Global.debugLog("Equipping, slot is NONE");
            slots[slotName] = newEquip;
            equipMessage.Invoke(this, slotName, newEquip);
            return true;
        }

        return false;
    }

    public void swap(string slotName, Equipable newEquip, out Equipable oldEquip)
    {
        unequip(slotName, out oldEquip);
        equip(slotName, newEquip);
    }

    public void unequip(string slotName, out Equipable oldEquip)
    {
        oldEquip = slots[slotName];
        slots[slotName] = null;
        unequipMessage.Invoke(this, slotName, oldEquip);
    }

    internal bool canEquip(String slotName, Item equip)
    {
        if (equip is not Equipable e) { return false; }
        return canEquip(slotName, e);
    }

    internal bool canEquip(String slotName, Equipable equip)
    {
        if (slotTypes[slotName].Contains(equip.type)) 
        {
            return true;
        }
        return false;
    }
}

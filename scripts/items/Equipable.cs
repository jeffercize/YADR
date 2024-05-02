using Godot;
using System;

[GlobalClass]
public partial class Equipable : Item
{

	public static new Equipable NONE;

	public enum EquipType
	{
		ANY,
		HELMET,
		HELMETATTACHMENT,
		FACE,
		BODYARMOR,
		BOOTS,
		GLOVES,
		BACKPACK,
		GUNWEAPON,
		MELEEWEAPON,
		SPECIALWEAPON,
		OFFHANDWEAPON,
		SPECIAL1,
		SPECIAL2,
		SPECIAL3,
		NONE,
	}

	[Export]
	public EquipType type;

	public Equipable() { type = EquipType.NONE; }
	public Equipable(EquipType type) { this.type = type; }

    public Equipable(float weight, int width, int height, string name, string description, bool stackable, Texture2D icon) : base(weight, width, height, name, description, stackable, icon)
    {
    }
}

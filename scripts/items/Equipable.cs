using Godot;
using System;

[GlobalClass]
public partial class Equipable : Item
{

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
	}

	[Export]
	public EquipType type;

	public Equipable() { }

    public Equipable(float weight, int width, int height, string name, string description, bool stackable, Texture2D icon) : base(weight, width, height, name, description, stackable, icon)
    {
    }
}

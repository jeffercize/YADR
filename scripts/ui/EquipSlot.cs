using Godot;
using System.Collections.Generic;
using System.Linq;
using static Networking.SteamNetwork;

[GlobalClass]
public partial class EquipSlot : Control
{

    [Export]
    public Item item = Item.NONE;

    public Equipment connectedEquipment;

    [Export]
    public Equipable.EquipType EquippableType;
    public List<Equipable.EquipType> EquippableTypes = new();

    [Export]
    int DRAG_START_DIST = 50;
    public Vector2 startMousePos = Vector2.Zero;





    public EquipSlot()
    {
        EquippableType = Equipable.EquipType.ANY;
        EquippableTypes.Add(Equipable.EquipType.ANY);
    }

    public EquipSlot(Equipable.EquipType type)
    {
        EquippableType = type;
        EquippableTypes.Add(type);
    }

    public EquipSlot(List<Equipable.EquipType> types)
    {
        EquippableTypes = types;
    }


    public override void _Draw()
    {
        if (item != Item.NONE)
        {
            Vector2 rectSize = new Vector2(item.width * Global.ui.gameUI.playerInventoryUI.slotSizeX, item.height * Global.ui.gameUI.playerInventoryUI.slotSizeX);

            //DrawRect(itemBox, Colors.Azure, false, 5);
            DrawTextureRect(item.icon, GetChild<Panel>(0).GetRect().Grow(-10f), false);
        }
        else
        {
            DrawString(GetThemeDefaultFont(), GetChild<Panel>(0).GetRect().GetCenter(), EquippableType.ToString());

        }
        if (Global.ui.gameUI.dragItem != null && Global.ui.gameUI.dragItem != Item.NONE && canEquip(Global.ui.gameUI.dragItem))
        {
            DrawRect(GetChild<Panel>(0).GetRect().Grow(10f), Colors.Aqua, false, 4);
        }

    }

    public override void _Process(double delta)
    {

        if (!IsVisibleInTree())
        {
            return;
        }
        QueueRedraw();
        if (GetChild<Panel>(0).GetRect().HasPoint(GetLocalMousePosition()))
        {
            if (Input.IsActionJustPressed("LCLICK", true)) //sent for one frame when Lmouse first goes down
            {
                if (Global.ui.gameUI.dragItem == Item.NONE)
                {
                    if (item != Item.NONE && startMousePos == Vector2.Zero)
                    {
                        Global.debugLog(this.Name + "Click on item!. Testing to see if drag...");
                        startMousePos = GetLocalMousePosition();
                    }
                    else
                    {
                        Global.debugLog(this.Name + "Click!");
                    }
                }

            }
            else if (Input.IsActionJustReleased("LCLICK", true)) //sent for one frame when Lmouse goes back up
            {
                if (Global.ui.gameUI.dragItem == Item.NONE && item != Item.NONE)
                {
                    Global.debugLog(this.Name + "Click pickup, Item Unequipped!");
                    Global.ui.gameUI.dragItem = item;
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                    item = Item.NONE;
                    startMousePos = Vector2.Zero;
                    connectedEquipment.unequip(this.Name, Global.ui.gameUI.dragItem);
                    QueueRedraw();
                }
                else if (Global.ui.gameUI.dragItem != Item.NONE && item == Item.NONE)
                {
                    if (canEquip(Global.ui.gameUI.dragItem))
                    {
                        Global.debugLog(this.Name + "Drag or click Drop! Item Equipped!");
                        GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouserelease1.wav");
                        GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                        item = Global.ui.gameUI.dragItem;
                        Global.ui.gameUI.dragItem = Item.NONE;
                        startMousePos = Vector2.Zero;
                        connectedEquipment.equip(this.Name,item as Equipable);
                        QueueRedraw();
                    }
                    else
                    {
                        Global.debugLog(this.Name + "Item doesnt fit in this slot!");
                    }
                }
                else if (Global.ui.gameUI.dragItem != Item.NONE && item != Item.NONE)
                {
                    if (item.combineWith(Global.ui.gameUI.dragItem, out Item remain))
                    {
                        Global.ui.gameUI.dragItem = remain;
                    }
                    else
                    {
                        Global.debugLog(this.Name + "These items cannot combine");
                        Item temp = item;
                        item = Global.ui.gameUI.dragItem;
                        Global.ui.gameUI.dragItem = temp;
                    }
                }
            }
        }

        if (Input.IsActionPressed("LCLICK", true)) //sent every frame the Lmouse button is down
        {
            if (Global.ui.gameUI.dragItem == Item.NONE && item != Item.NONE && startMousePos.DistanceTo(GetLocalMousePosition()) > DRAG_START_DIST && startMousePos != Vector2.Zero)
            {
                Global.debugLog(this.Name + "Drag Pickup!");
                GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                Global.ui.gameUI.dragItem = item;
                item = Item.NONE;
                startMousePos = Vector2.Zero;
                connectedEquipment.unequip(this.Name, Global.ui.gameUI.dragItem);
                QueueRedraw();
            }
        }

    }

    public bool canEquip(Item item)
    {
        if (item is Equipable equip)
        {
            if (EquippableType==equip.type || EquippableType==Equipable.EquipType.ANY || EquippableTypes.Contains(equip.type))
            {
                return true;
            }
        }
        return false;
    }
    
}
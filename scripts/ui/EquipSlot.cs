using Godot;

[GlobalClass]
public partial class EquipSlot : Control
{
    public Equipable equip;

    [Export]
    public Equipment connectedEquipment;

    [Export]
    int DRAG_START_DIST = 50;
    public Vector2 startMousePos = Vector2.Zero;

    public override void _Ready()
    {
        this.ProcessMode = ProcessModeEnum.Disabled;
    }

    public override void _Draw()
    {
        if (equip != null)
        {
            Vector2 rectSize = new Vector2(equip.width * Global.UIManager.playerInventoryUI.slotSizeX, equip.height * Global.UIManager.playerInventoryUI.slotSizeX);

            //DrawRect(itemBox, Colors.Azure, false, 5);
            DrawTextureRect(equip.icon, GetChild<Panel>(0).GetRect().Grow(-10f), false);
        }
        else
        {
            DrawString(GetThemeDefaultFont(), GetChild<Panel>(0).GetRect().GetCenter(), this.Name);

        }
        if (Global.UIManager.dragItem != null && Global.UIManager.dragItem != Item.NONE && connectedEquipment.canEquip(this.Name, Global.UIManager.dragItem))
        {
            DrawRect(GetChild<Panel>(0).GetRect().Grow(10f), Colors.Aqua, false, 4);
        }

    }

    public override void _Process(double delta)
    {
        if (connectedEquipment == null)
        {
            //connectedEquipment = Global.self.equipment;
        }
        connectedEquipment.slots.TryGetValue(this.Name, out equip);
        if (!IsVisibleInTree())
        {
            return;
        }
        QueueRedraw();
        if (GetChild<Panel>(0).GetRect().HasPoint(GetLocalMousePosition()))
        {
            if (Input.IsActionJustPressed("LCLICK", true)) //sent for one frame when Lmouse first goes down
            {
                if (!Global.UIManager.hasDragItem())
                {
                    if (equip != Item.NONE && startMousePos == Vector2.Zero)
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
                if (!Global.UIManager.hasDragItem() && hasEquip())
                {
                    Global.debugLog(this.Name + "Click pickup, Item Unequipped!");

                    connectedEquipment.unequip(this.Name, out equip);
                    Global.UIManager.dragItem = equip;
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();

                    startMousePos = Vector2.Zero;

                    QueueRedraw();
                }
                else if (Global.UIManager.hasDragItem() && !this.hasEquip())
                {
                    if (connectedEquipment.canEquip(this.Name, Global.UIManager.dragItem))
                    {
                        Global.debugLog(this.Name + "Drag or click Drop! Item Equipped!");
                        GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouserelease1.wav");
                        GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                        startMousePos = Vector2.Zero;
                        connectedEquipment.equip(this.Name, Global.UIManager.dragItem as Equipable);
                        Global.UIManager.dragItem = Item.NONE;
                        QueueRedraw();
                    }
                    else
                    {
                        Global.debugLog(this.Name + "Item doesnt fit in this slot!");
                    }
                }
                else if (Global.UIManager.hasDragItem() && this.hasEquip())
                {
                    if (equip.combineWith(Global.UIManager.dragItem, out Item remain))
                    {
                        Global.UIManager.dragItem = remain;
                    }
                    else
                    {
                        if (Global.UIManager.dragItem is Equipable e)
                        {
                            Global.debugLog(this.Name + "These items cannot combine,swapping...");
                            connectedEquipment.unequip(this.Name, out Equipable oldEquip);
                            connectedEquipment.equip(this.Name, e);
                            Global.UIManager.dragItem = oldEquip;
                        }


                    }
                }
            }
        }

        if (Input.IsActionPressed("LCLICK", true)) //sent every frame the Lmouse button is down
        {
            if (!Global.UIManager.hasDragItem() && hasEquip() && startMousePos.DistanceTo(GetLocalMousePosition()) > DRAG_START_DIST && startMousePos != Vector2.Zero)
            {
                Global.debugLog(this.Name + "Drag Pickup!");
                GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                startMousePos = Vector2.Zero;
                connectedEquipment.unequip(this.Name, out equip);
                Global.UIManager.dragItem = equip;
                QueueRedraw();
            }
        }

    }

    private bool hasEquip()
    {
        connectedEquipment.slots.TryGetValue(this.Name, out Equipable temp);
        return temp != null;
    }
}
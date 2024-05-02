 using Godot;
using Steamworks.Ugc;
using System;

public partial class gameUI : Control
{
    public Player player { get; set; }
    public Control playerMenu { get; set; }
    public InventoryUI playerInventoryUI { get; set; }
    public Equipment equipment { get; set; }

    public Item dragItem = Item.NONE;

    public int slotSize;

    public override void _Ready()
    {
        //Load up the PlayerMenu UI screen and add it as a child, and hide it
        playerMenu = ResourceLoader.Load<PackedScene>("res://scenes/ui/PlayerMenu.tscn").Instantiate<Control>();
        playerMenu.Visible = false;
        AddChild(playerMenu);

        //Grab a pointer to the UI element that corresponds to the player's inventory
        playerInventoryUI = GetNode<InventoryUI>("PlayerMenu/InventoryPanel/InventoryPanelMargins/ScrollContainer/PlayerInventoryUI");
        playerInventoryUI.connectedInventory = player.inventory;
        slotSize = playerInventoryUI.slotSizeX;

        //Take the space provided by the UI element above, and slice it into a grid.
        playerInventoryUI.subdivide();
        //playerInventoryUI.connectedInventory.debugGen();

        //Grab a pointer to the UI Element that corresponds to the player's equipment page
        equipment = GetNode<Equipment>("PlayerMenu/LeftTabs/Equipment");
        equipment.connectedCharacter = player;


        //Event subscription
        PlayerInput.input_OpenInventory += input_OpenInventory;
    }

    public override void _Draw()
    {
        //Draw the item that we are dragging around. The dragItem variable and this draw function appear here to enable cross-ui dragging and interaction 
        if (dragItem != Item.NONE)
        {
            Vector2 center = new Vector2(dragItem.width/2f, dragItem.height/2f);
            center = center * playerInventoryUI.slotSizeX;
            Vector2 newTopLeft = new Vector2(GetLocalMousePosition().X - center.X, GetLocalMousePosition().Y - center.Y);
            Rect2 itemBox = new Rect2(newTopLeft,dragItem.width*slotSize,dragItem.height*slotSize);
            DrawTextureRect(dragItem.icon, itemBox, false);
        }
    }

    public override void _Process(double delta)
    {
        if (playerMenu.IsVisibleInTree() || false)
        {
            QueueRedraw();
        }



        

    }

    public override void _GuiInput(InputEvent @event)
    {


        if (dragItem != Item.NONE && @event is InputEventMouseButton mb && mb.IsReleased())
        {
            Item temp = dragItem;
            dragItem = Item.NONE;
            Global.level.GetNode("proc").AddChild(temp);
            Vector3 pos= new Vector3(player.GlobalPosition.X, player.GlobalPosition.Y, player.GlobalPosition.Z);
            pos -= player.GlobalBasis.Z.Normalized();
            pos.Y += 1;
            temp.GlobalPosition = pos;
        }
    }

    public bool isUIOpen()
    {
        return (playerMenu.IsVisibleInTree());
    }

    private void input_OpenInventory()
    {
        if (playerMenu.Visible)
        {
            playerMenu.Visible = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else if (!playerMenu.Visible)
        {
            playerMenu.Visible = true;
            Input.MouseMode = Input.MouseModeEnum.Confined;
        }

    }
}


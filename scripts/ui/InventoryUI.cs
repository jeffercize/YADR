using Godot;
using System.Linq;

public partial class InventoryUI : Container
{
    public bool DEBUGLOG = true;
    public int slotSizeX = 50;
    public int slotSizeY = 50;
    float DRAG_START_DIST = 25;
    public Godot.Collections.Array<Vector2> points = new Godot.Collections.Array<Vector2>();
    Vector2 startMousePos = Vector2.Zero;
    Item tempItem = Item.NONE;
    public Inventory connectedInventory { get; set; }


    public InventoryUI(Inventory inventory)
    {
        connectedInventory = inventory;
    }

    public InventoryUI() { }

    public override void _Ready()
    {

    }


    public void subdivide()
    {
        CustomMinimumSize = new Vector2(Size.X, slotSizeY * connectedInventory.height);
        SetSize(new Vector2(Size.X, slotSizeY * connectedInventory.height));

        for (int y = 0; y < connectedInventory.height + 1; y++)
        {
            Vector2 from = new Vector2(Position.X, y * slotSizeY);
            Vector2 to = new Vector2(connectedInventory.width * slotSizeX, y * slotSizeY);
            points.Add(from);
            points.Add(to);
        }
        for (int x = 0; x < connectedInventory.width + 1; x++)
        {
            Vector2 from = new Vector2(x * slotSizeX, Position.Y);
            Vector2 to = new Vector2(x * slotSizeX, connectedInventory.height * slotSizeY);
            points.Add(from);
            points.Add(to);
        }
    }


    public override void _Input(InputEvent @event)
    {

    }


    public override void _Draw()
    {
        if (points.Count > 2)
        {
            DrawMultiline(points.ToArray(), Colors.Black, 2);
        }

        foreach (Item i in connectedInventory.items.Values.Distinct())
        {
            Rect2 itemBox = new Rect2(i.invTopLeft * slotSizeX, i.width * slotSizeX, i.height * slotSizeY);
            DrawTextureRect(i.icon, itemBox, false);

        }

        if (Global.UIManager.dragItem != Item.NONE)
        {
            if (!mapToLocalCoordsSnappy(GetLocalMousePosition(), Global.UIManager.dragItem.width, Global.UIManager.dragItem.height, out Vector2 mouseVec))
            {
                return;
            }
            int x = (int)mouseVec.X;
            int y = (int)mouseVec.Y;
            Item fake = new Item(Global.UIManager.dragItem.width, Global.UIManager.dragItem.height);
            if (connectedInventory.phantomPlaceItemAtCoordsOverlapOne(x, y, fake, out Item overlap))
            {
                Vector2 topLeft = new Vector2(fake.invTopLeft.X * slotSizeX, fake.invTopLeft.Y * slotSizeY);
                Vector2 bottomRight = new Vector2((fake.width) * slotSizeX, (fake.height) * slotSizeY);
                Rect2 snapBox = new Rect2(topLeft, bottomRight);
                DrawRect(snapBox, Colors.LightYellow, false, 5);

<<<<<<< HEAD
                if (overlap != Item.NONE)
=======
                if (overlap!=Item.NONE)
>>>>>>> refs/remotes/origin/master
                {
                    Rect2 itemBox = new Rect2(overlap.invTopLeft * slotSizeX, overlap.width * slotSizeX, overlap.height * slotSizeY);
                    DrawRect(itemBox, Colors.LightGray, false, 5);
                }
            }
            else
            {
                Vector2 topLeft = new Vector2(fake.invTopLeft.X * slotSizeX, fake.invTopLeft.Y * slotSizeY);
                Vector2 bottomRight = new Vector2((fake.width) * slotSizeX, (fake.height) * slotSizeY);
                Rect2 snapBox = new Rect2(topLeft, bottomRight);
                DrawRect(snapBox, Colors.Red, false, 5);
            }

        }


    }

    private bool mapToLocalCoordsSnappy(Vector2 vec, int width, int height, out Vector2 outVec)
    {
<<<<<<< HEAD
        int x = Mathf.RoundToInt(((float)vec.X / (float)slotSizeX) - (float)(width / 2f));
        int y = Mathf.RoundToInt(((float)vec.Y / (float)slotSizeY) - (float)(height / 2f));
        if (x == -1) { x = 0; }
        if (y == -1) { y = 0; }
        if (y <= connectedInventory.height - height / 2 && y >= connectedInventory.height - height) { y = connectedInventory.height - height; }
        if (x <= connectedInventory.width - width / 2 && x >= connectedInventory.width - width) { x = connectedInventory.width - width; }
        if (x < 0 || y < 0 || x >= connectedInventory.width - width / 2 || y >= connectedInventory.height - height / 2)
=======
        int x = Mathf.RoundToInt(((float)vec.X / (float)slotSizeX) - (float)(width/2f));
        int y = Mathf.RoundToInt(((float)vec.Y / (float)slotSizeY) - (float)(height/2f));
        if (x == -1) { x = 0; }
        if (y == -1) { y = 0; }
        if (y <= connectedInventory.height-height/2 && y>= connectedInventory.height - height) { y = connectedInventory.height - height; }
        if (x <= connectedInventory.width-width/2 && x>=connectedInventory.width - width) { x = connectedInventory.width - width; }
        if (x < 0 || y < 0 || x >= connectedInventory.width-width/2 || y >= connectedInventory.height-height/2)
>>>>>>> refs/remotes/origin/master
        {
            outVec = Vector2.Inf;
            return false;
        }
        outVec = new Vector2(x, y);
        return true;
    }

    private bool mapToLocalCoordsPrecise(Vector2 vec, out Vector2 outVec)
    {
        int x = Mathf.FloorToInt((vec.X / slotSizeX));
        int y = Mathf.FloorToInt((vec.Y / slotSizeY));
        if (x < 0 || y < 0 || x > connectedInventory.width || y > connectedInventory.height)
        {
            outVec = Vector2.Inf;
            return false;
        }
        outVec = new Vector2(x, y);
        return true;
    }

    public override void _Process(double delta)
    {

        if (!IsVisibleInTree())
        {
            return;
        }
        QueueRedraw();
        Vector2 mouseVec = Vector2.Inf;
        if (Global.UIManager.dragItem == Item.NONE)
        {
            if (!mapToLocalCoordsPrecise(GetLocalMousePosition(), out mouseVec))
            {
                return;
            }
        }
        else if (Global.UIManager.dragItem != Item.NONE)
        {
            if (!mapToLocalCoordsSnappy(GetLocalMousePosition(), Global.UIManager.dragItem.width, Global.UIManager.dragItem.height, out mouseVec))
            {
                return;
            }
<<<<<<< HEAD
            if (Global.UIManager.dragItem.height == 1)
=======
            if (Global.UIManager.dragItem.height == 1 )
>>>>>>> refs/remotes/origin/master
            {

            }
        }
        int x = (int)mouseVec.X;
        int y = (int)mouseVec.Y;
        if (Input.IsActionJustPressed("LCLICK")) //sent for one frame when Lmouse first goes down
        {
            if (Global.UIManager.dragItem == Item.NONE)
            {
                if (connectedInventory.items.TryGetValue(new Vector2(x, y), out tempItem))
                {
                    debugLog("[INV]Click on item! Coords: (" + x + "," + y + "). Testing to see if drag...");
                    debugLog("[INV]Local Mouse COORDS: " + GetLocalMousePosition());
                    startMousePos = GetLocalMousePosition();
                }
                else
                {
                    debugLog("[INV]Click! Coords: (" + x + "," + y + ")");
                }
            }
        }
        else if (Input.IsActionJustReleased("LCLICK")) //sent for one frame when Lmouse goes back up
        {
            if (Global.UIManager.dragItem == Item.NONE)
            {
                if (connectedInventory.removeItemAtCoords(x, y, out Global.UIManager.dragItem))
                {
                    debugLog("[INV]Release (Click) Pickup! (" + x + "," + y + ")");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                    debugLog("[INV]New Inv Items: " + connectedInventory.printValues());
                    debugLog("[INV]New Inv Key: " + connectedInventory.printKeys());
                    startMousePos = Vector2.Zero;
                    QueueRedraw();
<<<<<<< HEAD

=======
                    
>>>>>>> refs/remotes/origin/master
                }
                else
                {
                    debugLog("[INV]Release! Coords: (" + x + "," + y + ")");
                }
            }
            else if (Global.UIManager.dragItem != Item.NONE)
            {
                if (connectedInventory.phantomPlaceItemAtCoordsOverlapOne(x, y, Global.UIManager.dragItem, out Item overlap))
                {
<<<<<<< HEAD
                    if (overlap != Item.NONE)
=======
                    if (overlap!=Item.NONE)
>>>>>>> refs/remotes/origin/master
                    {
                        if (overlap.combineWith(Global.UIManager.dragItem, out Item remain))
                        {
                            Global.UIManager.dragItem = remain;
                        }
                        else
                        {
                            Item temp = Item.NONE;
                            connectedInventory.removeItemAtCoords(overlap.invTopLeft, out temp);
                            connectedInventory.placeItemAtCoords(x, y, Global.UIManager.dragItem);
                            Global.UIManager.dragItem = temp;
                        }

                    }
                    else
                    {
                        connectedInventory.placeItemAtCoords(x, y, Global.UIManager.dragItem);
                        Global.UIManager.dragItem = Item.NONE;
                    }
                    debugLog("[INV]Drag or click Drop! (" + x + "," + y + ")");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouserelease1.wav");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                    debugLog("[INV]New Inv Items: " + connectedInventory.printValues());
                    debugLog("[INV]New Inv Key: " + connectedInventory.printKeys());
                    startMousePos = Vector2.Zero;
                    QueueRedraw();
                }
                else
                {
                    debugLog("[INV]Item will not fit here. Coords: (" + x + "," + y + ")");

                }
            }
        }
        else if (Input.IsActionPressed("LCLICK")) //sent every frame the Lmouse button is down
        {
            if (Global.UIManager.dragItem == Item.NONE && tempItem != Item.NONE && startMousePos.DistanceTo(GetLocalMousePosition()) > DRAG_START_DIST && startMousePos != Vector2.Zero)
            {
                if (connectedInventory.removeItemAtCoords((int)tempItem.invTopLeft.X, (int)tempItem.invTopLeft.Y, out Global.UIManager.dragItem))
                {
                    debugLog("[INV]Drag Pickup! A distance of " + startMousePos.DistanceTo(GetLocalMousePosition()));
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Stream = ResourceLoader.Load<AudioStreamWav>("res://assets/audio/ui/mouseclick1.wav");
                    GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
                    startMousePos = Vector2.Zero;
                    tempItem = Item.NONE;
                    debugLog("[INV]New Inv Items: " + connectedInventory.printValues());
                    debugLog("[INV]New Inv Key: " + connectedInventory.printKeys());
                    QueueRedraw();
                }
                else
                {
                    debugLog("EOEROEROEROERORO");
                }
            }

        }

    }

    public void debugLog(string message)
    {
        if (DEBUGLOG)
        {
            GD.Print(message);
        }
    }
}

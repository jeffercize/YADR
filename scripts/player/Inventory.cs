
using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class Inventory : Node
{

    [Export] public int weightMax { get; set; } = 100;
    [Export] public int weightCurrent { get; set; } = 0;

    [Export]
    public int height { get; set; } = 10;

    [Export]
    public int width { get; set; } = 20;

    public Node3D spatialParent;
    public bool isPlayer { get; set; } = false;

    public Dictionary<Vector2, Item> items { get; set; } = new();

    public Inventory() { }

    public void generateGrid(int width, int height)
    {
        this.width = width;
        this.height = height;
        items = new Dictionary<Vector2, Item>();

    }


    public Item tryGetItemAtCoords(int x, int y)
    {
        items.TryGetValue(new Vector2(x, y), out Item item);
        return item;
    }

    public bool canPlaceItemAtCoords(Vector2 coords, Item item)
    {
        return canPlaceItemAtCoords((int)coords.X, (int)coords.Y, item);
    }

    public bool canPlaceItemAtCoords(int x, int y, Item item)
    {

        for (int xx = x; xx < x + item.width; xx++)
        {
            for (int yy = y; yy < y + item.height; yy++)
            {
                if (xx > width - 1 || yy > height - 1 || xx < 0 || yy < 0)
                {
                    return false;
                }
                items.TryGetValue(new Vector2(xx, yy), out Item it);
                if (it != null && it != item)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool placeItemAtCoordsOverlapOne(Vector2 coords, out Item replaced)
    {
        replaced = Item.NONE;
        return placeItemAtCoordsOverlapOne((int)coords.X, (int)coords.Y, out replaced);
    }

    private bool placeItemAtCoordsOverlapOne(int x, int y, out Item replaced)
    {
        throw new NotImplementedException();
    }

    public bool canPlaceItemAtCoordsOverlapOne(Vector2 coords, Item item, out Item overlap)
    {
        return canPlaceItemAtCoordsOverlapOne((int)coords.X, (int)coords.Y, item, out overlap);
    }

    private bool canPlaceItemAtCoordsOverlapOne(int x, int y, Item item, out Item overlap)
    {
        overlap = Item.NONE;
        for (int xx = x; xx < x + item.width; xx++)
        {
            for (int yy = y; yy < y + item.height; yy++)
            {
                if (xx > width - 1 || yy > height - 1 || xx < 0 || yy < 0)
                {
                    return false;
                }
                else if (items.TryGetValue(new Vector2(xx, yy), out Item it))
                {
                    if (it != null)
                    {
                        if (overlap == Item.NONE)
                        {
                            overlap = it;
                        }
                        else if (!it.Equals(overlap))
                        {
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }

    public bool phantomPlaceItemAtCoordsOverlapOne(Vector2 coords, Item item, out Item overlap)
    {
        return phantomPlaceItemAtCoordsOverlapOne((int)coords.X, (int)coords.Y, item, out overlap);
    }

    public bool phantomPlaceItemAtCoordsOverlapOne(int x, int y, Item item, out Item overlap)
    {
        item.invTopLeft = new Vector2((int)x, (int)y);
        bool retVal = canPlaceItemAtCoordsOverlapOne(x, y, item, out overlap);
        if (!retVal) { return false; }
        for (int xx = x; xx < x + item.width; xx++)
        {
            for (int yy = y; yy < y + item.height; yy++)
            {
                item.touchingSlots.Add(new Vector2(xx, yy));
            }
        }
        return retVal;

    }

    public bool phantomPlaceItemAtCoords(Vector2 coords, Item item)
    {
        return phantomPlaceItemAtCoords((int)coords.X, (int)coords.Y, item);
    }

    public bool phantomPlaceItemAtCoords(int x, int y, Item item)
    {

        item.invTopLeft = new Vector2((int)x, (int)y);
        for (int xx = x; xx < x + item.width; xx++)
        {
            for (int yy = y; yy < y + item.height; yy++)
            {
                item.touchingSlots.Add(new Vector2(xx, yy));
            }
        }
        return canPlaceItemAtCoords(x, y, item);

    }

    public bool placeItemAtCoords(Vector2 coords, Item item)
    {
        return placeItemAtCoords((int)coords.X, (int)coords.Y, item);
    }

    public bool placeItemAtCoords(int x, int y, Item item)
    {
        if (canPlaceItemAtCoords(x, y, item))
        {
            item.invTopLeft = new Vector2((int)x, (int)y);
            for (int xx = x; xx < x + item.width; xx++)
            {
                for (int yy = y; yy < y + item.height; yy++)
                {
                    items.Add(new Vector2(xx, yy), item);
                    item.touchingSlots.Add(new Vector2(xx, yy));
                }
            }
            return true;
        }
        return false;
    }

    public void moveItemFromTo(Vector2 from, Vector2 to)
    {
        removeItemAtCoords(from, out Item item);
        placeItemAtCoords(to, item);
    }

    public bool removeItemAtCoords(Vector2 coords, out Item item)
    {
        return removeItemAtCoords((int)coords.X, (int)coords.Y, out item);
    }

    public bool removeItemAtCoords(int x, int y, out Item item)
    {
        if (!items.TryGetValue(new Vector2(x, y), out item))
        {
            return false;
        }
        foreach (Vector2 slot in item.touchingSlots)
        {
            items.Remove(slot);
        }
        item.touchingSlots.Clear();
        //  item.invTopLeft = new Vector2(-1,-1);
        return true;
    }



    public bool isItemAtCoords(int x, int y)
    {
        return items.TryGetValue(new Vector2(x, y), out Item item);
    }


    public bool sortInventory()
    {
        return false;
    }

    public bool autoPlaceItem(Item item)
    {
        Vector2 placement = bruteSearch(item);
        if (placement == Vector2.Inf)
        {
            return false;
        }
        else
        {
            placeItemAtCoords(placement, item);
            return true;
        }
    }

    private Vector2 bruteSearch(Item item)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (canPlaceItemAtCoords(new Vector2(x, y), item))
                {
                    return new Vector2(x, y);
                }
            }
        }
        return Vector2.Inf;
    }

    public string printValues()
    {
        string retVal = "";
        foreach (Item item in items.Values)
        {
            retVal += item;
        }
        return retVal;
    }

    public string printKeys()
    {
        string retVal = "";
        foreach (Vector2 vec in items.Keys)
        {
            retVal += vec;
        }
        return retVal;
    }

    public void debugGen()
    {

        Equipable test = new Equipable(1f, 2, 2, "2x2Helmet", "testd", false, ResourceLoader.Load<Texture2D>("res://assets/items/helmet.png"));
        test.type = Equipable.EquipType.HELMET;
        placeItemAtCoords(5, 5, test);

        /*Sprite2D icon2 = new Sprite2D();
        icon2.Texture = ResourceLoader.Load<Texture2D>("res://assets/items/shirt.png");
        Equipment test2 = new Equipment(1, 2, 3, "test2", "test2d", false, icon2);
        test2.type = Equipment.EquipType.BODYARMOR;
        placeItemAtCoords(0, 0, test2);

        Sprite2D icon4 = new Sprite2D();
        icon4.Texture = ResourceLoader.Load<Texture2D>("res://assets/items/bandage.png");
        Consumable test4 = new Consumable(1, 1, 1, "test4", "test4d", false, icon4);
        placeItemAtCoords(8, 3, test4);*/



        Equipable test5 = new Equipable(1, 5, 2, "test5", "test5d", false, ResourceLoader.Load<Texture2D>("res://assets/items/longgun.png"));
        test5.type = Equipable.EquipType.GUNWEAPON;
        placeItemAtCoords(0, 0, test5);

    }


}
using Godot;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;

[GlobalClass]
public partial class Item : RigidBody3D, IEquatable<Item>
{
    internal static readonly Item NONE;

    [Export]
    public float physWeight; 

    [Export]
    public float weight;

    [Export]
    public int height;

    [Export]
    public int width;

    [Export]
    public string name;

    [Export]
    public string description;

    [Export]
    bool stackable;

    [Export]
    int count = 1;

    [Export]
    public Mesh mesh;

    [Export]
    public Texture2D icon;

    [Export]
    public CollisionShape3D collisionShape;



    public Vector2 invTopLeft{get; set;}
    public List<Vector2> touchingSlots = new List<Vector2>();

    public Item() { }
    public Item(float weight, int width, int height, string name, string description, bool stackable, Texture2D icon)
    {
        this.weight = weight;
        this.height = height;
        this.width = width;
        this.name = name;
        this.description = description;
        this.stackable = stackable;
        touchingSlots= new List<Vector2>();
        this.icon= icon;
        
    }

    public Item(int width,int height)
    {
        
        this.height = height;
        this.width = width;
        touchingSlots = new List<Vector2>();
    }

    public bool combineWith(Item item, out Item remain)
    {
        remain = Item.NONE;
        return false;
    }

    public override bool Equals(object obj)
    {
        //       
        // See the full list of guidelines at
        //   http://go.microsoft.com/fwlink/?LinkID=85237  
        // and also the guidance for operator== at
        //   http://go.microsoft.com/fwlink/?LinkId=85238
        //

        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        // TODO: write your implementation of Equals() here
        Item o = obj as Item;
        return o.GetInstanceId() == this.GetInstanceId();
          
    }

    // override object.GetHashCode
    public override int GetHashCode()
    {
        // TODO: write your implementation of GetHashCode() here
        return GetInstanceId().GetHashCode();
    }

    public bool Equals(Item other)
    {
        if (other == null)
        {
            return false;
        }
        return other.GetInstanceId() == this.GetInstanceId();
    }

    public Item shallowClone()
    {
        return (Item)MemberwiseClone();
    }

    public override string ToString()
    {
        return "Item{name:" + name + ",topleft:" + invTopLeft + "}";
    }

    internal static RigidBody3D generateRigidBody(Item item)
    {
        RigidBody3D temp = new();
        MeshInstance3D mesh = new();
        temp.AddChild(mesh);
        mesh.Mesh = item.mesh;
        mesh.CreateConvexCollision();
        return temp;
    }
}

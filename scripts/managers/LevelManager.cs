using Godot;
using System;

public partial class LevelManager : Node3D
{
    Node3D currentScene;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	
	
	}



    public bool loadScene(string uri)
    {
        try
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>(uri, "PackedScene", ResourceLoader.CacheMode.Reuse);
            currentScene = scene.Instantiate<Node3D>();
            AddChild(currentScene);
            return true;
        }
        catch
        {
            throw;
        }
    }

    public bool clearScenes()
    {
        foreach (Node3D scene in GetChildren())
        {
            scene.QueueFree();
        }
        return true;
    }

    public bool preloadScene(string uri)
    {
        try
        {
            PackedScene preloaded = ResourceLoader.Load<PackedScene>(uri, "PackedScene", ResourceLoader.CacheMode.Replace);
            return preloaded.CanInstantiate();
        }
        catch
        {
            return false;
        }

    }
}

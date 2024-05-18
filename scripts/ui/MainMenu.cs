using Godot;
using Steamworks;
using System;
using System.Reflection;

public partial class MainMenu : Control
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

        GetNode<Button>("Play").Pressed += playPressed;
        GetNode<Button>("Options").Pressed += optionsPressed;
        GetNode<Button>("Quit").Pressed += quitPressed;
    }

    private void quitPressed()
    {
        //TODO: Popup confirm
        GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
        GetTree().Quit();
    }

    private void optionsPressed()
    {
        GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
        throw new NotImplementedException();
    }

    private void playPressed()
    {
        GetNode<AudioStreamPlayer>("/root/main/uisfx").Play();
        GetNode<AudioStreamPlayer>("/root/main/music").Stop();
        //Global.NetworkManager.startServer();
        Global.NetworkManager.launchGame();




    }

    private void genPressed()
    {
        Global.LevelManager.loadScene("res://components/TerrainPlayer.tscn");
        Global.LevelManager.loadScene("res://scenes/TerrainGeneration.tscn");
        TerrainGeneration terrainGenerated = (TerrainGeneration)Global.LevelManager.currentScene;
        Image mapImage = Image.LoadFromFile("res://scripts/terrain/blur_test_gausbox.webp");
        terrainGenerated.AddTerrain("Terrain3D");
        Global.UIManager.clearUI();
    }

}

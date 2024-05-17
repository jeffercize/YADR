using Godot;
using System;
using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http.Headers;

public partial class WaitingRoom : Node3D
{
    /*
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        SteamLobby.lobbyUpdate += SteamLobby_lobbyUpdate;
        SteamLobby_lobbyUpdate("WaitingRoom_init",GetNode<SteamLobby>("/root/main/SteamLobby").lobbyMembers);
    }

    private async void SteamLobby_lobbyUpdate(string caller, List<Friend> list)
    {
        Global.debugLog("lobbyUpdate from " + caller);
        int i = 2;
        foreach (Friend f in list)
        {
            if (f.IsMe)
            {
                GetNode<Label3D>("Player1/Player1Label").Text = f.Name;
                Godot.Image avatar = await GetNode<Steam>("/root/Steam").getAvatar(f.Id);
                avatar.Resize(512, 512);
                GetNode<Sprite3D>("Player1/Player1Sprite").Texture = ImageTexture.CreateFromImage(avatar);
                GetNode<Node3D>("Player1").Visible = true;
            }
            else
            {
                GetNode<Label3D>("Player" + i + "/Player" + i + "Label").Text = f.Name;
                Godot.Image avatar = await GetNode<Steam>("/root/Steam").getAvatar(f.Id);
                avatar.Resize(512, 512);
                GetNode<Sprite3D>("Player" + i + "/Player" + i + "Sprite").Texture = ImageTexture.CreateFromImage(avatar);
                GetNode<Node3D>("Player"+i).Visible = true;
                i++;
                //put that guys shit in spot i
            }

        }
    }*/

}

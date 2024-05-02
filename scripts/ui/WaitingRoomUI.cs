using Godot;
using Steamworks.Data;
using Steamworks;
using System;
using Networking;

/// <summary>
/// Controls the UI elements on the Waiting Room Screen
/// </summary>
public partial class WaitingRoomUI : Control
{
    /// <summary>
    /// Subscribe to some events. If this is my lobby, I get cool buttons
    /// </summary>
    public override void _Ready()
    {
        Lobby lobby = GetNode<SteamLobby>("/root/main/SteamLobby").lobby;
        if (lobby.IsOwnedBy(SteamClient.SteamId))
        {
            GetNode<Button>("Panel/launch").Pressed += onLaunchPressed;
        }
        else
        {
            GetNode<Button>("Panel/launch").Disabled = true;
        }
        GetNode<Button>("Panel/invite").Pressed += _on_invite_pressed;
        GetNode<Button>("chat/send").Pressed += sendPressed;
        GetNode<Button>("Panel/host").Pressed += hostPressed;
        SteamConnectionManager.chatMessage += onChatMessage;
        SteamNetwork.serverHosted += onServerHosted;
        SteamMatchmaking.OnLobbyMemberJoined += onLobbyMemberJoined;
    }

    private void onChatMessage(ulong steamID, SteamNetwork.ChatMsg msg)
    {
        string sender = "ERROR";
        if (steamID == 0)
        {
            sender = "[INFO]";
        }
        else
        {
            sender = new Friend(steamID).Name;
        }
        GetNode<RichTextLabel>("chat/output/output").AppendText("[color=green]" + sender + "[/color]: " + msg.message + "\n");
    }

    private void onLobbyMemberJoined(Lobby arg1, Friend arg2)
    {
        onChatMessage(0, arg2.Name + "has joined the lobby!");
    }

    private void onServerHosted()
    {
        onChatMessage(0, "Server Hosted!");
        GetNode<Button>("Panel/invite").Disabled = false;
        GetNode<Button>("chat/send").Disabled = false;
        GetNode<Button>("Panel/host").Disabled = true;
    }

    private void onChatMessage(ulong steamID, string msg)
    {
    }

    private void hostPressed()
    {
        GetNode<Main>("/root/main").networkStart();

    }

    private void sendPressed()
    {
        GetNode<SteamNetwork>("/root/main/SteamNetwork").sendChatMessage(GetNode<LineEdit>("chat/enter").Text);
        GetNode<LineEdit>("chat/enter").Clear();
    }

    private void onLaunchPressed()
    {
        GetNode<Main>("/root/main").gameLaunch();
    }

    public void _on_invite_pressed()
    {
        //TODO Test/Fix this
        SteamFriends.OpenGameInviteOverlay(GetNode<SteamLobby>("/root/main/SteamLobby").lobbyId);
    }


}

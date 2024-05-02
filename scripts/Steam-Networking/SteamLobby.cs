using Godot;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;

/// <summary>
/// A Node that represents/controls a Steam Lobby
/// </summary>
public partial class SteamLobby : Control
{
    public int LOBBY_MAX_PLAYERS = 4;

    public readonly List<Friend> lobbyMembers = new List<Friend>();
    public Lobby lobby;
    public SteamId lobbyId;

    public delegate void lobbyUpdateEventHandler(string caller, List<Friend> list);
    public static event lobbyUpdateEventHandler lobbyUpdate;

    public override void _Ready()
    {
        connectSteamLobbySignals();
        SteamMatchmaking.CreateLobbyAsync(LOBBY_MAX_PLAYERS);
    }

    /// <summary>
    /// Just connects all the various built-in steam signals so we can make things happen.
    /// </summary>
    public void connectSteamLobbySignals()
    {
        SteamMatchmaking.OnChatMessage += SteamMatchmaking_OnChatMessage;
        SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
        SteamMatchmaking.OnLobbyDataChanged += SteamMatchmaking_OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged += SteamMatchmaking_OnLobbyMemberDataChanged;
        SteamMatchmaking.OnLobbyMemberJoined += SteamMatchmaking_OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += SteamMatchmaking_OnLobbyMemberLeave;
        SteamMatchmaking.OnLobbyInvite += SteamMatchmaking_OnLobbyInvite;
        SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
        SteamFriends.OnPersonaStateChange += SteamFriends_OnPersonaStateChange;
        SteamFriends.OnGameLobbyJoinRequested += SteamFriends_OnGameLobbyJoinRequested;
    }

    /// <summary>
    /// Called whenever you enter a lobby.
    /// </summary>
    /// <param name="obj"></param>
    private void SteamMatchmaking_OnLobbyEntered(Lobby obj)
    {
        Steam.debugLog("I just joined a lobby!");
        lobbyMembers.Add(new Friend(SteamClient.SteamId));
        foreach (Friend f in obj.Members)
        {
            if (!f.IsMe)
            {
                lobbyMembers.Add(f);
            }
        }
        Steam.debugLog("Lobby member count: " + obj.MemberCount);
        if (obj.GetData("gamestart").Equals("true") && !obj.Owner.IsMe)
        {
            Steam.debugLog("Got the word. Game started by host.");
            GetNode<Main>("/root/main").networkStart();
        }
        lobbyUpdate?.Invoke("enter", lobbyMembers);
    }

    /// <summary>
    /// Called whenever anyone leaves a lobby. If its me thats leaving, nuke the lobby stuff and start over with a new lobby with me as the host,.
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    private void SteamMatchmaking_OnLobbyMemberLeave(Lobby arg1, Friend arg2)
    {
        if (arg2.IsMe)
        {
            SteamMatchmaking.CreateLobbyAsync(4);
            lobbyMembers.Clear();
        }
        else
        {
            lobbyMembers.Remove(arg2);
            Steam.debugLog("Lobby Member Left: " + arg2.Name);
        }
        lobbyUpdate?.Invoke("leave", lobbyMembers);
    }

    /// <summary>
    /// In theory this should get called when someone changes their steam state.
    /// </summary>
    /// <param name="obj"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void SteamFriends_OnPersonaStateChange(Friend obj)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Should be called whenever you receieve an invite. In theory we shouldnt have to actually do anything until it is accepted.
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void SteamMatchmaking_OnLobbyInvite(Friend arg1, Steamworks.Data.Lobby arg2)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Called when someone joins the lobby I'm in. Also called after creating a lobby (you join it after creating it)
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    public void SteamMatchmaking_OnLobbyMemberJoined(Steamworks.Data.Lobby arg1, Friend arg2)
    {
        lobbyMembers.Add(arg2);
        Steam.debugLog("Lobby Member Joined: " + arg2.Name);
        lobbyUpdate?.Invoke("join", lobbyMembers);
    }

    /// <summary>
    /// Currently unused
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    private void SteamMatchmaking_OnLobbyMemberDataChanged(Steamworks.Data.Lobby arg1, Friend arg2)
    {
        Steam.debugLog("Lobby Member Changed Data: " + arg2.Name);
    }

    /// <summary>
    /// Called whenever the Lobby data changes. Used to trigger networking connections atm.
    /// </summary>
    /// <param name="obj"></param>
    private void SteamMatchmaking_OnLobbyDataChanged(Steamworks.Data.Lobby obj)
    {
        if (obj.Owner.IsMe)
        {
            return;
        }
        if (obj.GetData("gamestart").Equals("true") && !obj.Owner.IsMe)
        {
            Steam.debugLog("Got the word. Game started by host.");
            GetNode<Main>("/root/main").networkStart();
        }
    }

    /// <summary>
    /// Called after a lobby is successfully created. The parameter lobby is a reference to the created lobby
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    private void SteamMatchmaking_OnLobbyCreated(Result arg1, Steamworks.Data.Lobby arg2)
    {
        Steam.debugLog("Lobby Created: " + arg1.ToString());
        lobby = arg2;
        lobbyId = lobby.Id;
        arg2.SetFriendsOnly();
        arg2.SetJoinable(true);
        Steam.debugLog("Lobby is now joinable");
    }

    /// <summary>
    /// Currently unused. (Using my own chat)
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    /// <param name="arg3"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void SteamMatchmaking_OnChatMessage(Steamworks.Data.Lobby arg1, Friend arg2, string arg3)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// This should trigger after you've accepted an invite from arg2 to join arg1.
    /// </summary>
    /// <param name="arg1"></param>
    /// <param name="arg2"></param>
    private void SteamFriends_OnGameLobbyJoinRequested(Steamworks.Data.Lobby arg1, SteamId arg2)
    {
        //TODO: Goto Play Screen
        SteamMatchmaking.JoinLobbyAsync(arg1.Id);
    }



}

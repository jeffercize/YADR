using Godot;
using System;

/// <summary>
/// Just does the fun little camera movements in the waiting room.
/// </summary>
public partial class WaitingRoomCam : Camera3D
{
    float smoothSpeed = 0.05F;
    float camTransSpeed = .02F;
    float smoothAmount = 5f;
    float countPosMod = 1.5f;
    float scaledx;
    float scaledy;
    Vector3 basePos;
    float newPos;

    public override void _Ready()
    {
        basePos = Position;
        SteamLobby.lobbyUpdate += SteamLobby_lobbyUpdate;
        SteamLobby_lobbyUpdate("WaitingRoomCam_init", GetNode<SteamLobby>("/root/main/SteamLobby").lobbyMembers);
    }

    private void SteamLobby_lobbyUpdate(string caller, System.Collections.Generic.List<Steamworks.Friend> list)
    {
        newPos = basePos.Z - countPosMod * (list.Count - 1);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion e)
        {
            float xpercent = e.GlobalPosition.X / GetTree().Root.Size.X;
            float ypercent = e.GlobalPosition.Y / GetTree().Root.Size.Y;
            scaledx = (float)((xpercent - 0.5) * smoothAmount);
            scaledy = (float)((ypercent - 0.5) * smoothAmount);
        }
    }

    public override void _Process(double delta)
    {
        RotationDegrees = new Vector3(Mathf.Lerp(RotationDegrees.X, 0 + scaledy, smoothSpeed), Mathf.Lerp(RotationDegrees.Y, 90 + scaledx, smoothSpeed), 0);
        Position = new Vector3(Position.X, Position.Y, Mathf.Lerp(Position.Z, newPos, camTransSpeed));
    }
}

using Godot;
using Steamworks;
using System.Collections.Generic;

public partial class PlayerInputData
{

    public Vector2 direction = new(0f, 0f);
    public Vector2 lookDelta = new(0f, 0f);
    public Vector2 lookDirection = new(0f, 0f);
    public Dictionary<InputManager.ActionEnum,bool> actionStates = new Dictionary<InputManager.ActionEnum, bool>();

}
﻿using Godot;
using NetworkMessages;
using Steamworks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class PlayerInputData
{

    public Vector2 direction = new(0f, 0f);
    public Vector2 lookDelta = new(0f, 0f);
    public Vector3 lookDirection = new(0f, 0f, 0f);
    public Dictionary<ActionType,ActionState> actionStates = new Dictionary<ActionType, ActionState>();

}
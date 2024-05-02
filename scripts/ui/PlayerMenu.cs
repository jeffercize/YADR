using Godot;
using System;

public partial class PlayerMenu : Control
{ 

    private gameUI gameUI { get; set; }

    public override void _Ready()
    {
        gameUI = GetParent<gameUI>();
        Visible = false;
    }

}

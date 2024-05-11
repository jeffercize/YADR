using Godot;
using System;
using System.Collections.Generic;

public partial class LODMultiMeshInstance3D : MultiMeshInstance3D
{
    public MultiMeshInstance3D lowLODParent { get; set; }
    public MultiMeshInstance3D ultraLowLODParent { get; set; }
    public bool isLowLODParent { get; set; }
    public bool isUltraLowLODParent { get; set; }
    public List<MultiMeshInstance3D> lowLODChildren { get; set; }
    public List<MultiMeshInstance3D> highLODChildren { get; set; }

    public LODMultiMeshInstance3D()
    {
        highLODChildren = new List<MultiMeshInstance3D>();
        lowLODChildren = new List<MultiMeshInstance3D>();
    }
}
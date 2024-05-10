using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
public partial class GrassMeshMaker : Node
{
    float totalTime = 0.0f;
    List<LODMultiMeshInstance3D> grassClumps = new List<LODMultiMeshInstance3D>();
    Mesh highLODGrassBlade;
    Mesh lowLODGrassBlade;
    CharacterBody3D player;
    Thread shaderParamThread;

    private Mesh CreateHighLODGrassBlade(float grassWidth, float grassHeight)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        { //BE VERY MINDFUL CHANGING THE MESH AS THE SHADER NEEDS IT PASSED IN
            // First rectangle
            new Vector3(grassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(.195f, .2f, 0), // Top-right corner
            new Vector3(.005f, .2f, 0), // Top-left corner

            new Vector3(.19f, .4f, 0), // Top-right corner
            new Vector3(.01f, .4f, 0), // Top-left corner

            new Vector3(.18f, .6f, 0), // Top-right corner
            new Vector3(.02f, .6f, 0), // Top-left corner

            new Vector3(.17f, .8f, 0), // Top-right corner
            new Vector3(.03f, .8f, 0), // Top-left corner

            new Vector3(.155f, 1, 0), // Top-right corner
            new Vector3(.045f, 1, 0), // Top-left corner

            new Vector3(0.13f, 1.2f, 0), // Top-right corner
            new Vector3(0.07f, 1.2f, 0), // Top-left corner

            new Vector3(0.1f, grassHeight, 0) // Top corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
        GD.Print("Made the mesh");
        int index = 0;
        foreach (Vector3 vec in highLODVertices)
        {
            st.AddVertex(vec);
            if (index >= 3 && index % 2 != 0)
            {
                // Add two triangles to form a square (front face)
                st.AddIndex(index - 3);
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);

                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
                st.AddIndex(index);

                // Add two triangles to form a square (back face)
                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
                st.AddIndex(index - 3);

                st.AddIndex(index);
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);
            }
            else if (index == highLODVertices.Length - 1)
            {
                // Add the final triangle at the top (front face)
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);
                st.AddIndex(index);

                // Add the final triangle at the top (back face)
                st.AddIndex(index);
                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
            }
            index++;
        }
        st.GenerateNormals();
        return st.Commit();
    }

    private Mesh CreateLowLODGrassBlade(float grassWidth, float grassHeight)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        { //BE VERY MINDFUL CHANGING THE MESH AS THE SHADER NEEDS IT PASSED IN
            // First rectangle
            new Vector3(grassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(grassWidth, grassHeight, 0), // Top-right corner
            new Vector3(.0f, grassHeight, 0), // Top-left corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
        GD.Print("Made the mesh");
        int index = 0;
        foreach (Vector3 vec in highLODVertices)
        {
            st.AddVertex(vec);
        }
        // Add two triangles to form a square (front face)
        st.AddIndex(0);
        st.AddIndex(1);
        st.AddIndex(2);

        st.AddIndex(2);
        st.AddIndex(1);
        st.AddIndex(3);

        // Add two triangles to form a square (back face)
        st.AddIndex(2);
        st.AddIndex(1);
        st.AddIndex(0);

        st.AddIndex(3);
        st.AddIndex(1);
        st.AddIndex(2);
        st.GenerateNormals();
        return st.Commit();
    }
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        player = GetTree().CurrentScene.GetNode<CharacterBody3D>("Player");
        GD.Print("Hi welcome");
        Random rand = new Random();
        MultiMeshInstance3D grassChunk =  (MultiMeshInstance3D)GetNode("GrassChunk");
        float grassWidth = 0.2f;
        float grassHeight = 1.4f;
        highLODGrassBlade = CreateHighLODGrassBlade(grassWidth, grassHeight);
        lowLODGrassBlade = CreateLowLODGrassBlade(grassWidth, grassHeight);
        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://scripts/terrain/grass/grassShader.gdshader");
        grassMat.Shader = grassShader;

        int rowCount = 32;
        int columnCount = rowCount; // Assuming a square grid
        for (int row = 0; row < rowCount; row += 2)
        {
            for (int column = 0; column < columnCount; column += 2)
            {
                MultiMeshInstance3D temp = grassChunk.Duplicate() as MultiMeshInstance3D;
                LODMultiMeshInstance3D lowLODGrassClump = new LODMultiMeshInstance3D
                {
                    Multimesh = temp.Multimesh.Duplicate() as MultiMesh, // Replace with your low LOD mesh
                    Transform = new Transform3D(Basis.Identity, new Vector3(row * 12.5f, 0, column * 12.5f)),
                    MaterialOverride = grassMat,
                    isLowLODParent = true
                };
                grassClumps.Add(lowLODGrassClump);
                MultiMesh lowLODGrassMultiMesh = lowLODGrassClump.Multimesh;
                lowLODGrassMultiMesh.Mesh = lowLODGrassBlade;
                lowLODGrassClump.Basis = Basis.Identity;
                lowLODGrassMultiMesh.InstanceCount = 4048;
                lowLODGrassMultiMesh.VisibleInstanceCount = 4048;
                lowLODGrassClump.Multimesh = lowLODGrassMultiMesh;
                lowLODGrassClump.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                lowLODGrassClump.MaterialOverride = grassMat;
                int rowLength = (int)Math.Sqrt(4048);
                for (int k = 0; k < lowLODGrassMultiMesh.VisibleInstanceCount; k++)
                {
                    float x_loc = k % rowLength - rowLength / 2;
                    float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                    float y_loc = k / rowLength - rowLength / 2;
                    float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                    float density = 2.5f;
                    lowLODGrassMultiMesh.SetInstanceTransform(k, new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter) / density, 0, (y_loc + y_jitter) / density)));
                }
                lowLODGrassClump.Transform = new Transform3D(Basis.Identity, new Vector3((row+0.5f) * 12.5f, 0, (column+0.5f) * 12.5f));
                ((ShaderMaterial)lowLODGrassClump.MaterialOverride).SetShaderParameter("grassTotalWidth", grassWidth);
                ((ShaderMaterial)lowLODGrassClump.MaterialOverride).SetShaderParameter("grassTotalHeight", grassHeight);
                AddChild(lowLODGrassClump);

                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        if (row + i < rowCount && column + j < columnCount)
                        {
                            MultiMeshInstance3D tempHighLOD = grassChunk.Duplicate() as MultiMeshInstance3D;
                            int ind = (row + i) * columnCount + (column + j);
                            LODMultiMeshInstance3D newTemp = new LODMultiMeshInstance3D
                            {
                                Multimesh = tempHighLOD.Multimesh,
                                Transform = tempHighLOD.Transform,
                                MaterialOverride = tempHighLOD.MaterialOverride,
                                lowLODParent = lowLODGrassClump
                            };
                            grassClumps.Add(newTemp);
                            lowLODGrassClump.highLODChildren.Add(newTemp);

                            MultiMesh grassMultiMesh = newTemp.Multimesh;
                            grassMultiMesh.Mesh = highLODGrassBlade;
                            newTemp.Basis = Basis.Identity;
                            grassMultiMesh.InstanceCount = 4048;
                            grassMultiMesh.VisibleInstanceCount = 4048;
                            newTemp.Multimesh = grassMultiMesh;
                            newTemp.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                            newTemp.MaterialOverride = grassMat;
                            rowLength = (int)Math.Sqrt(4048);
                            for (int k = 0; k < grassMultiMesh.VisibleInstanceCount; k++)
                            {
                                float x_loc = k % rowLength - rowLength / 2;
                                float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                                float y_loc = k / rowLength - rowLength / 2;
                                float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                                float density = 5.0f;
                                grassMultiMesh.SetInstanceTransform(k, new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter) / density, 0, (y_loc + y_jitter) / density)));
                            }
                            newTemp.Transform = new Transform3D(Basis.Identity, new Vector3((row + i) * 12.5f, 0, (column + j) * 12.5f));
                            ((ShaderMaterial)newTemp.MaterialOverride).SetShaderParameter("grassTotalWidth", grassWidth);
                            ((ShaderMaterial)newTemp.MaterialOverride).SetShaderParameter("grassTotalHeight", grassHeight);
                            // Add the new instance to the scene
                            newTemp.Visible = false;
                            AddChild(newTemp);
                        }
                    }
                }
            }
        }
        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff(grassClumps);

                Thread.Sleep(15); // Wait for 15 milliseconds
            }
        });

        shaderParamThread.Start();


        GD.Print("Set the Visibles");
    }
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        // Assume cameraPosition is the position of your camera or any other point you want to measure distance from
        totalTime += (float)delta;
        //GD.Print(grassClumps.Count);
        foreach (var clump in grassClumps)
        {
            LODManagement(clump);
        }
    }

    public void LODManagement(LODMultiMeshInstance3D clump)
    {
        Vector3 playerPosition = player.GlobalTransform.Origin;
        float distance = clump.GlobalTransform.Origin.DistanceTo(playerPosition);
        //GD.Print(distance);
        // If the distance is greater than a certain threshold, switch to the low LOD mesh
        if (clump.isLowLODParent && !clump.Visible && distance > 50.0f)
        {
            //GD.Print("Make parent seen, hide kids");
            foreach (var child in clump.highLODChildren)
            {
                child.Visible = false;
            }
            clump.Visible = true;
        }
        // Otherwise, use the high LOD mesh
        else if (clump.isLowLODParent && clump.Visible && distance <= 50.0f)
        {
            //GD.Print("Make kids seen, hide parent");
            foreach (var child in clump.highLODChildren)
            {
                child.Visible = true;
            }
            clump.Visible = false;
        }

    }

    public void SetShaderStuff(List<LODMultiMeshInstance3D> grassClumps)
    {
        foreach (var clump in grassClumps)
        {
            ((ShaderMaterial)clump.MaterialOverride).SetShaderParameter("time", totalTime);
        }
    }
    
}

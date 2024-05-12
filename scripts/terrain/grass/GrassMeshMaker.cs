using Godot;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using static Godot.OpenXRInterface;
using System.Diagnostics;

public partial class GrassMeshMaker : Node
{
    float totalTime = 0.0f;
    int rowCount = 15;
    int columnCount = 15;
    List<MultiMeshInstance3D> grassClumps = new List<MultiMeshInstance3D>();
    List<MultiMeshInstance3D> lowLODGrassClumps = new List<MultiMeshInstance3D>();
    List<MultiMeshInstance3D> mediumLODGrassClumps = new List<MultiMeshInstance3D>();
    List<MultiMeshInstance3D> highLODGrassClumps = new List<MultiMeshInstance3D>();

    CharacterBody3D player;
    Thread shaderParamThread;

    private readonly object highLODGrassClumpsLock = new object();
    private readonly object mediumLODGrassClumpsLock = new object();
    private readonly object lowLODGrassClumpsLock = new object();

    private readonly object grassClumpsLock = new object();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        //SetupGrass("Player");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        totalTime += (float)delta;
        //UpdateGrassClumps();
    }

    public delegate void NodeReadyToBeAddedHandler(Node node);
    public static event NodeReadyToBeAddedHandler NodeReadyToBeAdded = delegate { };

    public void someFunction()
    {
        Node node = new Node();
        NodeReadyToBeAdded.Invoke(node);
    }

    public override void _PhysicsProcess(double delta)
    {
    }

    public void SetupGrass(String target, Image heightMap)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        //player = GetTree().CurrentScene.GetNode<CharacterBody3D>(target);

        MultiMeshInstance3D grassChunk = (MultiMeshInstance3D)GetNode("GrassChunk");
        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://scripts/terrain/grass/grassShader.gdshader");
        grassMat.Shader = grassShader;

        ImageTexture heightmapTexture = ImageTexture.CreateFromImage(heightMap);
        GD.Print(heightmapTexture.GetFormat());

        float grassWidth = 0.1f;
        float grassHeight = 0.5f;
        //TODO make a GrassMesh class that holds its width and height
        Mesh highLODGrassBlade = CreateHighLODGrassBlade(grassWidth, grassHeight);
        Mesh mediumLODGrassBlade = CreateMediumLODGrassBlade(grassWidth*2, grassHeight); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        Mesh lowLODGrassBlade = CreateLowLODGrassBlade(grassWidth*4, grassHeight); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        
        Stopwatch stopwatch2 = Stopwatch.StartNew();
        int numThreads = 6;
        Thread[] threads = new Thread[numThreads];

        for (int t = 0; t < numThreads / 3; t++)
        {
            threads[t] = new Thread(() => InitializeGrassClumpsThreaded(grassMat, lowLODGrassBlade, 2, grassWidth * 4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 1024, heightMap.GetWidth() / 30, heightMap.GetHeight() / 30, 30, 30));
            threads[t].Start();
        }
        for (int t = numThreads / 3; t < 2 * numThreads / 3; t++)
        {
            threads[t] = new Thread(() => InitializeGrassClumpsThreaded(grassMat, mediumLODGrassBlade, 1, grassWidth * 4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 2048, heightMap.GetWidth() / 30, heightMap.GetHeight() / 30, 30, 30));
            threads[t].Start();
        }
        for (int t = 2 * numThreads / 3; t < numThreads; t++)
        {
            threads[t] = new Thread(() => InitializeGrassClumpsThreaded(grassMat, highLODGrassBlade, 0, grassWidth * 4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 4096, heightMap.GetWidth() / 30, heightMap.GetHeight() / 30, 30, 30));
            threads[t].Start();
        }

        /*        for (int i = 0; i < heightMap.GetWidth()/30; i++)
                {
                    for (int j = 0; j < heightMap.GetHeight()/30; j++)
                    {
                        InitializeGrassClump(grassMat, lowLODGrassBlade, grassWidth*4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 1024, i, j, 30, 30); //lowLOD
                        InitializeGrassClump(grassMat, mediumLODGrassBlade, grassWidth * 4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 2048, i, j, 30, 30); //mediumLOD
                        InitializeGrassClump(grassMat, highLODGrassBlade, grassWidth * 4, grassHeight, heightmapTexture, heightMap.GetWidth(), heightMap.GetHeight(), 4096, i, j, 30, 30); //highLOD
                    }
                }*/
        GD.Print($"Init Grass Time elapsed: {stopwatch2.Elapsed}");
        GD.Print($"Average Time Taken: {stopwatch2.Elapsed/((heightMap.GetWidth() / 30) * (heightMap.GetHeight() / 30))}");

        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff(grassClumps);

                Thread.Sleep(16); // Wait for 16 milliseconds
            }
        });
        shaderParamThread.Start();
        GD.Print($"Setup Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetShaderStuff(List<MultiMeshInstance3D> grassClumps)
    {
        for (int i = 0; i < grassClumps.Count; i++)
        {
            ((ShaderMaterial)grassClumps[i].MaterialOverride).SetShaderParameter("time", totalTime);
        }
    }

    public void InitializeGrassClump(ShaderMaterial grassMat, Mesh grassBlade, float grassWidth, float grassHeight, ImageTexture heightMapTexture, int mapWidth, int mapHeight, int instanceCount = 2024, int i = 0, int j = 0, float fieldWidth = 15f, float fieldHeight = 15f)
    {

        Random rand = new Random();
        MultiMeshInstance3D grassChunk = new MultiMeshInstance3D();

        MultiMesh grassMultiMesh = new MultiMesh();
        grassMultiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        grassMultiMesh.Mesh = grassBlade;
        grassMultiMesh.InstanceCount = instanceCount;
        grassMultiMesh.VisibleInstanceCount = instanceCount;

        grassChunk.Multimesh = grassMultiMesh;
        grassChunk.Basis = Basis.Identity;
        grassChunk.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        grassChunk.MaterialOverride = grassMat.Duplicate() as ShaderMaterial;

        int rowLength = (int)Math.Sqrt(instanceCount);
        for (int k = 0; k < grassMultiMesh.VisibleInstanceCount; k++)
        {
            float x_loc = (k % rowLength) / (float)rowLength * fieldWidth - fieldWidth / 2;
            float y_loc = (k / rowLength) / (float)rowLength * fieldHeight - fieldHeight / 2;
            float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
            float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
            grassMultiMesh.SetInstanceTransform(k, new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter), 0, (y_loc + y_jitter))));
        }
        grassChunk.Transform = new Transform3D(Basis.Identity, new Vector3(i * fieldWidth, 0, j * fieldHeight));
        ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("grassTotalWidth", grassWidth);
        ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("grassTotalHeight", grassHeight);

        ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("heightMap", heightMapTexture);
        ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));

        // Add the new instance to the scene
        highLODGrassClumps.Add(grassChunk); //oppsie fix this TODO
        grassClumps.Add(grassChunk);
        //AddChild(grassChunk);
        if(instanceCount > 2000)
        {
            grassChunk.Visible = false;
        }
    }

    public void InitializeGrassClumpsThreaded(ShaderMaterial grassMat, Mesh grassBlade, int LOD_ENUM, float grassWidth, float grassHeight, ImageTexture heightMapTexture, int mapWidth, int mapHeight, int instanceCount = 2024, int width = 0, int height = 0, float fieldWidth = 15f, float fieldHeight = 15f)
    {
        for (int i = 0; i < width / 30; i++)
        {
            for (int j = 0; j < height / 30; j++)
            {
                Random rand = new Random();
                MultiMeshInstance3D grassChunk = new MultiMeshInstance3D();

                MultiMesh grassMultiMesh = new MultiMesh();
                grassMultiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
                grassMultiMesh.Mesh = grassBlade;
                grassMultiMesh.InstanceCount = instanceCount;
                grassMultiMesh.VisibleInstanceCount = instanceCount;

                grassChunk.Multimesh = grassMultiMesh;
                grassChunk.Basis = Basis.Identity;
                grassChunk.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                grassChunk.MaterialOverride = grassMat.Duplicate() as ShaderMaterial;

                int rowLength = (int)Math.Sqrt(instanceCount);
                for (int k = 0; k < grassMultiMesh.VisibleInstanceCount; k++)
                {
                    float x_loc = (k % rowLength) / (float)rowLength * fieldWidth - fieldWidth / 2;
                    float y_loc = (k / rowLength) / (float)rowLength * fieldHeight - fieldHeight / 2;
                    float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                    float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
                    grassMultiMesh.SetInstanceTransform(k, new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter), 0, (y_loc + y_jitter))));
                }
                grassChunk.Transform = new Transform3D(Basis.Identity, new Vector3(i * fieldWidth, 0, j * fieldHeight));
                ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("grassTotalWidth", grassWidth);
                ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("grassTotalHeight", grassHeight);

                ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("heightMap", heightMapTexture);
                ((ShaderMaterial)grassChunk.MaterialOverride).SetShaderParameter("heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));

                // Add the new instance to the scene
                if(LOD_ENUM == 0)
                {
                    lock (highLODGrassClumpsLock)
                    {
                        highLODGrassClumps.Add(grassChunk);
                    }
                }
                else if(LOD_ENUM == 2)
                {
                    lock (mediumLODGrassClumpsLock)
                    {
                        mediumLODGrassClumps.Add(grassChunk);
                    }
                }
                else if (LOD_ENUM == 3)
                {
                    lock (lowLODGrassClumpsLock)
                    {
                        lowLODGrassClumps.Add(grassChunk);
                    }
                }


                lock (grassClumpsLock)
                {
                    grassClumps.Add(grassChunk);
                }
                //AddChild(grassChunk);
                if (instanceCount > 2000)
                {
                    grassChunk.Visible = false;
                }
            }
        }
    }

    private Mesh CreateHighLODGrassBlade(float myGrassWidth, float myGrassHeight)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.975f, myGrassHeight * 0.1f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.025f, myGrassHeight * 0.1f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.95f, myGrassHeight * 0.2f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.05f, myGrassHeight * 0.2f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.3f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.3f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.85f, myGrassHeight * 0.4f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.4f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.8f, myGrassHeight * 0.5f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.2f, myGrassHeight * 0.5f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.7f, myGrassHeight * 0.6f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.3f, myGrassHeight * 0.6f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.5f, myGrassHeight, 0) // Top corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
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

    //mediumLODGrass is broken right now
    private Mesh CreateMediumLODGrassBlade(float myGrassWidth, float myGrassHeight)
    {
        myGrassWidth = myGrassWidth * 2;
        return CreateHighLODGrassBlade(myGrassWidth, myGrassHeight);
    }


    private Mesh CreateLowLODGrassBlade(float myGrassWidth, float myGrassHeight)
    {
        myGrassWidth = myGrassWidth * 4;
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth*2, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth*2, myGrassHeight, 0), // Top-right corner
            new Vector3(.0f, myGrassHeight, 0), // Top-left corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
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

}
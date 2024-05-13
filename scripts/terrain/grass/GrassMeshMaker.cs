using Godot;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using static Godot.OpenXRInterface;
using System.Diagnostics;
using System.Security.Cryptography;

public partial class GrassMeshMaker : Node3D
{
    float totalTime = 0.0f;
    int rowCount = 15;
    int columnCount = 15;
    List<Rid> grassClumps = new List<Rid>();
    List<Rid> lowLODGrassClumps = new List<Rid>();
    List<Rid> mediumLODGrassClumps = new List<Rid>();
    List<Rid> highLODGrassClumps = new List<Rid>();

    Mesh highLODMesh;
    Mesh mediumLODMesh;
    Mesh lowLODMesh;

    ImageTexture heightMapTexture;

    CharacterBody3D player;
    Thread shaderParamThread;

    private readonly object highLODGrassClumpsLock = new object();
    private readonly object mediumLODGrassClumpsLock = new object();
    private readonly object lowLODGrassClumpsLock = new object();

    private readonly object grassClumpsLock = new object();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        int width = 8192;
        int height = 4096;
        Image testImg = Image.Create(width, height, false, Image.Format.Rgb8);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float gradient = (float)x / (width/10 - 1); // Calculate the gradient value based on the x-coordinate
                Color color = new Color(gradient, 0, 0); // Create a color from the gradient value
                testImg.SetPixel(x, y, color); // Set the pixel color
            }
        }
        //testImg.FlipY(); // Flip the image vertically because Godot's coordinate system starts from the top-left corner
        //SetupGrass("Player", testImg);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        totalTime += (float)delta;
        //UpdateGrassClumps();
    }

    public override void _PhysicsProcess(double delta)
    {
    }

    public void SetupGrass(String target, Image heightMap)
    {
        heightMap.SavePng("C:\\Users\\jeffe\\test_images\\bruh_test.png");
        Stopwatch stopwatch = Stopwatch.StartNew();
        //player = GetTree().CurrentScene.GetNode<CharacterBody3D>(target);

        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://scripts/terrain/grass/grassShader.gdshader");
        grassMat.Shader = grassShader;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);
        GD.Print(heightMapTexture.GetWidth());
        GD.Print(heightMapTexture.GetHeight());

        float grassWidth = 0.1f;
        float grassHeight = 0.5f;
        //TODO make a GrassMesh class that holds its width and height
        highLODMesh = CreateHighLODGrassBlade(grassWidth, grassHeight, grassMat);
        Rid mediumLODGrassBlade = CreateMediumLODGrassBlade(grassWidth*2, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        Rid lowLODGrassBlade = CreateLowLODGrassBlade(grassWidth*4, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles

        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        // Create a RID for the material and set its shader
        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);

        // Set the shader parameters
        GD.Print("here?");
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", grassWidth);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(grassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        GD.Print("there?");
        RenderingServer.MaterialSetParam(grassMaterial, "heightMap", heightMapTexture.GetRid());
        GD.Print("what?");

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(highLODMesh.GetRid(), 0, grassMaterial);
        int instanceCount = 4096;
        int rowLength = (int)Math.Sqrt(instanceCount);
        int widthIndex = 0;
        int heightIndex = 0;
        Random rand = new Random();
        int randomSeed = rand.Next(int.MaxValue-1000000); //subtract 1 mil because I'm scared of int overflow in InitializeRenderServerGrassClump
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 1, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 2, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 3, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 4, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 5, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 6, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 7, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 8, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 9, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 10, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 11, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 12, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 12, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 13, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 14, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 15, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 16, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 17, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 18, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 19, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 20, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 21, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 22, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);

        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 1, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 2, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 3, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 4, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 5, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 6, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 7, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 8, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 9, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 10, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 11, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 12, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 12, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 13, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 14, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 15, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 16, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 17, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 18, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 19, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 20, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 21, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);
        InitializeRenderServerGrassClump(grassMat, highLODMesh, grassWidth, grassHeight, widthIndex + 22, heightIndex + 100, rowLength, 4096, heightMap.GetWidth(), heightMap.GetHeight(), 30, 30, randomSeed);


        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds TODO maybe speed up
            }
        });
        shaderParamThread.Start();
        GD.Print($"Setup Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }

    public void InitializeRenderServerGrassClump(ShaderMaterial grassMat, Mesh grassBlade, float grassWidth, float grassHeight, int widthIndex, int heightIndex, int rowLength = 25, int instanceCount = 2024, int width = 0, int height = 0, float fieldWidth = 15f, float fieldHeight = 15f, int randomSeed = 0)
    {
        Stopwatch stopwatch3 = Stopwatch.StartNew();
        //lookup cantor pairing functions, the result will apparently always be unique for all combinations of width and height index
        Random rand = new Random(randomSeed + ((widthIndex + heightIndex) * (widthIndex + heightIndex + 1) / 2 + heightIndex)); 
        //GD.Print($"begin make multimesh: {stopwatch3.Elapsed}");
        Rid grassChunk = RenderingServer.MultimeshCreate();
        // Create a RID for the shader and set its code
        RenderingServer.MultimeshSetMesh(grassChunk, grassBlade.GetRid());
        // Create a new array to hold the transform data for all instances
        //GD.Print($"declare array size: {stopwatch3.Elapsed}");
        float[] instanceData = new float[12 * instanceCount];
        //GD.Print($"pre-array: {stopwatch3.Elapsed}");
        // Fill the array with the transform data for each instance
        for (int k = 0; k < instanceCount; k++)
        {
            float x_loc = (k % rowLength) / (float)rowLength * fieldWidth - fieldWidth / 2;
            float y_loc = (k / rowLength) / (float)rowLength * fieldHeight - fieldHeight / 2;
            float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
            float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;

            // Create a new transform for this instance
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter), 0, (y_loc + y_jitter)));

            // Add the transform data to the array
            instanceData[k * 12 + 0] = transform.Basis.X.X;
            instanceData[k * 12 + 1] = transform.Basis.X.Y;
            instanceData[k * 12 + 2] = transform.Basis.X.Z;
            instanceData[k * 12 + 3] = transform.Origin.X;
            instanceData[k * 12 + 4] = transform.Basis.Y.X;
            instanceData[k * 12 + 5] = transform.Basis.Y.Y;
            instanceData[k * 12 + 6] = transform.Basis.Y.Z;
            instanceData[k * 12 + 7] = transform.Origin.Y;
            instanceData[k * 12 + 8] = transform.Basis.Z.X;
            instanceData[k * 12 + 9] = transform.Basis.Z.Y;
            instanceData[k * 12 + 10] = transform.Basis.Z.Z;
            instanceData[k * 12 + 11] = transform.Origin.Z;
        }
        //GD.Print($"post-array: {stopwatch3.Elapsed}");
        // Set the buffer data for the MultiMesh
        RenderingServer.MultimeshAllocateData(grassChunk, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false);
        RenderingServer.MultimeshSetBuffer(grassChunk, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(grassChunk, instanceCount);

        // Create a new instance for the multimesh
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        //RenderingServer.InstanceSetCustomAabb() //TODO
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(widthIndex * fieldWidth, 0, heightIndex * fieldHeight)));
    }

    private Mesh CreateHighLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
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
        highLODMesh = st.Commit();


        return highLODMesh;
    }

    //mediumLODGrass is broken right now
    private Rid CreateMediumLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.25f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.25f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.8f, myGrassHeight * 0.5f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.5f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.7f, myGrassHeight * 0.75f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.3f, myGrassHeight * 0.75f, 0), // Top-left corner

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
        mediumLODMesh = st.Commit();
        // Create a RID for the mesh and add the arrayMesh's surface to it
        Rid mediumLODGrassBlade = mediumLODMesh.GetRid();

        // Create a RID for the shader and set its code
        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        // Create a RID for the material and set its shader
        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);

        // Set the shader parameters
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", myGrassWidth);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", myGrassHeight);

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(mediumLODGrassBlade, 0, grassMaterial);

        return mediumLODGrassBlade;
    }


    private Rid CreateLowLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
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
        lowLODMesh = st.Commit();
        // Create a RID for the mesh and add the arrayMesh's surface to it
        Rid lowLODGrassBlade = lowLODMesh.GetRid();

        // Create a RID for the shader and set its code
        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        // Create a RID for the material and set its shader
        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);

        // Set the shader parameters
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", myGrassWidth);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", myGrassHeight);

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(lowLODGrassBlade, 0, grassMaterial);

        return lowLODGrassBlade;
    }

}
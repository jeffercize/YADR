using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public partial class GrassMeshMaker : Node3D
{
    float totalTime = 0.0f;
    int rowCount = 15;
    int columnCount = 15;
    ValueTuple<Rid, Rid>[,] grassChunks;
    ValueTuple<uint, Rid, uint, Rid>[,] grassChunkMultimeshs;
    List<Rid> activeGrassChunks = new List<Rid>();
    List<Rid> newActiveGrassChunks = new List<Rid>();

    int instanceCount = 2048;
    int rowLength = (int)Math.Sqrt(2048);
    int randomSeed;
    bool grassReady = false;

    Mesh highLODMesh;
    Mesh mediumLODMesh;
    Mesh lowLODMesh;

    ImageTexture heightMapTexture;
    Image heightMap;

    CharacterBody3D player;



    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Random rand = new Random();
        randomSeed = rand.Next(int.MaxValue - 1000000); //subtract 1 mil because we use it in math and I dont want to overflow

/*        int width = 8192;
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
        SetupGrass("Player", testImg);*/
    }

    public (float factor1, float factor2) CalculateFactors(float distance)
    {
        float factor1, factor2;

        if (distance >= 0 && distance <= 3)
        {
            factor1 = 1;
            factor2 = 1;
        }
        else if (distance > 3 && distance <= 5)
        {
            factor1 = 1;
            factor2 = 1 - ((distance - 3) / 2);
        }
        else if (distance > 5 && distance <= 7)
        {
            factor1 = 1 - ((distance - 5) / 2);
            factor2 = ((distance - 5) / 2);
        }
        else if (distance > 7 && distance <= 10)
        {
            factor1 = (distance - 7) / 3;
            factor2 = 1 - ((distance - 7) / 3);
        }
        else if (distance > 10 && distance <= 15)
        {
            factor1 = 1 - ((distance - 10) / 5);
            factor2 = (distance - 10) / 5;
        }
        else if (distance <= 20)
        {
            factor1 = 0;
            factor2 = 1 - (distance - 15) / 5;
        }
        else
        {
            factor1 = 0;
            factor2 = 0;
        }

        return (factor1, factor2);
    }


    int i = -15;
    int j = -15;
    int widthIndex;
    int heightIndex;
// Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Input.IsKeyPressed(Key.J))
        {
            return;
        }
        totalTime += (float)delta;
        //UpdateGrassChunks();
        if (grassReady)
        {
            int chunksUpdatedThisFrame = 0;
            int maxChunksPerFrame = 10; // Adjust this value for performance vs grass loading
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (; i <= 18; i++)
            {
                for (; j <= 18; j++)
                {
                    int currentWidthIndex = widthIndex + i;
                    int currentHeightIndex = heightIndex + j;
                    if(chunksUpdatedThisFrame > maxChunksPerFrame)
                    {
                        return;
                    }
                    chunksUpdatedThisFrame++;
                    // Ensure the indices are within the bounds of the grassChunks array
                    if (currentWidthIndex >= 0 && currentWidthIndex < grassChunks.GetLength(0) &&
                        currentHeightIndex >= 0 && currentHeightIndex < grassChunks.GetLength(1))
                    {
                        // Calculate the distance from the player to the current chunk
                        int distance = Math.Max(Math.Abs(i), Math.Abs(j));
                        if (!grassChunks[currentWidthIndex, currentHeightIndex].Item1.IsValid && !grassChunks[currentWidthIndex, currentHeightIndex].Item2.IsValid)
                        {
                            grassChunks[currentWidthIndex, currentHeightIndex].Item1 = InitializeRenderServerGrassClump(lowLODMesh, currentWidthIndex, currentHeightIndex, rowLength, 2048, 30, 30, randomSeed, 0);
                            grassChunks[currentWidthIndex, currentHeightIndex].Item2 = InitializeRenderServerGrassClump(lowLODMesh, currentWidthIndex, currentHeightIndex, rowLength, 2048, 30, 30, randomSeed, 1);
                            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, instanceCount / 4);
                            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, instanceCount / 4);
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 = 2;
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 2;
                        }
                        // Calculate the distance from the player to the current chunk

                        Vector3 chunkPosition = new Vector3(currentWidthIndex*30, 0, currentHeightIndex*30); //TODO REPLACE 0 with a local Y heightmap check
                        Vector3 playerPosition = new Vector3(player.Transform.Origin.X, 0, player.Transform.Origin.Z);

                        float real_distance = (chunkPosition - playerPosition).Length()/30.0f;
                        (float transitionFactor1, float transitionFactor2) = CalculateFactors(real_distance); //TODO smooth out the transition from medium to low and low to none
                        // Calculate the transition factors based on the distance

                        // Ensure the transition factors are within the range [0, 1]
                        transitionFactor1 = Math.Max(0, Math.Min(1, transitionFactor1));
                        transitionFactor2 = Math.Max(0, Math.Min(1, transitionFactor2));

                        // Calculate the number of instances for each LOD
                        int item2LODInstanceCount = (int)(instanceCount * transitionFactor1);
                        int item4LODInstanceCount = (int)(instanceCount * transitionFactor2);

                        //Item2 management between high and medium
                        if (distance <= 5 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 != 0)
                        {
                            RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, highLODMesh.GetRid());
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 0;
                        }
                        else if(distance > 5 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 != 1)
                        {
                            RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, mediumLODMesh.GetRid());
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 1;
                        }

                        //Item4 management between high and low
                        if (distance <= 10 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 != 0)
                        {
                            RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, highLODMesh.GetRid());
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 = 0;
                        }
                        else if(distance > 10 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 != 2)
                        {
                            RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, lowLODMesh.GetRid());
                            grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 = 2;
                        }


                        if (distance > 5)
                        {
                            item2LODInstanceCount = item2LODInstanceCount / 4;
                        }
                        if (distance > 10)
                        {
                            item4LODInstanceCount = item4LODInstanceCount / 8;
                        }
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, item2LODInstanceCount);
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, item4LODInstanceCount);
                    }
                }
                j = -15;
            }
            i = -15;
            widthIndex = (int)(player.Transform.Origin.X / 30);
            heightIndex = (int)(player.Transform.Origin.Z / 30);
            // Cleanup
            for (int i = 0; i < grassChunks.GetLength(0); i++)
            {
                for (int j = 0; j < grassChunks.GetLength(1); j++)
                {
                    if (i < widthIndex - 18 || i > widthIndex + 18 || j < heightIndex - 18 || j > heightIndex + 18)
                    {
                        if (grassChunks[i,j].Item1.IsValid || grassChunks[i, j].Item2.IsValid)
                        {
                            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[i, j].Item2, 0);
                            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[i, j].Item4, 0);
                            RenderingServer.FreeRid(grassChunks[i, j].Item1);
                            RenderingServer.FreeRid(grassChunks[i, j].Item2);
                            RenderingServer.FreeRid(grassChunkMultimeshs[i, j].Item2);
                            RenderingServer.FreeRid(grassChunkMultimeshs[i, j].Item4);
                            grassChunks[i, j].Item1 = new Rid();
                            grassChunks[i, j].Item2 = new Rid();
                            grassChunkMultimeshs[i, j].Item2 = new Rid();
                            grassChunkMultimeshs[i, j].Item4 = new Rid();
                        }
                    }
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
    }

    public void SetupGrass(String target, Image givenHeightMap)
    {
        heightMap = givenHeightMap;
        player = GetTree().CurrentScene.GetNode<CharacterBody3D>(target);
        GD.Print(player.Transform.Origin.X);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://scripts/terrain/grass/grassShader.gdshader");
        grassMat.Shader = grassShader;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);
        GD.Print(heightMapTexture.GetWidth());
        GD.Print(heightMapTexture.GetHeight());
        int chunkIndexWidth = heightMapTexture.GetWidth() / 30;
        int chunkIndexHeight = heightMapTexture.GetHeight() / 30;
        grassChunks = new ValueTuple<Rid, Rid>[chunkIndexWidth, chunkIndexHeight];
        grassChunkMultimeshs = new ValueTuple<uint, Rid, uint, Rid>[chunkIndexWidth, chunkIndexHeight];

        float grassWidth = 0.3f;
        float grassHeight = 1.5f;
        //TODO make a GrassMesh class that holds its width and height
        highLODMesh = CreateHighLODGrassBlade(grassWidth, grassHeight, grassMat);
        mediumLODMesh = CreateMediumLODGrassBlade(grassWidth*2, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        lowLODMesh = CreateLowLODGrassBlade(grassWidth*4, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles

        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        Rid mediumMaterialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(mediumMaterialShader, grassMat.Shader.Code);

        Rid lowMaterialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(lowMaterialShader, grassMat.Shader.Code);


        // Create a RID for the material and set its shader HIGH
        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);
        // Set the shader parameters HIGH
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", grassWidth);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(grassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(grassMaterial, "heightMap", heightMapTexture.GetRid());

        // Create a RID for the material and set its shader MEDIUM
        Rid mediumGrassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(mediumGrassMaterial, mediumMaterialShader);
        // Set the shader parameters MEDIUM
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "grassTotalWidth", grassWidth*2);
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "heightMap", heightMapTexture.GetRid());

        // Create a RID for the material and set its shader LOW
        Rid lowGrassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(lowGrassMaterial, lowMaterialShader);
        // Set the shader parameters LOW
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalWidth", grassWidth*4);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightMap", heightMapTexture.GetRid());

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(highLODMesh.GetRid(), 0, grassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(mediumLODMesh.GetRid(), 0, mediumGrassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(lowLODMesh.GetRid(), 0, lowGrassMaterial);



        //lets target 30 chunks by 30 chunks
        /*        for(int widthIndex = 0; widthIndex < 5; widthIndex++)
                {
                    for(int heightIndex = 0; heightIndex < 5; heightIndex++) 
                    {
                        InitializeRenderServerGrassClump(highLODMesh, widthIndex, heightIndex, rowLength, 4096, 30, 30, randomSeed);
                    }
                }*/

        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds TODO maybe speed up
            }
        });
        shaderParamThread.Start();

        widthIndex = (int)(player.Transform.Origin.X / 30);
        heightIndex = (int)(player.Transform.Origin.Z / 30);

        grassReady = true;
        GD.Print($"Setup Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }

    public Rid InitializeRenderServerGrassClump(Mesh grassBlade, int widthIndex, int heightIndex, int rowLength = 64, int instanceCount = 4096, float fieldWidth = 30f, float fieldHeight = 30f, int randomSeed = 0, int innerClumpIndex = 0)
    {
        Stopwatch stopwatch3 = Stopwatch.StartNew();
        //lookup cantor pairing functions, the result will apparently always be unique for all combinations of width and height index
        Random rand = new Random(randomSeed + CantorPair(CantorPair(widthIndex, heightIndex), innerClumpIndex));
        //GD.Print($"begin make multimesh: {stopwatch3.Elapsed}");
        Rid grassChunk = RenderingServer.MultimeshCreate();
        // Create a RID for the shader and set its code
        RenderingServer.MultimeshSetMesh(grassChunk, grassBlade.GetRid());
        // Create a new array to hold the transform data for all instances
        //GD.Print($"declare array size: {stopwatch3.Elapsed}");
        float[] instanceData = new float[12 * instanceCount];
        //GD.Print($"pre-array: {stopwatch3.Elapsed}");
        // Fill the array with the transform data for each instance
        int instanceDataIndex = 0;

        List<int> indices = Enumerable.Range(0, instanceCount).ToList();

        // Shuffle the list
        indices = indices.OrderBy(x => rand.Next()).ToList();

        foreach (int i in indices)
        {
            double x_loc = rand.NextDouble() * fieldWidth - fieldWidth / 2;
            double y_loc = rand.NextDouble() * fieldHeight - fieldHeight / 2;
            float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
            float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;

            // Create a new transform for this instance
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3((float)(x_loc + x_jitter), 0, (float)(y_loc + y_jitter)));

            // Add the transform data to the array
            instanceData[instanceDataIndex * 12 + 0] = transform.Basis.X.X;
            instanceData[instanceDataIndex * 12 + 1] = transform.Basis.X.Y;
            instanceData[instanceDataIndex * 12 + 2] = transform.Basis.X.Z;
            instanceData[instanceDataIndex * 12 + 3] = transform.Origin.X;
            instanceData[instanceDataIndex * 12 + 4] = transform.Basis.Y.X;
            instanceData[instanceDataIndex * 12 + 5] = transform.Basis.Y.Y;
            instanceData[instanceDataIndex * 12 + 6] = transform.Basis.Y.Z;
            instanceData[instanceDataIndex * 12 + 7] = transform.Origin.Y;
            instanceData[instanceDataIndex * 12 + 8] = transform.Basis.Z.X;
            instanceData[instanceDataIndex * 12 + 9] = transform.Basis.Z.Y;
            instanceData[instanceDataIndex * 12 + 10] = transform.Basis.Z.Z;
            instanceData[instanceDataIndex * 12 + 11] = transform.Origin.Z;

            instanceDataIndex++;
        }
        //GD.Print($"post-array: {stopwatch3.Elapsed}");
        // Set the buffer data for the MultiMesh
        RenderingServer.MultimeshAllocateData(grassChunk, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false);
        RenderingServer.MultimeshSetBuffer(grassChunk, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(grassChunk, instanceCount);

        // Create a new instance for the multimesh
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        Aabb multiMeshAABB = RenderingServer.MultimeshGetAabb(grassChunk);
        multiMeshAABB = multiMeshAABB.Expand(new Vector3(0, 400, 0));
        RenderingServer.InstanceSetCustomAabb(instance, multiMeshAABB);
        RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.Off);
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(widthIndex * fieldWidth + (fieldWidth/2), 0, heightIndex * fieldHeight + (fieldHeight / 2))));
        if (innerClumpIndex == 0)
        {
            grassChunkMultimeshs[widthIndex, heightIndex].Item2 = grassChunk;
        }
        else
        {
            grassChunkMultimeshs[widthIndex, heightIndex].Item4 = grassChunk;
        }
        return instance;
    }

    int CantorPair(int a, int b)
    {
        return (a + b) * (a + b + 1) / 2 + b;
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
    private Mesh CreateMediumLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.25f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.25f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.5f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.5f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.85f, myGrassHeight * 0.75f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.75f, 0), // Top-left corner

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

        return mediumLODMesh;
    }


    private Mesh CreateLowLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth, myGrassHeight, 0), // Top-right corner
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

        return lowLODMesh;
    }

}
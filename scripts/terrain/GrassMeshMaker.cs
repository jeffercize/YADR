using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Drawing;

public partial class GrassMeshMaker : Node3D
{
    float totalTime = 0.0f;
    int rowCount = 15;
    int columnCount = 15;
    ValueTuple<Rid, Rid>[,] grassChunks;
    ValueTuple<uint, Rid, uint, Rid>[,] grassChunkMultimeshs;
    List<Rid> activeGrassChunks = new List<Rid>();
    List<Rid> newActiveGrassChunks = new List<Rid>();

    int instanceCountGlobal = 8192;
    int rowLength = (int)Math.Sqrt(8192);
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
    {        //configure a set randomSeed, could share between users to make grass look the same in theory, tie it to the map generation seed TODO
        Random rand = new Random();
        randomSeed = rand.Next(int.MaxValue - 1000000); //subtract 1 mil because we use it in math and I dont want to overflow
    }

    public (float factor1, float factor2) CalculateFactorsOld(float distance)
    {
        float factor1, factor2;

        if (distance >= 0 && distance <= 1) //fade out 1 high lod
        {
            factor1 = 1;
            factor2 = 1 - distance;
        }
        else if (distance > 2 && distance <= 3) //fade in medium lod and fade out high lod
        {
            factor1 = 1 - (distance - 2);
            factor2 = distance - 2;
        }
        else if (distance > 3 && distance <= 4) //fade out medium lod and fade in low lod
        {
            factor1 = distance - 3;
            factor2 = 1 - (distance - 3);
        }
        else //maintain low lod
        {
            factor1 = 1;
            factor2 = 0;
        }

        return (factor1, factor2);
    }

    public (float factor1, float factor2) CalculateFactors(float distance)
    {
        float factor1 = 0.0f;
        float factor2 = 0.0f;
        if(distance < 2)
        {
            factor1 = 1.0f;
            factor2 = 0.0f;
        }
        else if (distance <= 12)
        {
            distance = (distance / 4) - 0.5f; //we divide by 2 to slow down the cosine wave so now its is 0-2 per flip
            //start 2 go to 6, instead divide by 4 so its start at .5 go to 1.5 so subtract .5 go from 0 to 1.0 then back again for low lod fade in thus 2-12 or 0->1->2(0), normalized :)
            factor1 = 0.5f * (1 + (float)Math.Cos(distance * Math.PI));
            factor2 = 0.5f * (1 + (float)Math.Cos(distance+1 * Math.PI));
        }
        else
        {
            factor1 = 1.0f;
            factor2 = 0.0f;
        }
        

        return (factor1, factor2);
    }


    int i = 0;
    int j = 0;
    int k = 0;
    int l = 0;
    int widthIndex;
    int heightIndex;
// Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        totalTime += (float)delta;
        //UpdateGrassChunks();
        if (grassReady)
        {
            if (Input.IsKeyPressed(Key.J))
            {
                processGrassClumps(9);
                //return;
            }
            else
            {
                //processGrassClumps(9);
                //cleanupGrassClumps();
            }

            if (Input.IsKeyPressed(Key.T))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 0.0);
            }
            else if (Input.IsKeyPressed(Key.F))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", MathF.PI/2);
            }
            else if(Input.IsKeyPressed(Key.B))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", MathF.PI);
            }
            else if(Input.IsKeyPressed(Key.H))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 3*MathF.PI/2);
            }
            else if (Input.IsKeyPressed(Key.Y))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 7*MathF.PI/4);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
    }

    private int GetNumBlades(int lodLevel)
    {
        // Return the number of blades based on the LOD level TODO
        return 10000 / (lodLevel + 1);
    }

    private Mesh GetMesh(int lodLevel)
    {
        // Return the mesh based on the LOD level
        return lodLevel >= 5 ? highLODMesh : lowLODMesh;
    }

    /// <summary>
    /// Clears the Rids and sets visible to 0 for multi-meshes that are out of range
    /// DOESNT ACTUALLY DELETE THE MULTIMESH not sure why TODO how do you actually clear something from RenderingServer
    /// ALSO TODO use visibility ranges to just fade in-out, then we can just say fuck it and generate a bunch of chunks and let them sit for a long time
    /// after we generate it a ring out from us we can generate beyond that into the fade out distance, the problem is still how to update mesh and distance stuff
    /// re-examine expert approach
    /// </summary>
    public void cleanupGrassClumps()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int chunksCleanedThisFrame = 0;
        int maxChunksCleanedPerFrame = 100;

        widthIndex = (int)(player.Transform.Origin.X / 30);
        heightIndex = (int)(player.Transform.Origin.Z / 30);
        // Cleanup
        for (; k < grassChunks.GetLength(0); k++)
        {
            for (; l < grassChunks.GetLength(1); l++)
            {
                if (k < widthIndex - 10 || k > widthIndex + 10 || l < heightIndex - 10 || l > heightIndex + 10)
                {
                    if (grassChunks[k, l].Item1.IsValid || grassChunks[k, l].Item2.IsValid)
                    {
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[k, l].Item2, 0);
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[k, l].Item4, 0);
                        RenderingServer.FreeRid(grassChunks[k, l].Item1);
                        RenderingServer.FreeRid(grassChunks[k, l].Item2);
                        RenderingServer.FreeRid(grassChunkMultimeshs[k, l].Item2);
                        RenderingServer.FreeRid(grassChunkMultimeshs[k, l].Item4);
                        grassChunks[k, l].Item1 = new Rid();
                        grassChunks[k, l].Item2 = new Rid();
                        grassChunkMultimeshs[k, l].Item2 = new Rid();
                        grassChunkMultimeshs[k, l].Item4 = new Rid();
                    }
                }
                chunksCleanedThisFrame += 1;
                if (chunksCleanedThisFrame > maxChunksCleanedPerFrame)
                {
                    //GD.Print($"Cleanup Time elapsed: {k} {l} {stopwatch.Elapsed}");
                    return;
                }
            }
            l = 0;
        }
        k = 0;
        //GD.Print($"Cleanup Time elapsed: {stopwatch.Elapsed}");
    }
    /// <summary>
    /// Should be reworked to dispatch the compute shader for each desired cluster (in view + some?), cluster will then manage their own culling
    /// </summary>
    public void processGrassClumps(int maxLevels)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Create a quadtree with bounds that cover your entire game world
        QuadTreeLOD quadtree = new QuadTreeLOD(0, new Rectangle(0, 0, 8192, 4096));

        // Process each quad in the quadtree as a grass clump
        ProcessQuads(quadtree, maxLevels);

        stopwatch.Stop();
        GD.Print("Processed grass clumps in " + stopwatch.ElapsedMilliseconds + " ms");
    }

    private void ProcessQuads(QuadTreeLOD quadtree, int maxLevel)
    {
        Vector3 centerPosition = new Vector3(quadtree.bounds.X + (quadtree.bounds.Width / 2), 0, quadtree.bounds.Y + (quadtree.bounds.Height / 2));
        float distanceToPlayer = centerPosition.DistanceTo(player.Transform.Origin);

        //could try quadtree.level * quadtree.bounds.Width instead
        float[] distanceThresholds = { 5000, 2500, 1250, 625, 300, 150, 75, 37.5f, 18.75f, 9.5f };

        // If the quad is within a certain distance of the player and it's not at the maximum level, split it
        if (quadtree.level < maxLevel && distanceToPlayer < distanceThresholds[quadtree.level])
        {
            GD.Print(distanceToPlayer + ", " + distanceThresholds[quadtree.level]);
            quadtree.SplitNode();

            // Process the children quads recursively
            foreach (QuadTreeLOD child in quadtree.nodes)
            {
                ProcessQuads(child, maxLevel);
            }
        }
        else
        {
            GD.Print(quadtree.bounds.Left + ", " + quadtree.bounds.Width);
            GD.Print(quadtree.bounds.Top + ", " + quadtree.bounds.Height);
            // Set the properties of the chunk based on the LOD level and quad size
            int numBlades = GetNumBlades(quadtree.level);
            Mesh mesh = GetMesh(quadtree.level);

            // Process the grass clump with the given properties
            processGrassClump(quadtree.bounds.Width, numBlades, mesh, quadtree, centerPosition);
        }
    }

    /// <summary>
    /// This should be re-written to just be a compute shader with a RenderingServerWrapper
    /// Currently it takes in a chunkWidth index and chunkHeight index and initializes that grass clump
    /// </summary>
    /// <param name="currentWidthIndex">index in chunks</param>
    /// <param name="currentHeightIndex">index in chunks</param>
    public void processGrassClump(int chunkSize, int numBlades, Mesh grassBlade, QuadTreeLOD quadTree, Vector3 centerPosition)
    {
        //Stopwatch stopwatch = Stopwatch.StartNew();
        float chunkHeight = heightMap.GetPixel((int)(centerPosition.X), (int)(centerPosition.Z)).R * 400.0f;
        RenderingDevice rd;
        int instanceCount;
        Rid instanceDataBuffer;
        (rd, instanceCount, instanceDataBuffer) = InitializeRenderServerGrassClump(quadTree, centerPosition, numBlades, chunkSize, chunkSize, chunkHeight);

        Rid chunkInstance = processComputeClumpData(grassBlade, rd, chunkSize, chunkSize, chunkHeight, instanceCount, instanceDataBuffer);
        //GD.Print($"One Clump Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetupGrass(String target, Image givenHeightMap)
    {
        heightMap = givenHeightMap;
        player = GetNode<CharacterBody3D>("../../"+target);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://shaders/terrain/grassShader.gdshader");
        grassMat.Shader = grassShader;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);
        //GD.Print(heightMapTexture.GetWidth());
        //GD.Print(heightMapTexture.GetHeight());
        int chunkIndexWidth = heightMapTexture.GetWidth() / 30;
        int chunkIndexHeight = heightMapTexture.GetHeight() / 30;
        grassChunks = new ValueTuple<Rid, Rid>[chunkIndexWidth, chunkIndexHeight];
        grassChunkMultimeshs = new ValueTuple<uint, Rid, uint, Rid>[chunkIndexWidth, chunkIndexHeight];

        float grassWidth = 0.3f;
        float grassHeight = 1.5f;
        highLODMesh = CreateHighLODGrassBlade(grassWidth, grassHeight, grassMat);
        mediumLODMesh = CreateMediumLODGrassBlade(grassWidth, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        lowLODMesh = CreateLowLODGrassBlade(grassWidth*2, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        
        //create and assign a shader per mesh
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

        //Set global wind direction
        RenderingServer.GlobalShaderParameterSet("windDirection", 7*MathF.PI/4);
        RenderingServer.GlobalShaderParameterSet("windStrength", 0.5);

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(highLODMesh.GetRid(), 0, grassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(mediumLODMesh.GetRid(), 0, mediumGrassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(lowLODMesh.GetRid(), 0, lowGrassMaterial);

        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds
            }
        });
        shaderParamThread.Start();

        widthIndex = (int)(player.Transform.Origin.X / 30);
        heightIndex = (int)(player.Transform.Origin.Z / 30);

        grassReady = true;
        //GD.Print($"Setup Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }

    public (RenderingDevice, int, Rid) InitializeRenderServerGrassClump(QuadTreeLOD quad, Vector3 centerPosition, int widthIndex, int heightIndex, int instanceCount = 4096, float fieldWidth = 30f, float fieldHeight = 30f, float chunkHeight = 0f)
    {
        int randSeed = randomSeed + CantorPair(widthIndex, heightIndex);
        //lookup cantor pairing functions, the result will apparently always be unique for all combinations of width and height index
        Random rand = new Random(randSeed);
        //GD.Print($"begin make multimesh: {stopwatch.ElapsedTicks}");

        // Create a new array to hold the transform data for all instances
        float[] instanceData = new float[16 * instanceCount];

        //various parameters for clumping information
        int bladesPerClump = 6;
        int desiredClumpCount = instanceCount / bladesPerClump;
        // Calculate the size of the array
        int arraySize = (int)MathF.Ceiling(MathF.Sqrt(desiredClumpCount)); 
        Tuple<float, float, float, int>[,] clumpPoints = new Tuple<float, float, float, int>[arraySize + 2, arraySize + 2]; //x,y,height,type, facing
        float spacing = fieldWidth / (MathF.Sqrt(desiredClumpCount) - 1);


        // Populate the array of clumps, this is very fast so we do it on CPU to reduce repetition on the GPU, could move it
        for (int i = -1; i <= arraySize; i++)
        {
            for (int j = -1; j <= arraySize; j++)
            {
                float x = i * spacing - fieldWidth / 2;
                float y = j * spacing - fieldHeight / 2;
                float jitterFactor = 3.0f;
                float x_jitter = rand.NextSingle() * spacing * jitterFactor - spacing / 2;
                float y_jitter = rand.NextSingle() * spacing * jitterFactor - spacing / 2;
                float rangeMin = 0.5f;
                float rangeMax = 0.6f;
                float randomNum = rangeMin + (rand.NextSingle() * (rangeMax - rangeMin));
                clumpPoints[i + 1, j + 1] = new Tuple<float, float, float, int>(x + x_jitter, y + y_jitter, rand.NextSingle()+0.4f, 1); //random height from 0.4 to 1.4 for now, all grass is type 1
            }
        }

        //fieldWidth, fieldHeight, chunkHeight, randSeed?, arraySize, clumpPoints, instanceData 
        //prepare the compute shader for repeated use later
        RenderingDevice rd = RenderingServer.CreateLocalRenderingDevice();
        RDShaderFile blendShaderFile = GD.Load<RDShaderFile>("res://shaders/terrain/computeGrassClump.glsl");
        RDShaderSpirV blendShaderBytecode = blendShaderFile.GetSpirV();
        Rid computeClumpShader = rd.ShaderCreateFromSpirV(blendShaderBytecode);

        //fieldWidth and fieldHeight buffer
        byte[] fieldDimensionsBytes = new byte[sizeof(float) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes(fieldWidth), 0, fieldDimensionsBytes, 0, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(fieldHeight), 0, fieldDimensionsBytes, sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(chunkHeight), 0, fieldDimensionsBytes, sizeof(float)*2, sizeof(float));
        Rid fieldDimensionsBuffer = rd.StorageBufferCreate((uint)fieldDimensionsBytes.Length, fieldDimensionsBytes);

        RDUniform fieldDimensionsUniform = new RDUniform()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 0
        };
        fieldDimensionsUniform.AddId(fieldDimensionsBuffer);

        //randSeed buffer
        byte[] randNumBytes = new byte[sizeof(int) * 2];
        Buffer.BlockCopy(BitConverter.GetBytes(randSeed), 0, randNumBytes, 0, sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(arraySize), 0, randNumBytes, sizeof(int), sizeof(int));
        Rid randNumBuffer = rd.StorageBufferCreate((uint)randNumBytes.Length, randNumBytes);

        RDUniform randNumUniform = new RDUniform()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 1
        };
        randNumUniform.AddId(randNumBuffer);


        //clumpPoints buffer
        byte[] clumpPointsBytes = new byte[clumpPoints.Length * (sizeof(float)*3 + sizeof(int))];
        for (int i = 0; i < arraySize + 2; i++)
        {
            for (int j = 0; j < arraySize + 2; j++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item1), 0, clumpPointsBytes, ((i + j) * (sizeof(float) * 3 + sizeof(int))), sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item2), 0, clumpPointsBytes, ((i + j) * (sizeof(float) * 3 + sizeof(int)) + sizeof(float)), sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item3), 0, clumpPointsBytes, ((i + j) * (sizeof(float) * 3 + sizeof(int)) + 2 * sizeof(float)), sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item4), 0, clumpPointsBytes, ((i + j) * (sizeof(float) * 3 + sizeof(int)) + 3 * sizeof(float)), sizeof(int));
            }
        }
        Rid clumpPointsBuffer = rd.StorageBufferCreate((uint)clumpPointsBytes.Length, clumpPointsBytes);
        var clumpPointsUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 2
        };
        clumpPointsUniform.AddId(clumpPointsBuffer);

        //instanceData buffer
        byte[] instanceDataBytes = new byte[instanceData.Length * sizeof(float)];
        for (int i = 0; i < instanceData.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(instanceData[i]), 0, instanceDataBytes, (i * sizeof(float)), sizeof(float));
        }
        Rid instanceDataBuffer = rd.StorageBufferCreate((uint)instanceDataBytes.Length, instanceDataBytes);
        var instanceDataUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 3
        };
        instanceDataUniform.AddId(instanceDataBuffer);

        var computeUniformSet = rd.UniformSetCreate(new Array<RDUniform> { fieldDimensionsUniform, randNumUniform, clumpPointsUniform, instanceDataUniform }, computeClumpShader, 0);

        // Create a compute pipeline
        var blendpipeline = rd.ComputePipelineCreate(computeClumpShader);
        var blendcomputeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(blendcomputeList, blendpipeline);
        rd.ComputeListBindUniformSet(blendcomputeList, computeUniformSet, 0);
        int blendthreadsPerGroup = 32;
        uint blendxGroups = (uint)(instanceCount + blendthreadsPerGroup - 1) / (uint)blendthreadsPerGroup;
        rd.ComputeListDispatch(blendcomputeList, blendxGroups, 1, 1);
        rd.ComputeListEnd();

        // Submit to GPU and wait for sync
        rd.Submit();
        return (rd, instanceCount, instanceDataBuffer);
        //we should wait a few frames then Sync
    }

    public Rid processComputeClumpData(Mesh grassBladeMesh, RenderingDevice rd, float fieldWidth, float fieldHeight, float chunkHeight, int instanceCount, Rid instanceDataBuffer)
    {
        rd.Sync();
        //Get Data
        byte[] byteData = rd.BufferGetData(instanceDataBuffer);
        float[] instanceData = new float[byteData.Length / sizeof(float)];
        for (int i = 0; i < byteData.Length; i += sizeof(float))
        {
            instanceData[i / sizeof(float)] = BitConverter.ToSingle(byteData, i);
            //GD.Print(BitConverter.ToSingle(byteData, i));
        }
        Rid grassChunk = RenderingServer.MultimeshCreate();
        // Create a RID for the shader and set its code
        RenderingServer.MultimeshSetMesh(grassChunk, grassBladeMesh.GetRid());
        RenderingServer.MultimeshAllocateData(grassChunk, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false, true);
        RenderingServer.MultimeshSetBuffer(grassChunk, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(grassChunk, instanceCount);

        // Create a new instance for the multimesh
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        Aabb multiMeshAABB = RenderingServer.MultimeshGetAabb(grassChunk);
        multiMeshAABB = multiMeshAABB.Expand(new Vector3(0, 400, 0));
        RenderingServer.InstanceSetCustomAabb(instance, multiMeshAABB);
        RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.Off);
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(0, 0, 0))); //calculate using centerpoint of the chunk TODO
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(0, chunkHeight, 0)));
        GD.Print("hurray chunk added");
        //RenderingServer.InstanceGeometrySetVisibilityRange(instance, 0.0f, 300.0f, 0.0f, 50.0f, RenderingServer.VisibilityRangeFadeMode.Self);
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
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
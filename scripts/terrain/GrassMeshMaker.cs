using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;

public partial class GrassMeshMaker : Node3D
{
    float totalTime = 0.0f;

    int randomSeed;
    bool grassReady = false;

    int globalOffsetX;
    int globalOffsetY;
    int globalXGridSize;
    int globalYGridSize;

    Mesh highLODMesh;
    Mesh mediumLODMesh;
    Mesh lowLODMesh;

    ImageTexture heightMapTexture;
    ImageTexture flattenMapTexture;
    ImageTexture controlMapTexture;

    Image heightMap;
    Image flattenMap;
    Image controlMap;

    CharacterBody3D player;

    RenderingDevice rd;

    private readonly object _chunkDatalock = new object();
    private readonly object _computeListLock = new object();
    private readonly object _renderServerLock = new object();

    
    Rid computeClumpShader;

    private volatile bool _abortRun = false;
    Queue<(Rid, Rid, Rid, Rid)> freeChunks = new Queue<(Rid, Rid, Rid, Rid)>();
    Queue<(float[], float, int, Vector3, Mesh, int, int, int)> readyDataChunks = new Queue<(float[], float, int, Vector3, Mesh, int, int, int)>();

    System.Collections.Generic.Dictionary<(int, int), (Rid, Rid, Rid, Rid, float)> activeChunkDictionary = new System.Collections.Generic.Dictionary<(int, int), (Rid, Rid, Rid, Rid, float)>();

    Thread processGrassThread;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {        //configure a set randomSeed, could share between users to make grass look the same in theory, tie it to the map generation seed TODO
        Random rand = new Random();
        randomSeed = rand.Next(2000); //subtract 1 mil because we use it in math and I dont want to overflow

    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    Transform3D oldPlayerPosition = new Transform3D();
    public override void _Process(double delta)
    {
        totalTime += (float)delta;
        //UpdateGrassChunks();
        if (grassReady)
        {
            if (readyDataChunks.Count != 0)
            {
                Stopwatch sw = Stopwatch.StartNew();
                int i = 0;
                while (i < 10 && readyDataChunks.Count != 0)
                {
                    i++;
                    (float[], float, int, Vector3, Mesh, int, int, int) readyDataChunk;
                    lock (_chunkDatalock)
                    {
                        readyDataChunk = readyDataChunks.Dequeue();
                    }
                    if(freeChunks.Count == 0)
                    {
                        freeChunks.Enqueue(InitializeFullRIDClump());
                    }
                    (Rid, Rid, Rid, Rid) chunkRids = freeChunks.Dequeue();
                    RecycleAndAddComputeClumpData(chunkRids.Item1, chunkRids.Item2, chunkRids.Item3, chunkRids.Item4, readyDataChunk.Item1, readyDataChunk.Item2, readyDataChunk.Item3, readyDataChunk.Item4, readyDataChunk.Item5, readyDataChunk.Item6, readyDataChunk.Item7, readyDataChunk.Item8);
                }
                if(sw.ElapsedMilliseconds > 5)
                {
                    GD.Print(sw.ElapsedMilliseconds);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        //controlMapTexture.Update();
    }

    public void SetupGrass(String target, Image givenHeightMap, int offsetX, int offsetY, int x_axis, int y_axis, Image givenFlattenMap, Image givenControlMap)
    {
        globalOffsetX = offsetX;
        globalOffsetY = offsetY;
        globalXGridSize = x_axis;
        globalYGridSize = y_axis;
        rd = RenderingServer.CreateLocalRenderingDevice();
        RDShaderFile blendShaderFile = GD.Load<RDShaderFile>("res://shaders/terrain/computeGrassClump.glsl");
        RDShaderSpirV blendShaderBytecode = blendShaderFile.GetSpirV();
        computeClumpShader = rd.ShaderCreateFromSpirV(blendShaderBytecode);
        heightMap = givenHeightMap;
        player = GetNode<CharacterBody3D>("../" + target);

        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://shaders/terrain/grassShader.gdshader");
        grassMat.Shader = grassShader;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);
        //flattenMapTexture = ImageTexture.CreateFromImage(givenFlattenMap);
        //controlMapTexture = ImageTexture.CreateFromImage(givenControlMap);

        float grassWidth = 0.3f;
        float grassHeight = 1.5f;
        highLODMesh = CreateHighLODGrassBlade(grassWidth, grassHeight);
        mediumLODMesh = CreateMediumLODGrassBlade(grassWidth, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        lowLODMesh = CreateLowLODGrassBlade(grassWidth, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles

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
        RenderingServer.MaterialSetParam(grassMaterial, "flattenMap", heightMapTexture.GetRid());
        RenderingServer.MaterialSetParam(grassMaterial, "controlMap", heightMapTexture.GetRid());

        //set location offset
        RenderingServer.MaterialSetParam(grassMaterial, "globalOffset", new Vector2(globalOffsetX, globalOffsetY));

        // Create a RID for the material and set its shader LOW
        Rid lowGrassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(lowGrassMaterial, lowMaterialShader);
        // Set the shader parameters LOW
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalWidth", grassWidth * 4);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));

        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightMap", heightMapTexture.GetRid());
        RenderingServer.MaterialSetParam(lowGrassMaterial, "flattenMap", heightMapTexture.GetRid());
        RenderingServer.MaterialSetParam(lowGrassMaterial, "controlMap", heightMapTexture.GetRid());

        //set location offset
        RenderingServer.MaterialSetParam(lowGrassMaterial, "globalOffset", new Vector2(globalOffsetX, globalOffsetY));

        //Set global wind direction
        RenderingServer.GlobalShaderParameterSet("windDirection", 7 * MathF.PI / 4);
        RenderingServer.GlobalShaderParameterSet("windStrength", 0.5);



        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(mediumLODMesh.GetRid(), 0, grassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(lowLODMesh.GetRid(), 0, lowGrassMaterial);

        for (int i = 0; i < 4096; i++)
        {
            freeChunks.Enqueue(InitializeFullRIDClump());
        }


        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds
            }
        });
        shaderParamThread.Start();

        grassReady = true;
        //processGrassThread = new Thread(() => process2DGrassClumps(8, 64, 16384)); 
        //processGrassThread = new Thread(() => process2DGrassClumps(16, 32, 4096)); 
        processGrassThread = new Thread(() => process2DGrassClumps(32, 16, 1024));
        //processGrassThread = new Thread(() => process2DGrassClumps(64, 8, 256));
        processGrassThread.Start();
    }

    public void process2DGrassClumps(int gridSize, int blockSize, int numBlades)
    {
        Process2DGrid(gridSize, blockSize, numBlades);
    }

    private void Process2DGrid(int gridSize, int blockSize, int numBlades)
    {
        // Iterate over the grid
        for(int x = 0; x < gridSize; x++)
        {
            for(int y = 0; y < gridSize; y++)
            {
                // Calculate the center position of the block
                Vector3 centerPosition = new Vector3(x * blockSize, 0, y * blockSize);

                // Process the grass clump with the given properties
                processGrassClump(blockSize, numBlades, highLODMesh, centerPosition, x, y, 0);
                processGrassClump(blockSize, numBlades, lowLODMesh, centerPosition, x, y, 1);
            }
        }
    }


    /// <summary>
    /// This should be re-written to just be a compute shader with a RenderingServerWrapper
    /// Currently it takes in a chunkWidth index and chunkHeight index and initializes that grass clump
    /// </summary>
    /// <param name="currentWidthIndex">index in chunks</param>
    /// <param name="currentHeightIndex">index in chunks</param>
    public void processGrassClump(int chunkSize, int numBlades, Mesh grassBlade, Vector3 centerPosition, int gridX, int gridZ, int myLOD)
    {
        //Stopwatch stopwatch = Stopwatch.StartNew();
        float chunkHeight = 0.0f;
        if (centerPosition.X >= 0 && centerPosition.Z >= 0 && centerPosition.X <= heightMap.GetWidth() && centerPosition.Z <= heightMap.GetHeight())
        {
            chunkHeight = heightMap.GetPixel((int)(centerPosition.X), (int)(centerPosition.Z)).R * 400.0f;
        }
        else
        {
            return;
        }

        bool success = true;

        float[] instanceData;
        (instanceData, success) = InitializeRenderServerGrassClump(centerPosition, numBlades, chunkSize, chunkSize, chunkHeight);

        if (!success)
        {
            return;
        }
        
        lock (_chunkDatalock)
        {
            readyDataChunks.Enqueue((instanceData, chunkHeight, numBlades, centerPosition, grassBlade, gridX, gridZ, myLOD));
        }
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }

    public (float[], bool) InitializeRenderServerGrassClump(Vector3 centerPosition, int instanceCount, float fieldWidth, float fieldHeight, float chunkHeight)
    {
        int randSeed = randomSeed + CantorPair((int)centerPosition.X, (int)centerPosition.Z);
        //lookup cantor pairing functions, the result will apparently always be unique for all combinations of width and height index
        Random rand = new Random(randSeed);

        // Create a new array to hold the transform data for all instances
        float[] instanceData = new float[16 * instanceCount];
        //various parameters for clumping information
        int bladesPerClump = 6;
        int desiredClumpCount = instanceCount / bladesPerClump;
        // Calculate the size of the array
        int arraySize = (int)MathF.Ceiling(MathF.Sqrt(desiredClumpCount)); 
        Tuple<float, float, float, int>[,] clumpPoints = new Tuple<float, float, float, int>[arraySize + 2, arraySize + 2]; //x,y,height,type, facing

        //thread terminator
        if (_abortRun)
        {
            return (null, false);
        }

        for (int i = 0; i < arraySize + 2; i++)
        {
            for (int j = 0; j < arraySize + 2; j++)
            {
                float x = (rand.NextSingle() * fieldWidth) - fieldWidth / 2;
                float y = (rand.NextSingle() * fieldHeight) - fieldHeight / 2;
                clumpPoints[i, j] = new Tuple<float, float, float, int>(x, y, rand.NextSingle() + 0.6f, 1); //random height from 0.4 to 1.4 for now, all grass is type 1
            }
        }

        //thread terminator
        if (_abortRun)
        {
            return (null,false);
        }

        //prepare the compute shader for use
        //rd = RenderingServer.CreateLocalRenderingDevice();

        //fieldWidth and fieldHeight buffer
        byte[] fieldDimensionsBytes = new byte[sizeof(float) * 5];
        Buffer.BlockCopy(BitConverter.GetBytes(fieldWidth), 0, fieldDimensionsBytes, 0, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(fieldHeight), 0, fieldDimensionsBytes, sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(chunkHeight), 0, fieldDimensionsBytes, sizeof(float) * 2, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(centerPosition.X), 0, fieldDimensionsBytes, sizeof(float) * 3, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(centerPosition.Z), 0, fieldDimensionsBytes, sizeof(float) * 4, sizeof(float));

        Rid fieldDimensionsBuffer = rd.StorageBufferCreate((uint)fieldDimensionsBytes.Length, fieldDimensionsBytes);

        RDUniform fieldDimensionsUniform = new RDUniform()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 0
        };
        fieldDimensionsUniform.AddId(fieldDimensionsBuffer);

        //randSeed buffer
        byte[] randNumBytes = new byte[sizeof(int) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes(randSeed), 0, randNumBytes, 0, sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(arraySize), 0, randNumBytes, sizeof(int), sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(instanceCount), 0, randNumBytes, sizeof(int) * 2, sizeof(int));
        Rid randNumBuffer = rd.StorageBufferCreate((uint)randNumBytes.Length, randNumBytes);

        RDUniform randNumUniform = new RDUniform()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 1
        };
        randNumUniform.AddId(randNumBuffer);


        //clumpPoints buffer
        byte[] clumpPointsBytes = new byte[clumpPoints.Length * (sizeof(float) * 3 + sizeof(int))];
        for (int i = 0; i < arraySize + 2; i++)
        {
            for (int j = 0; j < arraySize + 2; j++)
            {
                int index = (i * (arraySize + 2) + j) * (sizeof(float) * 3 + sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item1), 0, clumpPointsBytes, index, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item2), 0, clumpPointsBytes, index + sizeof(float), sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item3), 0, clumpPointsBytes, index + 2 * sizeof(float), sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(clumpPoints[i, j].Item4), 0, clumpPointsBytes, index + 3 * sizeof(float), sizeof(int));
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
        byte[] instanceDataBytes = new byte[16 * instanceCount * sizeof(float)];
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
        Rid blendpipeline;
        long blendcomputeList;
        
        
        lock (_computeListLock)
        {

        }
        if (_abortRun)
        {
            rd.FreeRid(instanceDataBuffer);
            rd.FreeRid(clumpPointsBuffer);
            rd.FreeRid(randNumBuffer);
            rd.FreeRid(fieldDimensionsBuffer);
            return (null, false);
        }
        // Submit to GPU and wait for sync

        blendpipeline = rd.ComputePipelineCreate(computeClumpShader);
        blendcomputeList = rd.ComputeListBegin(true);
        rd.ComputeListBindComputePipeline(blendcomputeList, blendpipeline);
        rd.ComputeListBindUniformSet(blendcomputeList, computeUniformSet, 0);
        int blendthreadsPerGroup = 32;
        //the addition of blendthreadsPerGroup before dividing is a common trick used to perform a "ceiling division" or "round-up division" in integer arithmetic.
        uint blendxGroups = (uint)(instanceCount + blendthreadsPerGroup - 1) / (uint)blendthreadsPerGroup;
        rd.ComputeListDispatch(blendcomputeList, blendxGroups, 1, 1);
        rd.ComputeListEnd();
        rd.Submit();
        rd.Sync();
        //thread terminator
        if (_abortRun)
        {
            rd.FreeRid(blendpipeline);
            rd.FreeRid(instanceDataBuffer);
            rd.FreeRid(clumpPointsBuffer);
            rd.FreeRid(randNumBuffer);
            rd.FreeRid(fieldDimensionsBuffer);
            return (null, false);
        }

        //Get Data
        byte[] byteData = rd.BufferGetData(instanceDataBuffer);
        ReadOnlySpan<byte> byteSpan = byteData.AsSpan();
        ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<byte, float>(byteSpan);
        instanceData = floatSpan.ToArray();
        rd.FreeRid(blendpipeline);
        rd.FreeRid(instanceDataBuffer);
        rd.FreeRid(clumpPointsBuffer);
        rd.FreeRid(randNumBuffer);
        rd.FreeRid(fieldDimensionsBuffer);
        return (instanceData, true);
        //we should wait a few frames then Sync
    }

    public (Rid, Rid, Rid, Rid) InitializeFullRIDClump()
    {
        (Rid, Rid) temp = InitializeRIDClump(0);
        (Rid, Rid) temp2 = InitializeRIDClump(1);
        return (temp.Item1, temp.Item2, temp2.Item1, temp2.Item2);
    }
    public (Rid, Rid) InitializeRIDClump(int myLOD)
    {
        Rid grassChunk = RenderingServer.MultimeshCreate();
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.Off);
        if (myLOD == 0)
        {
            //RenderingServer.InstanceGeometrySetVisibilityRange(instance, 0.0f, 100.0f, 2.0f, 2.0f, RenderingServer.VisibilityRangeFadeMode.Disabled);
        }
        else
        {
           // RenderingServer.InstanceGeometrySetVisibilityRange(instance, 100.0f, 250.0f, 2.0f, 2.0f, RenderingServer.VisibilityRangeFadeMode.Disabled);
        }
        return (instance, grassChunk);
    }

    public void RecycleAndAddComputeClumpData(Rid instance1, Rid multimesh1, Rid instance2, Rid multimesh2, float[] instanceData, float chunkHeight, int instanceCount, Vector3 centerPosition, Mesh grassBladeMesh, int gridX, int gridZ, int myLOD)
    {
        if(!activeChunkDictionary.ContainsKey((gridX, gridZ)))
        {
            activeChunkDictionary.Add((gridX, gridZ), (instance1, multimesh1, instance2, multimesh2, totalTime));
        }
        RecycleComputeClumpData(instance1, multimesh1, instanceData, chunkHeight, instanceCount, centerPosition, mediumLODMesh, gridX, gridZ);
        RecycleComputeClumpData(instance2, multimesh2, instanceData, chunkHeight, instanceCount, centerPosition, lowLODMesh, gridX, gridZ);
    }

    public void RecycleComputeClumpData(Rid instance, Rid multimesh, float[] instanceData, float chunkHeight, int instanceCount, Vector3 centerPosition, Mesh grassBladeMesh, int gridX, int gridZ)
    {
        // Set the new multimesh settings
        RenderingServer.MultimeshSetMesh(multimesh, grassBladeMesh.GetRid());
        RenderingServer.MultimeshAllocateData(multimesh, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false, true);
        RenderingServer.MultimeshSetBuffer(multimesh, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(multimesh, instanceCount);

        //AABB setting, maybe we can trim this out? we cant
        Aabb multiMeshAABB = RenderingServer.MultimeshGetAabb(multimesh);
        multiMeshAABB = multiMeshAABB.Expand(new Vector3(0, 800, 0));
        multiMeshAABB = multiMeshAABB.Expand(new Vector3(0, -800, 0));
        RenderingServer.InstanceSetCustomAabb(instance, multiMeshAABB);
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(centerPosition.X + globalOffsetX, chunkHeight, centerPosition.Z+globalOffsetY)));
    }

    int CantorPair(int a, int b)
    {
        return (a + b) * (a + b + 1) / 2 + b;
    }

    private Mesh CreateHighLODGrassBlade(float myGrassWidth, float myGrassHeight)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.975f, myGrassHeight * 0.15f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.025f, myGrassHeight * 0.15f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.95f, myGrassHeight * 0.3f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.05f, myGrassHeight * 0.3f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.45f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.45f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.85f, myGrassHeight * 0.6f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.6f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.8f, myGrassHeight * 0.75f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.2f, myGrassHeight * 0.75f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.7f, myGrassHeight * 0.85f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.3f, myGrassHeight * 0.85f, 0), // Top-left corner

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
using Godot;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot.Collections;
using System.Runtime.CompilerServices;

public partial class TerrainGeneration : Node3D
{
    int x_axis = 512; //the size of the image generated not the terrain
    int y_axis = 512;//the size of the image generated not the terrain
    float heightScale = 800.0f; //height modifier

    CharacterBody3D player;
    Rid navMap;
    float totalTime = 0.0f;
    bool waitingForRenderingDevices = false;
    Image grassFlattenMap;
    public ConcurrentQueue<RenderingDevice> renderingDevices = new ConcurrentQueue<RenderingDevice>();
    RDShaderFile[] shaderList = new RDShaderFile[] {
            GD.Load<RDShaderFile>("res://shaders/terrain/gausianblur.glsl")
        };
    RDShaderFile pathBuilderShaderFile;

    RDShaderFile blendShaderFile;
    Shader grassShader;

    //we use -5000000,-5000000 so it will update on spawn seems lazy idk
    private (int, int) currentChunk = (-5000000, -5000000);

    //queue of terrainchunks we need to create
    private ConcurrentQueue<(int,int)> chuckRequests = new ConcurrentQueue<(int,int)>();
    private ConcurrentQueue<(int, int)> midLODChunkRequests = new ConcurrentQueue<(int, int)>();
    private ConcurrentQueue<(int, int)> lowLODChuckRequests = new ConcurrentQueue<(int, int)>();

    //queue of free terrain chunks to be used by the terrain generation
    private ConcurrentQueue<TerrainChunk> freeChunks = new ConcurrentQueue<TerrainChunk>();
    private ConcurrentQueue<TerrainChunk> freeMidLODChunks = new ConcurrentQueue<TerrainChunk>();
    private ConcurrentQueue<TerrainChunk> freeLowLODChunks = new ConcurrentQueue<TerrainChunk>();


    //dictionary of all the terrain chunks we have created
    private ConcurrentDictionary<(int, int), TerrainChunk> terrainChunks = new ConcurrentDictionary<(int, int), TerrainChunk>();
    private ConcurrentDictionary<(int, int), TerrainChunk> midLODTerrainChunks = new ConcurrentDictionary<(int, int), TerrainChunk>();
    private ConcurrentDictionary<(int, int), TerrainChunk> lowLODTerrainChunks = new ConcurrentDictionary<(int, int), TerrainChunk>();


    //dictionary for the heightmap
    public ConcurrentDictionary<(int, int), Image> paddedHeightMaps = new ConcurrentDictionary<(int, int), Image>();

    //dictionary for the regions to track if paddedHeightMaps has the region loaded
    public ConcurrentDictionary<(int, int), bool> regionLoaded = new ConcurrentDictionary<(int, int), bool>();

    //offloading the physics additions from all chunks to be handeled here
    public ConcurrentQueue<(Rid, Rid, Transform3D)> queuedPhysicsShapes = new ConcurrentQueue<(Rid, Rid, Transform3D)>();
    public ConcurrentQueue<TerrainChunk> queuedChunkShapes = new ConcurrentQueue<TerrainChunk>();


    //queue of free grass chunks to be used by grassmeshmakers
    bool wantGrass = false;


    CompressedTexture2D rock = ResourceLoader.Load<CompressedTexture2D>("res://.godot/imported/rock030_alb_ht.png-c841db18b37aa5c942943cffad123dc2.bptc.ctex");
    CompressedTexture2D grass = ResourceLoader.Load<CompressedTexture2D>("res://.godot/imported/ground037_alb_ht.png-587e922b9c8fcab3f2d4050ac005b844.bptc.ctex");
    CompressedTexture2D road = ResourceLoader.Load<CompressedTexture2D>("res://.godot/imported/asphalt_04_diff_1k.png-6fb6a69fb7bdad4149863435ba87c518.bptc.ctex");

    CompressedTexture2D rockNormal = ResourceLoader.Load<CompressedTexture2D>("res://.godot/imported/rock030_nrm_rgh.png-f372ae26829f66919317068d636f6985.bptc.ctex");
    CompressedTexture2D grassNormal = ResourceLoader.Load<CompressedTexture2D>("res://.godot/imported/ground037_nrm_rgh.png-6815d522079724ff9e191de06a20875a.bptc.ctex");

    Shader terrainShader = GD.Load<Shader>("res://shaders/terrain/terrainChunk.gdshader");

    int highQuality = 4;
    int midQuality = 16;
    int lowQuality = 32;


    public override void _Ready()
    {
        blendShaderFile = GD.Load<RDShaderFile>("res://shaders/terrain/computeGrassClump.glsl");
        grassShader = GD.Load<Shader>("res://shaders/terrain/grassShader.gdshader");
        pathBuilderShaderFile = GD.Load<RDShaderFile>("res://shaders/terrain/pathbuilder.glsl");

        player = GetNode<CharacterBody3D>("Player");
        for (int i = 0; i < 20; i++)
        {
            renderingDevices.Enqueue(RenderingServer.CreateLocalRenderingDevice());
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

        //we declare the nav map here so it is shared for all the chunks
        navMap = NavigationServer3D.MapCreate();
        NavigationServer3D.MapSetUp(navMap, Vector3.Up);
        NavigationServer3D.MapSetActive(navMap, true);

        //grass stuff
        if (wantGrass)
        {

        }
        Thread generateRegionThread = new Thread(() => GenerateRegions());
        generateRegionThread.Start();
    }
    public override void _PhysicsProcess(double delta)
    {
        
        Stopwatch sw = Stopwatch.StartNew();
        (Rid, Rid, Transform3D) physicsShape;
        TerrainChunk chunk;
        if (queuedChunkShapes.TryDequeue(out chunk))
        {
            chunk.DeployMesh();
            //GD.Print("Add Mesh" + sw.ElapsedMilliseconds);
        }
        else if(queuedPhysicsShapes.TryDequeue(out physicsShape))
        {
            if(physicsShape.Item1.IsValid && physicsShape.Item2.IsValid)
            {
                PhysicsServer3D.BodyAddShape(physicsShape.Item1, physicsShape.Item2, physicsShape.Item3);
                //GD.Print("Add Shape" + sw.ElapsedMilliseconds);
            }
        }
        if(sw.ElapsedMilliseconds > 6)
        {
            GD.Print($"Physics Process Time elapsed: {sw.ElapsedMilliseconds}");
        }
    }
    public override void _Process(double delta)
    {

        Stopwatch sw2 = Stopwatch.StartNew();
        // Called every frame. Delta is time since the last frame
        // Update game logic here.
        totalTime += (float)delta;

        // Get the player's global position
        Vector3 playerPosition = player.GlobalTransform.Origin;

        // Calculate the player's current chunk
        int playerChunkX = (int)Math.Floor((playerPosition.X / 2048));
        int playerChunkY = (int)Math.Floor((playerPosition.Z / 2048));

        //generate new regions heightmaps and other processing if the player is getting close enough to them
        int playerRegionX = (playerChunkX / 16);
        int playerRegionY = (playerChunkY / 16);



        return;

        // Only update the current chunk if the player is outside the buffer zone
        if (Math.Abs(playerChunkX - currentChunk.Item1) > 0 || Math.Abs(playerChunkY - currentChunk.Item2) > 0)
        {
            (int, int) prevChunk = currentChunk;
            currentChunk = (playerChunkX, playerChunkY);
            if(currentChunk.Item1 != prevChunk.Item1 || currentChunk.Item2 != prevChunk.Item2)
            {
                // Request a 3x3 grid of highLOD chunks around the player.
                for (int x = playerChunkX - 1; x <= playerChunkX + 1; x++)
                {
                    for (int y = playerChunkY - 1; y <= playerChunkY + 1; y++)
                    {
                        (int, int) chunk = (x, y);

                        // If the chunk doesn't already exist, create it.
                        if (!terrainChunks.ContainsKey(chunk))
                        {
                            terrainChunks.TryAdd(chunk, null);
                            chuckRequests.Enqueue(chunk);
                        }
                    }
                }
                // Update the outer 11x11 grid of lowLOD chunks around the player
                for (int x = playerChunkX - 5; x <= playerChunkX + 5; x++)
                {
                    for (int y = playerChunkY - 5; y <= playerChunkY + 5; y++)
                    {
                        // Skip the inner 3x3 grid (OR DONT BECAUSE IT SUCKS JUST FADE IT)
                        if (x >= playerChunkX - 1 && x <= playerChunkX + 1 && y >= playerChunkY - 1 && y <= playerChunkY + 1)
                        {
                            continue;
                        }

                        (int, int) chunk = (x, y);
                        if (!midLODTerrainChunks.ContainsKey(chunk))
                        {
                            midLODTerrainChunks.TryAdd(chunk, null);
                            midLODChunkRequests.Enqueue((chunk.Item1, chunk.Item2));
                        }
                    }
                }

                // Update the outer-outer 10x10 grid of lowLOD chunks around the player
                for (int x = playerChunkX - 14; x <= playerChunkX + 14; x++)
                {
                    for (int y = playerChunkY - 14; y <= playerChunkY + 14; y++)
                    {
                        // Skip the inner 9x9 grid
                        if (x >= playerChunkX - 5 && x <= playerChunkX + 5 && y >= playerChunkY - 5 && y <= playerChunkY + 5)
                        {
                            continue;
                        }

                        (int, int) chunk = (x, y);
                        if (!lowLODTerrainChunks.ContainsKey(chunk))
                        {
                            lowLODTerrainChunks.TryAdd(chunk, null);
                            lowLODChuckRequests.Enqueue((chunk.Item1, chunk.Item2));
                        }
                    }
                }

                // Free chunks that are no longer needed.
                FreeChunks(playerChunkX, playerChunkY);
                /*Thread freeChunksThread = new Thread(() => FreeChunks(playerChunkX, playerChunkY));
                freeChunksThread.Start();*/
                FreeMidLODChunks(playerChunkX, playerChunkY);
                /*Thread freeMidLODChunksThread = new Thread(() => FreeMidLODChunks(playerChunkX, playerChunkY));
                freeMidLODChunksThread.Start();*/
                FreeLowLODChunks(playerChunkX, playerChunkY);
                /*Thread freeLowLODChunksThread = new Thread(() => FreeLowLODChunks(playerChunkX, playerChunkY));
                freeLowLODChunksThread.Start();*/
            }
        }
        Stopwatch sw = Stopwatch.StartNew();
        if (chuckRequests.Any() && renderingDevices.Any())
        {
            (int, int) requestedChunk;
            if (chuckRequests.TryDequeue(out requestedChunk))
            {
                TerrainChunk tempChunk;
                //GD.Print(tempChunk +" "+freeChunks.Count());
                if (freeChunks.TryDequeue(out tempChunk))
                {
                    //GD.Print("USING FREE CHUNK");
                    if (renderingDevices.TryDequeue(out RenderingDevice rd))
                    {
                        Thread updateTerrainThread = new Thread(() => UpdateTerrainChunk(rd, tempChunk, highQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                        updateTerrainThread.Start();
                    }
                }
                else if (renderingDevices.TryDequeue(out RenderingDevice rd))
                {
                    //GD.Print("Create New CHUNK");
                    Thread addTerrainThread = new Thread(() => AddTerrain(rd, false, highQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                    addTerrainThread.Start();
                }
                else
                {
                    //GD.Print("Give UP?");
                    chuckRequests.Enqueue(requestedChunk);
                }
            }
        }
        if (midLODChunkRequests.Any() && renderingDevices.Any())
        {
            (int, int) requestedChunk;
            if (midLODChunkRequests.TryDequeue(out requestedChunk))
            {
                TerrainChunk tempChunk;
                if (freeMidLODChunks.TryDequeue(out tempChunk))
                {
                    //GD.Print("USING FREE MID CHUNK");
                    if (renderingDevices.TryDequeue(out RenderingDevice rd))
                    {
                        Thread updateTerrainThread = new Thread(() => UpdateMidTerrainChunk(rd, tempChunk, midQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                        updateTerrainThread.Start();
                    }
                }
                else if (renderingDevices.TryDequeue(out RenderingDevice rd))
                {
                    Thread addTerrainThread = new Thread(() => AddMidTerrain(rd, false, midQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                    addTerrainThread.Start();
                }
                else
                {
                    midLODChunkRequests.Enqueue(requestedChunk);
                }
            }
        }
/*        if (lowLODChuckRequests.Any() && renderingDevices.Any())
        {
            (int, int) requestedChunk;
            if (lowLODChuckRequests.TryDequeue(out requestedChunk))
            {
                TerrainChunk tempChunk;
                if (freeLowLODChunks.TryDequeue(out tempChunk))
                {
                    //GD.Print("USING FREE LOW CHUNK");
                    if (renderingDevices.TryDequeue(out RenderingDevice rd))
                    {
                        Thread updateTerrainThread = new Thread(() => UpdateLowTerrainChunk(rd, tempChunk, lowQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                        updateTerrainThread.Start();
                    }
                }
                else if (renderingDevices.TryDequeue(out RenderingDevice rd))
                {
                    Thread addTerrainThread = new Thread(() => AddLowTerrain(rd, false, lowQuality, (requestedChunk.Item1, requestedChunk.Item2)));
                    addTerrainThread.Start();
                }
                else
                {
                    lowLODChuckRequests.Enqueue(requestedChunk);
                }
            }
        }*/
        if (sw2.ElapsedMilliseconds > 4)
        {
            GD.Print($"Full Process Time elapsed: {sw2.ElapsedMilliseconds}");
        }

/*        if (Input.IsKeyPressed(Key.K)) // Check if K key is pressed
        {
            GD.Print("Player Location:" + playerChunkX + " " + playerChunkY);
            GD.Print("midLODChunkRequests:");
            foreach (var request in midLODChunkRequests)
            {
                GD.Print(request);
            }

            GD.Print("midLODTerrainChunks (sorted by keys):");
            // Sort the ConcurrentDictionary by keys using LINQ
            var sortedChunks = midLODTerrainChunks.OrderBy(kvp => kvp.Key);
            foreach (var kvp in sortedChunks)
            {
                GD.Print($"Key: {kvp.Key}, Value: {kvp.Value}");
            }

            GD.Print("freeMidLODChunks:");
            foreach (var chunk in freeMidLODChunks)
            {
                GD.Print(chunk);
            }
        }*/
    }


    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private void FreeChunks(int playerChunkX, int playerChunkY)
    {
        foreach ((int, int) chunk in terrainChunks.Keys)
        {
            if (Math.Abs(chunk.Item1 - playerChunkX) > 1 || Math.Abs(chunk.Item2 - playerChunkY) > 1)
            {
                //GD.Print("High Free?: " + Math.Abs(chunk.Item1 - playerChunkX) + " " + Math.Abs(chunk.Item2 - playerChunkY) + " At: " + chunk.Item1 + " " + chunk.Item2);

                terrainChunks.TryGetValue(chunk, out TerrainChunk temp);
                if (temp != null)
                {
                    if (terrainChunks.TryRemove(chunk, out temp))
                    {
                        freeChunks.Enqueue(temp);
                        temp.PrepForFree();
                        //temp.CleanUp();
                    }
                }
            }
        }
    }

    private void FreeMidLODChunks(int playerChunkX, int playerChunkY)
    {
        foreach ((int, int) chunk in midLODTerrainChunks.Keys)
        {
            if (Math.Abs(chunk.Item1 - playerChunkX) > 5 || Math.Abs(chunk.Item2 - playerChunkY) > 5)
            {
                //GD.Print("MID Free At: " + chunk.Item1 + " " + chunk.Item2);
                //GD.Print("playerAt: " + playerChunkX + " " + playerChunkY);

                midLODTerrainChunks.TryGetValue(chunk, out TerrainChunk temp);
                if (temp != null)
                {
                    if(midLODTerrainChunks.TryRemove(chunk, out temp))
                    {
                        freeMidLODChunks.Enqueue(temp);
                        temp.PrepForFree();
                        //temp.CleanUp();
                    }
                }
            }
        }
    }

    private void FreeLowLODChunks(int playerChunkX, int playerChunkY)
    {
        foreach ((int, int) chunk in lowLODTerrainChunks.Keys)
        {
            if (Math.Abs(chunk.Item1 - playerChunkX) > 14 || Math.Abs(chunk.Item2 - playerChunkY) > 14 || (Math.Abs(chunk.Item1 - playerChunkX) < 5 && Math.Abs(chunk.Item2 - playerChunkY) < 5))
            {
                //GD.Print("LOW Free?: " + Math.Abs(chunk.Item1 - playerChunkX) + " " + Math.Abs(chunk.Item2 - playerChunkY) +" At: " + chunk.Item1 + " " + chunk.Item2);
                lowLODTerrainChunks.TryGetValue(chunk, out TerrainChunk temp);
                if (temp != null)
                {
                    if (lowLODTerrainChunks.TryRemove(chunk, out temp))
                    {
                        freeLowLODChunks.Enqueue(temp);
                        temp.PrepForFree();
                        //temp.CleanUp();
                    }
                }
            }
        }
    }





    public Image ApplyGassianAndBoxBlur(RenderingDevice rd, Image image, RenderingDevice.DataFormat imageFormat)
    {
        // Create a local rendering device.
        Image pathImg = Image.Create(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgf);
        foreach (var shaderFile in shaderList)
        {
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);

            //Setup Input Image
            RDSamplerState samplerState = new RDSamplerState();
            samplerState.RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge;
            samplerState.RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge;
            samplerState.RepeatW = RenderingDevice.SamplerRepeatMode.ClampToEdge;
            samplerState.MinFilter = RenderingDevice.SamplerFilter.Linear;
            samplerState.MipFilter = RenderingDevice.SamplerFilter.Linear;
            samplerState.MagFilter = RenderingDevice.SamplerFilter.Linear;
            Rid sampler = rd.SamplerCreate(samplerState);
            RDTextureFormat inputFmt = new RDTextureFormat();
            inputFmt.Width = (uint)image.GetWidth();
            inputFmt.Height = (uint)image.GetHeight();
            inputFmt.Format = imageFormat;
            inputFmt.UsageBits = RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit;
            RDTextureView inputView = new RDTextureView();
            byte[] inputImageData = image.GetData();
            Godot.Collections.Array<byte[]> inputData = new Godot.Collections.Array<byte[]>
            {
                inputImageData
            };
            Rid inputTex = rd.TextureCreate(inputFmt, inputView, inputData);
            RDUniform samplerUniform = new RDUniform();
            samplerUniform.UniformType = RenderingDevice.UniformType.SamplerWithTexture;
            samplerUniform.Binding = 0;
            samplerUniform.AddId(sampler);
            samplerUniform.AddId(inputTex);



            //Setup Output Image 
            RDTextureFormat fmt = new RDTextureFormat();
            fmt.Width = (uint)image.GetWidth();
            fmt.Height = (uint)image.GetHeight();
            fmt.Format = RenderingDevice.DataFormat.R32G32Sfloat;
            fmt.UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.CanCopyFromBit;
            RDTextureView view = new RDTextureView();
            Image output_image = Image.Create(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgf);
            byte[] outputImageData = image.GetData();
            Godot.Collections.Array<byte[]> tempData = new Godot.Collections.Array<byte[]>
            {
                outputImageData
            };
            Rid output_tex = rd.TextureCreate(fmt, view, tempData);
            RDUniform outputTexUniform = new RDUniform();
            outputTexUniform.UniformType = RenderingDevice.UniformType.Image;
            outputTexUniform.Binding = 1;
            outputTexUniform.AddId(output_tex);

            // Setup ImageDimensionsUniform
            int imageWidth = image.GetWidth();
            int imageHeight = image.GetHeight();
            byte[] imageDimensionsBytes = new byte[sizeof(int) * 2];
            Buffer.BlockCopy(BitConverter.GetBytes(imageWidth), 0, imageDimensionsBytes, 0, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(imageHeight), 0, imageDimensionsBytes, sizeof(int), sizeof(int));
            Rid imageDimensionsBuffer = rd.StorageBufferCreate((uint)imageDimensionsBytes.Length, imageDimensionsBytes);

            RDUniform imageDimensionsUniform = new RDUniform()
            {
                UniformType = RenderingDevice.UniformType.StorageBuffer,
                Binding = 2
            };
            imageDimensionsUniform.AddId(imageDimensionsBuffer);

            //create the uniformSet
            Rid uniformSet = rd.UniformSetCreate(new Array<RDUniform> { samplerUniform, outputTexUniform, imageDimensionsUniform }, shader, 0);

            // Create a compute pipeline
            Rid pipeline = rd.ComputePipelineCreate(shader);
            long computeList = rd.ComputeListBegin();
            rd.ComputeListBindComputePipeline(computeList, pipeline);
            rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
            int threadsPerGroup = 32;
            uint xGroups = (uint)(pathImg.GetWidth() + threadsPerGroup - 1) / (uint)threadsPerGroup;
            uint yGroups = (uint)(pathImg.GetHeight() + threadsPerGroup - 1) / (uint)threadsPerGroup;
            rd.ComputeListDispatch(computeList, xGroups, yGroups, 1);
            rd.ComputeListEnd();

            // Submit to GPU and wait for sync
            GD.Print("Submit Blur Noise Job");
            rd.Submit();
/*            int waitTime = 3000;
            Thread.Sleep(waitTime);*/
            rd.Sync();

            //Get Data
            var byteData = rd.TextureGetData(output_tex, 0);
            pathImg = Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgf, byteData);
            image = pathImg;
            imageFormat = RenderingDevice.DataFormat.R32G32Sfloat;

            rd.FreeRid(shader);
            rd.FreeRid(sampler);
            rd.FreeRid(inputTex);
            rd.FreeRid(output_tex);
            rd.FreeRid(imageDimensionsBuffer);
            //rd.FreeRid(uniformSet); //we dont free these apparently
            //rd.FreeRid(pipeline);
        }
        return pathImg;
    }

    public Image GPUGeneratePath(RenderingDevice rd, Image noiseImage, int offsetX, int offsetY, int x_axis, int y_axis, Vector3[] points)
    { 

        //TODO this is the shit that is broken idk :) if we just loop over all points then its fine but of course that isnt an option
        int gridSize = 256;
        // Calculate the bounds for cell IDs based on the specified rectangular region
        int minX = offsetX / gridSize; // 0/512 = 0
        int maxX = (offsetX + x_axis) / gridSize; //0+8192 / 512 = 18
        int minY = offsetY / gridSize; // 0/512 = 0
        int maxY = (offsetY + y_axis) / gridSize; //0+8192 / 512 = 18
        int totalCellsX = (maxX - minX); //18
        int totalCellsY = (maxY - minY); //18
        int totalCells = totalCellsX * totalCellsY; //324

        // Initialize with the correct size based on grid dimensions
        (int StartIndex, int Count)[] cellIndexAndCountArray = new (int, int)[totalCells];
        List<Vector3> pointsList = new List<Vector3>();

        // Initialize all counts to 0
        for (int i = 0; i < cellIndexAndCountArray.Length; i++)
        {
            cellIndexAndCountArray[i] = (0, 0);
        }

        // Initialize a dictionary to hold lists of points for each cell
        System.Collections.Generic.Dictionary<(int cellX, int cellY), List<Vector3>> cellPointsDict = new System.Collections.Generic.Dictionary<(int cellX, int cellY), List<Vector3>>();
        GD.Print("OffsetX: " + offsetX + " x_axis: " + x_axis);
        foreach (Vector3 point in points)
        {
            int cellX = (int)Math.Floor((point.X - (float)offsetX) / (float)gridSize);
            int cellY = (int)Math.Floor((point.Y - (float)offsetY) / (float)gridSize);

            if (cellX >= 0 && cellX < totalCellsX && cellY >= 0 && cellY < totalCellsY)
            {
                // Create a key for the current cell
                var cellKey = (cellX, cellY);

                // If the cell is not already in the dictionary, add it with a new list
                if (!cellPointsDict.ContainsKey(cellKey))
                {
                    cellPointsDict[cellKey] = new List<Vector3>();
                }

                // Add the current point to the list for this cell
                cellPointsDict[cellKey].Add(point);
            }
        }

        foreach (var kvp in cellPointsDict)
        {
            var cellKey = kvp.Key;
            var pointsInCell = kvp.Value;
            int cellIndex = cellKey.cellX + (cellKey.cellY * totalCellsX);
            GD.Print($"Cell Location: {cellKey}, Count: {pointsInCell.Count}, Cell Index: {cellIndex}AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            GD.Print($"Point: {pointsInCell.First()}");
            // Assuming cellIndexAndCountArray has been initialized to the correct size
            cellIndexAndCountArray[cellIndex].StartIndex = pointsList.Count; // Set the start index for this cell
            cellIndexAndCountArray[cellIndex].Count = pointsInCell.Count; // Set the count of points for this cell

            // Add all points from this cell to the pointsList
            pointsList.AddRange(pointsInCell);
        }

        for (int i = 0; i < cellIndexAndCountArray.Length; i++)
        {
            var cell = cellIndexAndCountArray[i];
            GD.Print($"Cell Index: {i}, StartIndex: {cell.StartIndex}, Count: {cell.Count}");
        }

        // Convert pointsList to an array
        Vector3[] pointsArray = pointsList.ToArray();
/*        for (int i = 0; i < pointsArray.Length/4; i++)
        {
            var cell = pointsArray[i];
            GD.Print($"Point Index: {i}, Point Value: {cell}");
        }*/

        //setup compute shader

        RDShaderSpirV blendShaderBytecode = pathBuilderShaderFile.GetSpirV();
        Rid blendShader = rd.ShaderCreateFromSpirV(blendShaderBytecode);

        //Setup Noise Image
        RDSamplerState noiseSamplerState = new RDSamplerState();
        Rid noiseSampler = rd.SamplerCreate(noiseSamplerState);
        RDTextureFormat noiseInputFmt = new RDTextureFormat();
        noiseInputFmt.Width = (uint)noiseImage.GetWidth();
        noiseInputFmt.Height = (uint)noiseImage.GetHeight();
        noiseInputFmt.Format = RenderingDevice.DataFormat.R32G32Sfloat;
        noiseInputFmt.UsageBits = RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit;
        RDTextureView noiseInputView = new RDTextureView();
        byte[] noiseInputImageData = noiseImage.GetData();
        Godot.Collections.Array<byte[]> noiseData = new Godot.Collections.Array<byte[]>
            {
                noiseInputImageData
            };
        Rid noiseTex = rd.TextureCreate(noiseInputFmt, noiseInputView, noiseData);
        RDUniform noiseSamplerUniform = new RDUniform();
        noiseSamplerUniform.UniformType = RenderingDevice.UniformType.SamplerWithTexture;
        noiseSamplerUniform.Binding = 0;
        noiseSamplerUniform.AddId(noiseSampler);
        noiseSamplerUniform.AddId(noiseTex);

        //Setup Path Array


        //Setup Output Image 
        RDTextureFormat blendfmt = new RDTextureFormat();
        blendfmt.Width = (uint)x_axis;
        blendfmt.Height = (uint)y_axis;
        blendfmt.Format = RenderingDevice.DataFormat.R32G32Sfloat;
        blendfmt.UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.CanCopyFromBit;
        RDTextureView blendview = new RDTextureView();
        Image blend_image = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        byte[] blendOutputImageData = blend_image.GetData();
        Godot.Collections.Array<byte[]> blendTempData = new Godot.Collections.Array<byte[]>
            {
                blendOutputImageData
            };
        Rid blendOutputTex = rd.TextureCreate(blendfmt, blendview, blendTempData);
        RDUniform blendOutputTexUniform = new RDUniform();
        blendOutputTexUniform.UniformType = RenderingDevice.UniformType.Image;
        blendOutputTexUniform.Binding = 1;
        blendOutputTexUniform.AddId(blendOutputTex);

        // Setup ImageDimensionsUniform
        int imageWidth = x_axis;
        int imageHeight = y_axis;
        byte[] imageDimensionsBytes = new byte[sizeof(int) * 4];
        Buffer.BlockCopy(BitConverter.GetBytes(imageWidth), 0, imageDimensionsBytes, 0, sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(imageHeight), 0, imageDimensionsBytes, sizeof(int), sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(offsetX), 0, imageDimensionsBytes, sizeof(int)*2, sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(offsetY), 0, imageDimensionsBytes, sizeof(int)*3, sizeof(int));


        Rid imageDimensionsBuffer = rd.StorageBufferCreate((uint)imageDimensionsBytes.Length, imageDimensionsBytes);

        RDUniform imageDimensionsUniform = new RDUniform()
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 2
        };
        imageDimensionsUniform.AddId(imageDimensionsBuffer);

        List<byte> cellIndexBytesList = new List<byte>();
        foreach (var cell in cellIndexAndCountArray)
        {
            cellIndexBytesList.AddRange(BitConverter.GetBytes(cell.StartIndex));
            cellIndexBytesList.AddRange(BitConverter.GetBytes(cell.Count));
        }
        byte[] cellIndexBytes = cellIndexBytesList.ToArray();

        // Create buffer for cellIndexArray
        Rid cellIndexBuffer = rd.StorageBufferCreate((uint)cellIndexBytes.Length, cellIndexBytes);

        // Create uniform for the buffer
        var cellIndexUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 3
        };
        cellIndexUniform.AddId(cellIndexBuffer);

        // Calculate the size of the buffer needed
        int bufferSize = pointsArray.Length * sizeof(float) * 3; // 3 floats per Vector3, 4 bytes per float

        // Create a byte array to hold the points data
        byte[] pointsBytes = new byte[bufferSize];

        // Fill the byte array with the points data
        for (int i = 0; i < pointsArray.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(pointsArray[i].X), 0, pointsBytes, i * sizeof(float) * 3, sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(pointsArray[i].Y), 0, pointsBytes, i * sizeof(float) * 3 + sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(pointsArray[i].Z), 0, pointsBytes, i * sizeof(float) * 3 + 2 * sizeof(float), sizeof(float));
        }

        // Create the storage buffer with the points data
        Rid pointsBuffer = rd.StorageBufferCreate((uint)bufferSize, pointsBytes);

        var pointsArrayUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 4
        };
        pointsArrayUniform.AddId(pointsBuffer);

        // Create the uniformSet
        Rid blenduniformSet = rd.UniformSetCreate(new Array<RDUniform> { noiseSamplerUniform, blendOutputTexUniform, imageDimensionsUniform, pointsArrayUniform, cellIndexUniform }, blendShader, 0);

        // Create a compute pipeline
        Rid blendpipeline = rd.ComputePipelineCreate(blendShader);

        var blendcomputeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(blendcomputeList, blendpipeline);
        rd.ComputeListBindUniformSet(blendcomputeList, blenduniformSet, 0);
        int blendthreadsPerGroup = 32;
        uint blendxGroups = (uint)(noiseImage.GetWidth() + blendthreadsPerGroup - 1) / (uint)blendthreadsPerGroup;
        uint blendyGroups = (uint)(noiseImage.GetHeight() + blendthreadsPerGroup - 1) / (uint)blendthreadsPerGroup;
        rd.ComputeListDispatch(blendcomputeList, blendxGroups, blendyGroups, 1);
        rd.ComputeListEnd();

        // Submit to GPU and wait for sync
        GD.Print("Submit Generate Path Job");
        rd.Submit();
        /*int waitTime = 3000;
        Thread.Sleep(waitTime);//this was an attempt at avoiding the hitches didnt seem to work?*/
        rd.Sync();

        //rd.FreeRid(blenduniformSet); //for some reason this is invalid to free?
        //rd.FreeRid(blendpipeline);  //for some reason this is invalid to free?

        //Get Data
        var blendbyteData = rd.TextureGetData(blendOutputTex, 0);
        Image final_img = Image.CreateFromData(x_axis, y_axis, false, Image.Format.Rgf, blendbyteData);

        rd.FreeRid(blendShader);
        rd.FreeRid(noiseSampler);
        rd.FreeRid(noiseTex);
        rd.FreeRid(pointsBuffer);
        rd.FreeRid(cellIndexBuffer);
        rd.FreeRid(blendOutputTex);
        rd.FreeRid(imageDimensionsBuffer);

        return final_img;
    }

    public Image GenerateTerrain(RenderingDevice rd, int offsetX, int offsetY, int x_axis, int y_axis)
	{
        //move offset back by 10 and add 20 extra pixels to discard after blurring
        //that way we have a ring of extra pixels so we can calculate blur
/*        offsetX -= 16;
        x_axis += 32;
        offsetY -= 16;
        y_axis += 32;*/
        

        //consider moving all this noise generation to GPU and do errosion and other fun stuff
		FastNoiseLite noise = new FastNoiseLite();
		noise.Frequency = 0.0005f;
		noise.Seed = 1;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.None;
        noise.DomainWarpEnabled = false;
        noise.Offset = new Vector3(offsetX, offsetY, 0.0f);

        Image noiseImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        Stopwatch sw = Stopwatch.StartNew();
        for(int i = 0; i < x_axis; i++)
        {
            for(int j = 0; j < y_axis; j++)
            {
                // Get the noise value at the world position
                float noiseValue = noise.GetNoise2D(i, j);

                // Set the pixel in the image
                noiseImage.SetPixel(i, j, new Color(noiseValue, 0, 0, 0));
            }
        }
        //noiseImage.SavePng("C:\\Users\\jeffe\\test_images\\noise_test"+"("+ offsetX + "," + offsetY + ")"+ ".png");

        Curve3D path = new Curve3D();
        path.AddPoint(new Vector3(300, 0, 1.0f));
        path.AddPoint(new Vector3(300, 750, 0.9f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(400, 500, 0.8f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(500, 750, 0.7f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(600, 500, 0.6f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(700, 750, 0.5f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(800, 500, 0.4f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(900, 750, 0.3f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(1000, 500, 0.2f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(1100, 750, 0.1f), new Vector3(-100.0f, 0.0f, 0.0f), new Vector3(100.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(600, 3000, 0.11f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(2600, 1000, 0.19f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(2600, 3500, 0.19f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(4600, 1200, 0.10f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(6600, 3500, 0.33f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(7600, 1500, 0.19f), new Vector3(-200.0f, 0.0f, 0.0f), new Vector3(200.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(12192, 2048, 0.0f));

        Image pathImg = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        path.BakeInterval = 0.1f;
        Vector3[] localPath = path.GetBakedPoints();
        pathImg = GPUGeneratePath(rd, noiseImage, offsetX, offsetY, x_axis, y_axis, localPath);
        // Run the blur shader
        pathImg = ApplyGassianAndBoxBlur(rd, pathImg, RenderingDevice.DataFormat.R32G32Sfloat);

        return pathImg;
    }
    private void GenerateRegions()
    {
        // Get the player's global position
        Vector3 playerPosition = player.GlobalTransform.Origin;
        // Calculate the player's current chunk
        int playerChunkX = (int)Math.Floor((playerPosition.X / 2048)); //512 * vertex expansion (which is 4 when writing this)
        int playerChunkY = (int)Math.Floor((playerPosition.Z / 2048));

        //generate new regions heightmaps and other processing if the player is getting close enough to them
        int regionSize = 4; //number of chunks in a region 16 gives you a region size of 8192x8192
        int playerRegionX = (playerChunkX / regionSize);
        int playerRegionY = (playerChunkY / regionSize);

        for (int i = 0; i < 2; i++) //probably switch to -1 <= 1, but leaving because this is probably all getting thrown out
        {
            for (int j = 0; j < 2; j++)
            {
                if (!regionLoaded.TryGetValue((playerRegionX + i, playerChunkY + j), out bool hasRegion))
                {
                    GD.Print((playerRegionX + i) + " " + (playerChunkY + j));
                    if (renderingDevices.Any())
                    {
                        if (renderingDevices.TryDequeue(out RenderingDevice rd))
                        {
                            regionLoaded.TryAdd((playerRegionX + i, playerChunkY + j), false);
                            GenerateRegion(rd, playerRegionX + i, playerChunkY + j, x_axis, y_axis, regionSize);
                        }
                    }
                }
            }
        }
    }
    private void GenerateRegion(RenderingDevice rd, int regionX, int regionY, int x_axis, int y_axis, int regionSize)
    {
        //each region is regionsize by regionsize chunks
        GD.Print("OffsetX: " + regionX * regionSize * x_axis + " OffsetY: " + regionY * regionSize * y_axis + " x_axis: " + x_axis * regionSize + " y_axis: " + y_axis * regionSize);
        Image regionImg = GenerateTerrain(rd, regionX * regionSize * x_axis, regionY * regionSize * y_axis, x_axis * regionSize, y_axis* regionSize);
        regionImg.SavePng("C:\\Users\\jeffe\\test_images\\region(" + regionX + "," + regionY + ").png");
        for (int i = 0; i < regionSize; i++)
        {
            for (int j = 0; j < regionSize; j++)
            {
                int chunkX = regionX + i;
                int chunkY = regionY + j;
                int offsetX = chunkX * x_axis;
                int offsetY = chunkY * y_axis;
                Image paddedImg = Image.Create(x_axis + 32, y_axis + 32, false, Image.Format.Rgf);
                paddedImg.BlitRect(regionImg, new Rect2I(offsetX, offsetY, x_axis + 32, y_axis + 32), new Vector2I(0, 0));
                paddedHeightMaps.TryAdd((chunkX, chunkY), paddedImg);
            }
        }
        regionLoaded.TryUpdate((regionX, regionY), true, false);
        renderingDevices.Enqueue(rd);
    }
    private void UpdateTerrainChunk(RenderingDevice rd, TerrainChunk myTerrainChunk, int quality, (int, int) chunkRequest)
    {
        int offsetX = chunkRequest.Item1 * x_axis;// - (chunkRequest.Item1 * quality);
        int offsetY = chunkRequest.Item2 * y_axis;// - (chunkRequest.Item2 * quality);

        Image paddedImg;
        if (paddedHeightMaps.TryGetValue(chunkRequest, out paddedImg))
        {
            //GD.Print("we found the heightmap in the dictionary");
            renderingDevices.Enqueue(rd);
            Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
            mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
            myTerrainChunk.RebuildChunk(mapImage, paddedImg, x_axis, y_axis, offsetX, offsetY);
        }
        else
        {
            GD.Print("OBSOLETE SHOULD NOT BE CALLED");  //we generate region ahead of time and should be loaded into paddedHeightMaps ahead of time
            return;
            paddedImg = GenerateTerrain(rd, offsetX, offsetY, x_axis, y_axis);
            renderingDevices.Enqueue(rd);
            paddedHeightMaps.TryAdd(chunkRequest, paddedImg);
            Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
            mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
            myTerrainChunk.RebuildChunk(mapImage, paddedImg, x_axis, y_axis, offsetX, offsetY);
        }
        terrainChunks.TryUpdate(chunkRequest, myTerrainChunk, null);
    }

    public void UpdateHighTerrainChunk(RenderingDevice rd, TerrainChunk myTerrainChunk, int quality, (int, int) chunkRequest)
    {
        UpdateTerrainChunk(rd, myTerrainChunk, quality, chunkRequest);
        terrainChunks.TryUpdate(chunkRequest, myTerrainChunk, null);
    }
    public void UpdateMidTerrainChunk(RenderingDevice rd, TerrainChunk myTerrainChunk, int quality, (int, int) chunkRequest)
    {
        UpdateTerrainChunk(rd, myTerrainChunk, quality, chunkRequest);
        midLODTerrainChunks.TryUpdate(chunkRequest, myTerrainChunk, null);
    }
    public void UpdateLowTerrainChunk(RenderingDevice rd, TerrainChunk myTerrainChunk, int quality, (int, int) chunkRequest)
    {
        UpdateTerrainChunk(rd, myTerrainChunk, quality, chunkRequest);
        lowLODTerrainChunks.TryUpdate(chunkRequest, myTerrainChunk, null);
    }

    public void AddTerrain(RenderingDevice rd, bool wantGrass, int quality, (int,int) chunkRequest)
    {

        terrainChunks.TryUpdate(chunkRequest, AddTerrainChunk(rd, chunkRequest, x_axis, y_axis, heightScale, wantGrass, quality), null);

        renderingDevices.Enqueue(rd);
    }
    public void AddMidTerrain(RenderingDevice rd, bool wantGrass, int quality, (int, int) chunkRequest)
    {
        midLODTerrainChunks.TryUpdate(chunkRequest, AddTerrainChunk(rd, chunkRequest, x_axis, y_axis, heightScale, wantGrass, quality), null);

        renderingDevices.Enqueue(rd);
    }
    public void AddLowTerrain(RenderingDevice rd, bool wantGrass, int quality, (int, int) chunkRequest)
    {
        int x_axis = 512; //if you change these a lot of code may need changed?
        int y_axis = 512; //if you change these a lot of code may need changed?
        float heightScale = 800.0f;

        lowLODTerrainChunks.TryUpdate(chunkRequest, AddTerrainChunk(rd, chunkRequest, x_axis, y_axis, heightScale, wantGrass, quality), null);

        renderingDevices.Enqueue(rd);
    }

    private TerrainChunk AddTerrainChunk(RenderingDevice rd, (int,int) chunkRequest, int x_axis, int y_axis, float heightScale, bool wantGrass, int quality)
    {

        int offsetX = chunkRequest.Item1 * x_axis;// - (chunkRequest.Item1 * quality);
        int offsetY = chunkRequest.Item2 * y_axis;// - (chunkRequest.Item2 * quality);
        Image paddedImg;
        if (paddedHeightMaps.TryGetValue(chunkRequest, out paddedImg))
        {
            Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
            mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
            TerrainChunk terrainChunk = new TerrainChunk(mapImage, paddedImg, heightScale, x_axis, y_axis, offsetX, offsetY, wantGrass, quality, this, blendShaderFile, grassShader, rock, grass, road, rockNormal, grassNormal, terrainShader);
            CallDeferred(Node3D.MethodName.AddChild, (terrainChunk));
            return terrainChunk;
        }
        else
        {
            GD.Print("OBSOLETE SHOULD NOT BE CALLED"); //we generate region ahead of time and should be loaded into paddedHeightMaps ahead of time
            return null;
            paddedImg = GenerateTerrain(rd, offsetX, offsetY, x_axis, y_axis);
            paddedHeightMaps.TryAdd(chunkRequest, paddedImg);
            Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
            mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
            TerrainChunk terrainChunk = new TerrainChunk(mapImage, paddedImg, heightScale, x_axis, y_axis, offsetX, offsetY, wantGrass, quality, this, blendShaderFile, grassShader, rock, grass, road, rockNormal, grassNormal, terrainShader);
            CallDeferred(Node3D.MethodName.AddChild, (terrainChunk));

            return terrainChunk;
        } 
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }
}
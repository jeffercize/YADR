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

    //we use -5,-5 so it will update on spawn seems lazy idk
    private (int, int) currentChunk = (-5, -5);

    //queue of terrainchunks we need to create
    private ConcurrentQueue<(int,int)> chuckRequests = new ConcurrentQueue<(int,int)>();
    private ConcurrentQueue<(int, int)> lowLODChuckRequests = new ConcurrentQueue<(int, int)>();

    //dictionary of all the terrain chunks we have created
    private ConcurrentDictionary<(int, int), TerrainChunk> terrainChunks = new ConcurrentDictionary<(int, int), TerrainChunk>();
    private ConcurrentDictionary<(int, int), TerrainChunk> lowLODTerrainChunks = new ConcurrentDictionary<(int, int), TerrainChunk>();


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
    }
    public override void _PhysicsProcess(double delta)
    {
        
        Stopwatch sw = Stopwatch.StartNew();
        (Rid, Rid, Transform3D) physicsShape;
        TerrainChunk chunk;
        if (queuedChunkShapes.TryDequeue(out chunk))
        {
            chunk.DeployMesh();
            GD.Print("Add Mesh" + sw.ElapsedMilliseconds);
        }
        else if(queuedPhysicsShapes.TryDequeue(out physicsShape))
        {
            if(physicsShape.Item1.IsValid && physicsShape.Item2.IsValid)
            {
                PhysicsServer3D.BodyAddShape(physicsShape.Item1, physicsShape.Item2, physicsShape.Item3);
                GD.Print("Add Shape" + sw.ElapsedMilliseconds);
            }
        }
        if(sw.ElapsedMilliseconds > 4)
        {
            GD.Print($"Physics Process Time elapsed: {sw.ElapsedMilliseconds}");
        }
    }
    public override void _Process(double delta)
    {
        Stopwatch sw2 = Stopwatch.StartNew();
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw3 = new Stopwatch();
        Stopwatch sw4 = new Stopwatch();
        sw4.Start();
        // Called every frame. Delta is time since the last frame
        // Update game logic here.
        totalTime += (float)delta;

        // Get the player's global position
        Vector3 playerPosition = player.GlobalTransform.Origin;

        // Calculate the player's current chunk
        int playerChunkX = (int)Math.Floor((playerPosition.X / 512));
        int playerChunkY = (int)Math.Floor((playerPosition.Z / 512));


        sw4.Stop();
        // Only update the current chunk if the player is outside the buffer zone
        if (Math.Abs(playerChunkX - currentChunk.Item1) > 0 || Math.Abs(playerChunkY - currentChunk.Item2) > 0)
        {
            (int, int) prevChunk = currentChunk;
            currentChunk = (playerChunkX, playerChunkY);
            if(currentChunk.Item1 != prevChunk.Item1 || currentChunk.Item2 != prevChunk.Item2)
            {
                sw3.Start();
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
                //Update the 10x10 grid of lowLOD chunks around the player
                for (int x = playerChunkX - 10; x <= playerChunkX + 10; x++)
                {
                    for (int y = playerChunkY - 10; y <= playerChunkY + 10; y++)
                    {
                        (int, int) chunk = (x, y);
                        if (!lowLODTerrainChunks.ContainsKey(chunk))
                        {
                            lowLODTerrainChunks.TryAdd(chunk, null);
                            lowLODChuckRequests.Enqueue(chunk);
                        }
                    }
                }
                sw3.Stop();
                sw1.Start();
                // Remove chunks that are no longer needed.
                Thread removeLowLODChunksThread = new Thread(() => RemoveLowLODChunks(playerChunkX, playerChunkY));
                removeLowLODChunksThread.Start();
                Thread removeChunksThread = new Thread(() => RemoveChunks(playerChunkX, playerChunkY));
                removeChunksThread.Start();
                sw1.Stop();
            }
        }
        Stopwatch sw = Stopwatch.StartNew();
        while (chuckRequests.Any() && renderingDevices.Any())
        {
            (int, int) requestedChunk;
            if (chuckRequests.TryDequeue(out requestedChunk))
            {
                if(renderingDevices.TryDequeue(out RenderingDevice rd))
                {
                    Thread addTerrainThread = new Thread(() => AddTerrain(rd, wantGrass, 16, requestedChunk)); //higher the number LOWER THE QUALITY lol
                    addTerrainThread.Start();
                }
                else
                {
                    chuckRequests.Enqueue(requestedChunk);
                }
            }
        }
        /*if (lowLODChuckRequests.Any() && renderingDevices.Any())
        {
            (int, int) requestedChunk;
            if (lowLODChuckRequests.TryDequeue(out requestedChunk))
            {
                if (renderingDevices.TryDequeue(out RenderingDevice rd))
                {
                    Thread addTerrainThread = new Thread(() => AddTerrain(rd, false, 4, requestedChunk));
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
            GD.Print($"Other STuff Time elapsed: {sw4.ElapsedMilliseconds}");
            GD.Print($"Add Chunks Time elapsed: {sw3.ElapsedMilliseconds}");
            GD.Print($"Remove Chunks Time elapsed: {sw1.ElapsedMilliseconds}");
            GD.Print($"Chunk Requests Time elapsed: {sw.ElapsedMilliseconds}");
            GD.Print($"Full Process Time elapsed: {sw2.ElapsedMilliseconds}");
        }
    }


    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private void RemoveChunks(int playerChunkX, int playerChunkY)
    {
        foreach ((int, int) chunk in terrainChunks.Keys)
        {
            if (Math.Abs(chunk.Item1 - playerChunkX) > 1 || Math.Abs(chunk.Item2 - playerChunkY) > 1)
            {
                TerrainChunk temp;
                terrainChunks.TryGetValue(chunk, out temp);
                if (temp != null)
                {
                    terrainChunks.TryRemove(chunk, out temp);
                    temp.CleanUp();
                }
            }
        }
    }

    private void RemoveLowLODChunks(int playerChunkX, int playerChunkY)
    {
        foreach ((int, int) chunk in lowLODTerrainChunks.Keys)
        {
            if (Math.Abs(chunk.Item1 - playerChunkX) > 10 || Math.Abs(chunk.Item2 - playerChunkY) > 10)
            {
                TerrainChunk temp;
                terrainChunks.TryGetValue(chunk, out temp);
                if (temp != null)
                {
                    terrainChunks.TryRemove(chunk, out temp);
                    temp.CleanUp();
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
            rd.Submit();
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

    public Image GPUGeneratePath(RenderingDevice rd, Image noiseImage, int x_axis, int y_axis, int offsetX, int offsetY, Vector3[] points)
    {
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
        byte[] pointsBytes = new byte[points.Length * sizeof(float) * 3];
        for (int i = 0; i < points.Length; i++)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].X), 0, pointsBytes, (i * sizeof(float) * 3), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].Y), 0, pointsBytes, (i * sizeof(float) * 3) + sizeof(float), sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(points[i].Z), 0, pointsBytes, (i * sizeof(float) * 3) + 2 * sizeof(float), sizeof(float));
        }
        Rid pointsBuffer = rd.StorageBufferCreate((uint)pointsBytes.Length, pointsBytes);
        var pathUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 1
        };
        pathUniform.AddId(pointsBuffer);


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
        blendOutputTexUniform.Binding = 2;
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
            Binding = 3
        };
        imageDimensionsUniform.AddId(imageDimensionsBuffer);

        //create the uniformSet
        Rid blenduniformSet = rd.UniformSetCreate(new Array<RDUniform> { noiseSamplerUniform, pathUniform, blendOutputTexUniform, imageDimensionsUniform }, blendShader, 0);

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
        rd.Submit();
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
        rd.FreeRid(blendOutputTex);
        rd.FreeRid(imageDimensionsBuffer);

        return final_img;
    }

    public Image GenerateTerrain(RenderingDevice rd, int offsetX, int offsetY, int x_axis, int y_axis)
	{
        //move offset back by 10 and add 20 extra pixels to discard after blurring
        //that way we have a ring of extra pixels so we can calculate blur
        offsetX -= 16;
        x_axis += 32;
        offsetY -= 16;
        y_axis += 32;
        

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
                path.AddPoint(new Vector3(8192, 2048, 0.0f));

        Image pathImg = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        path.BakeInterval = 0.1f;
        Vector3[] localPath = path.GetBakedPoints();
        pathImg = GPUGeneratePath(rd, noiseImage, x_axis, y_axis, offsetX, offsetY, localPath);
        // Run the blur shader
        pathImg = ApplyGassianAndBoxBlur(rd, pathImg, RenderingDevice.DataFormat.R32G32Sfloat);

        //THIS LOOKS CONFUSING but its because we adjust x_axis and y_axis at the top of this function to make passing it easier

        return pathImg;
    }
    public void AddTerrain(RenderingDevice rd, bool wantGrass, int quality, (int,int) chunkRequest)
    {
        int x_axis = 512; //if you change these a lot of code may need changed?
        int y_axis = 512; //if you change these a lot of code may need changed?
        float heightScale = 400.0f;

        terrainChunks.TryUpdate(chunkRequest, AddTerrainChunk(rd, chunkRequest, x_axis, y_axis, heightScale, wantGrass, quality), null);

        renderingDevices.Enqueue(rd);
    }

    private TerrainChunk AddTerrainChunk(RenderingDevice rd, (int,int) chunkRequest, int x_axis, int y_axis, float heightScale, bool wantGrass, int quality)
    {
        int offsetX = chunkRequest.Item1 * x_axis - (chunkRequest.Item1 * quality);
        int offsetY = chunkRequest.Item2 * y_axis - (chunkRequest.Item2 * quality);
        Image paddedImg = GenerateTerrain(rd, offsetX, offsetY, x_axis, y_axis);
        Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
        //Image mapImage = Image.LoadFromFile("C:\\Users\\jeffe\\test_images\\noise_test.png");
        TerrainChunk terrainChunk = new TerrainChunk(mapImage, paddedImg, heightScale, x_axis, y_axis, offsetX, offsetY, wantGrass, quality, this, blendShaderFile, grassShader, rock, grass, road, rockNormal, grassNormal, terrainShader);
        CallDeferred(Node3D.MethodName.AddChild, (terrainChunk));
        return terrainChunk;
    }



    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }
}
using Godot;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot.Collections;
using System.Runtime.CompilerServices;

public partial class TerrainGeneration : Node3D
{
    Rid navMap;
    float totalTime = 0.0f;
    Image grassFlattenMap;
    public override void _Ready()
    {
        Thread addTerrainThread = new Thread(() => AddTerrain(false));
        addTerrainThread.Start();
        //AddTerrain(false);
        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds
            }
        });
        shaderParamThread.Start();
    }

    public override void _Process(double delta)
    {
        // Called every frame. Delta is time since the last frame.
        // Update game logic here.
        totalTime += (float)delta;
    }


    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }





    public Image ApplyGassianAndBoxBlur(Image image, RenderingDevice.DataFormat imageFormat)
    {
        // Create a local rendering device.
        var rd = RenderingServer.CreateLocalRenderingDevice();
        Image pathImg = Image.Create(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgf);
        RDShaderFile[] shaderList = new RDShaderFile[] {
            GD.Load<RDShaderFile>("res://shaders/terrain/gausianblur.glsl")
        };
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
            var uniformSet = rd.UniformSetCreate(new Array<RDUniform> { samplerUniform, outputTexUniform, imageDimensionsUniform }, shader, 0);

            // Create a compute pipeline
            var pipeline = rd.ComputePipelineCreate(shader);
            var computeList = rd.ComputeListBegin();
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
        }
        return pathImg;
    }

    public Image GPUGeneratePath(Image noiseImage, int x_axis, int y_axis, int offsetX, int offsetY, Vector3[] points)
    {
        var rd = RenderingServer.CreateLocalRenderingDevice();
        RDShaderFile blendShaderFile = GD.Load<RDShaderFile>("res://shaders/terrain/pathbuilder.glsl");
        RDShaderSpirV blendShaderBytecode = blendShaderFile.GetSpirV();
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
        var blenduniformSet = rd.UniformSetCreate(new Array<RDUniform> { noiseSamplerUniform, pathUniform, blendOutputTexUniform, imageDimensionsUniform }, blendShader, 0);

        // Create a compute pipeline
        var blendpipeline = rd.ComputePipelineCreate(blendShader);
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

        //Get Data
        var blendbyteData = rd.TextureGetData(blendOutputTex, 0);
        Image final_img = Image.CreateFromData(x_axis, y_axis, false, Image.Format.Rgf, blendbyteData);
        return final_img;
    }

    public Image GenerateTerrain(int offsetX, int offsetY, int x_axis, int y_axis)
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
        noiseImage.SavePng("C:\\Users\\jeffe\\test_images\\noise_test"+"("+ offsetX + "," + offsetY + ")"+ ".png");

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
        pathImg = GPUGeneratePath(noiseImage, x_axis, y_axis, offsetX, offsetY, localPath);
        // Run the blur shader
        pathImg = ApplyGassianAndBoxBlur(pathImg, RenderingDevice.DataFormat.R32G32Sfloat);

        //THIS LOOKS CONFUSING but its because we adjust x_axis and y_axis at the top of this function to make passing it easier

        return pathImg;
    }
    TerrainChunk[,] innerChunks = new TerrainChunk[5,5];
    public void AddTerrain(bool wantGrass=true)
    {
        wantGrass = true;
        Stopwatch stopwatch = Stopwatch.StartNew();
        int x_axis = 512;//16000; //if you change these a lot of shaders need re-coded maybe?
        int y_axis = 512;//6000; //if you change these a lot of shaders need re-coded maybe?
        float heightScale = 400.0f;

        //we declare the nav map here so it is shared for all the chunks
        navMap = NavigationServer3D.MapCreate();
        NavigationServer3D.MapSetUp(navMap, Vector3.Up);
        NavigationServer3D.MapSetActive(navMap, true);

        for (int i = 0; i < 3; i++)
        {
            for(int j = 0; j < 3; j++)
            {
                AddTerrainChunk(i, j, x_axis, y_axis, heightScale, wantGrass);
            }
        }

        GD.Print($"Time elapsed: {stopwatch.Elapsed}");

        //runtime_nav_baker.Set("enabled", true);
        GD.Print($"Terrrain Full Time elapsed: {stopwatch.Elapsed}");

    }

    private void AddTerrainChunk(int i, int j, int x_axis, int y_axis, float heightScale, bool wantGrass)
    {
        int offsetX = i * x_axis - i;
        int offsetY = j * y_axis - j;
        Stopwatch sw = Stopwatch.StartNew();
        Image paddedImg = GenerateTerrain(offsetX, offsetY, x_axis, y_axis);
        GD.Print($"Generate TerrainImage: {sw.ElapsedMilliseconds}");
        sw.Restart();
        Image mapImage = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        mapImage.BlitRect(paddedImg, new Rect2I(16, 16, x_axis + 16, y_axis + 16), new Vector2I(0, 0));
        GD.Print($"Clip Image: {sw.ElapsedMilliseconds}");
        //Image mapImage = Image.LoadFromFile("C:\\Users\\jeffe\\test_images\\noise_test.png");
        TerrainChunk terrainChunk = new TerrainChunk(mapImage, paddedImg, heightScale, x_axis, y_axis, offsetX, offsetY, wantGrass);
        innerChunks[i, j] = terrainChunk;
        CallDeferred(Node3D.MethodName.AddChild, (terrainChunk));
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }
}
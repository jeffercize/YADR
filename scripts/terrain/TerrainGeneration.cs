using Godot;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot.Collections;

public partial class TerrainGeneration : Node3D
{
    public override void _Ready()
    {
        // Called every time the node is added to the scene.
        // Initialization here.
        //AddTerrain("Terrain3D");
        //GenerateTerrain("Terrain3D2", 1000);
    }

    public override void _Process(double delta)
    {
        // Called every frame. Delta is time since the last frame.
        // Update game logic here.
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
            GD.Load<RDShaderFile>("res://shaders/terrain/gausianblur.glsl"),
            GD.Load<RDShaderFile>("res://shaders/terrain/boxblur.glsl")
        };
        foreach (var shaderFile in shaderList)
        {
            RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
            Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);

            //Setup Input Image
            RDSamplerState samplerState = new RDSamplerState();
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
            byte[] outputImageData = output_image.GetData();
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

    public Image GPUGeneratePath(Image noiseImage, int x_axis, int y_axis, Vector3[] points)
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
        byte[] imageDimensionsBytes = new byte[sizeof(int) * 2];
        Buffer.BlockCopy(BitConverter.GetBytes(imageWidth), 0, imageDimensionsBytes, 0, sizeof(int));
        Buffer.BlockCopy(BitConverter.GetBytes(imageHeight), 0, imageDimensionsBytes, sizeof(int), sizeof(int));
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
        Image final_img = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);
        final_img = Image.CreateFromData(x_axis, y_axis, false, Image.Format.Rgf, blendbyteData);
        return final_img;
    }

    public void GeneratePath(Image innerPathImg, Image pathImg, System.Collections.Generic.Dictionary<float, List<float>>  pathDict, List<Vector3> path_result, int x_axis, int y_axis, System.Collections.Generic.List<float> myKeys)
    {
        foreach (var key in myKeys)
        {
            var yValues = pathDict[key];
            foreach (var yVal in yValues.Distinct())
            {
                float height = 0.0f;
                float pathHeight = path_result.FirstOrDefault(p => p.X == key && p.Y == yVal).Z;
                float midVal = yVal;
                for (int innerY = (int)MathF.Round(yVal) - 300; innerY < (int)MathF.Round(yVal) + 300; innerY++)
                {
                    if (!((int)MathF.Round(key) >= (int)x_axis || (int)MathF.Round(innerY) >= (int)y_axis || (int)MathF.Round(key) < 0 || (int)MathF.Round(innerY) < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerY - midVal);
                        float weight = 0.0f;

                        if (innerY > yVal - 30.0f && innerY < yVal + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel((int)key, (int)innerY, new Color(height, weight, 0, 0));
                        }
                        else if (innerY > yVal - 300.0f && innerY < yVal + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel((int)key, (int)innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel((int)key, (int)innerY, new Color(height, newWeight, 0, 0));
                        }
                    }
                }
                for (int innerX = (int)MathF.Round(key) - 300; innerX < (int)MathF.Round(key) + 300; innerX++)
                {
                    if (!((int)MathF.Round(innerX) >= (int)x_axis || (int)MathF.Round(yVal) >= (int)y_axis || (int)MathF.Round(innerX) < 0 || (int)MathF.Round(yVal) < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerX - key);
                        float weight = 0.0f;
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel(innerX, (int)MathF.Round(yVal), new Color(height, weight, 0, 0));

                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel(innerX, (int)MathF.Round(yVal));
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel(innerX, (int)MathF.Round(yVal), new Color(height, newWeight, 0, 0));
                        }
                    }
                }
                // Top-left
                for (int innerX = (int)MathF.Round(key) - 300, innerY = (int)MathF.Round(yVal) + 300;
                     innerX < (int)MathF.Round(key) && innerY > (int)MathF.Round(yVal);
                     innerX++, innerY--)
                {
                    if (!((int)MathF.Round(innerX) >= (int)x_axis || innerY >= (int)y_axis || (int)MathF.Round(innerX) < 0 || innerY < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerX - key);
                        float weight = 0.0f;
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel((int)innerX, (int)innerY, new Color(height, weight, 0, 0));

                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel(innerX, innerY, new Color(height, newWeight, 0, 0));
                        }
                    }
                }

                // Top-right
                for (int innerX = (int)MathF.Round(key) + 300, innerY = (int)MathF.Round(yVal) + 300;
                     innerX > (int)MathF.Round(key) && innerY > (int)MathF.Round(yVal);
                     innerX--, innerY--)
                {
                    if (!((int)MathF.Round(innerX) >= (int)x_axis || innerY >= (int)y_axis || (int)MathF.Round(innerX) < 0 || innerY < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerX - key);
                        float weight = 0.0f;
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel((int)innerX, (int)innerY, new Color(height, weight, 0, 0));
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel(innerX, innerY, new Color(height, newWeight, 0, 0));
                        }
                    }
                }

                // Bottom-right
                for (int innerX = (int)MathF.Round(key) + 300, innerY = (int)MathF.Round(yVal) - 300;
                     innerX > (int)MathF.Round(key) && innerY < (int)MathF.Round(yVal);
                     innerX--, innerY++)
                {
                    if (!((int)MathF.Round(innerX) >= (int)x_axis || innerY >= (int)y_axis || (int)MathF.Round(innerX) < 0 || innerY < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerX - key);
                        float weight = 0.0f;
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel((int)innerX, (int)innerY, new Color(height, weight, 0, 0));
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel(innerX, innerY, new Color(height, newWeight, 0, 0));
                        }
                    }
                }

                // Bottom-left
                for (int innerX = (int)MathF.Round(key) - 300, innerY = (int)MathF.Round(yVal) - 300;
                     innerX < (int)MathF.Round(key) && innerY < (int)MathF.Round(yVal);
                     innerX++, innerY++)
                {
                    if (!((int)MathF.Round(innerX) >= (int)x_axis || innerY >= (int)y_axis || (int)MathF.Round(innerX) < 0 || innerY < 0))
                    {
                        float mainPath = 0.0f;
                        float diff = MathF.Abs(innerX - key);
                        float weight = 0.0f;
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                            innerPathImg.SetPixel((int)innerX, (int)innerY, new Color(height, weight, 0, 0));
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(0.9f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = pathImg.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.G + height * weight) / (pixel.G + weight);
                            float newWeight = Math.Max(pixel.G, weight);
                            pathImg.SetPixel(innerX, innerY, new Color(height, newWeight, 0, 0));
                        }
                    }
                }
            }
        }
    }

    public Image GenerateTerrain(int terrainOffset, int x_axis, int y_axis)
	{
        Stopwatch stopwatch = Stopwatch.StartNew();
		//AddChild((Node)terrain, true);

		GD.Print("start");
        
		FastNoiseLite noise = new FastNoiseLite();
		noise.Frequency = 0.0005f;
		noise.Seed = 1;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.None;
        noise.DomainWarpEnabled = false;

        Image noiseImage = noise.GetImage(x_axis, y_axis);
        GD.Print($"Time elapsed: {stopwatch.Elapsed}");
        GD.Print("get noise");

        noiseImage.Convert(Image.Format.Rgf);
        noiseImage = ApplyGassianAndBoxBlur(noiseImage, RenderingDevice.DataFormat.R32G32Sfloat);
        noiseImage.SavePng("C:\\Users\\jeffe\\test_images\\noise_test.png");
        GD.Print($"Time elapsed: {stopwatch.Elapsed}");


        Image img = Image.Create(x_axis, y_axis, false, Image.Format.Rgf);

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
        GD.Print("GPU Path Generate");
        path.BakeInterval = 0.1f;
        pathImg = GPUGeneratePath(noiseImage, x_axis, y_axis, path.GetBakedPoints());
        GD.Print($"Time elapsed: {stopwatch.Elapsed}");
        /*        foreach(var point in path.GetBakedPoints())
                {
                    GD.Print(point);
                }*/
        //pathImg.SavePng("C:\\Users\\jeffe\\test_images\\gpu_path_test.png");

        GD.Print("blur");
        // Run the blur shader
        pathImg = ApplyGassianAndBoxBlur(pathImg, RenderingDevice.DataFormat.R32G32Sfloat);
        GD.Print($"Time elapsed: {stopwatch.Elapsed}");

        //pathImg.SaveWebp("C:\\Users\\jeffe\\test_images\\blur_test_gausbox.webp");
        //ResourceSaver.Save(pathImg, "C:\\Users\\jeffe\\Desktop\\Untitled41\\scripts\\terrain\\map_output.tres");

        return pathImg;
    }

    public void AddTerrain(string terrainName, Image mapImage = null, bool wantGrass=true)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int x_axis = 8192;//16000; //if you change these a lot of shaders need re-coded
        int y_axis = 4096;//6000; //if you change these a lot of shaders need re-coded
        var terrain = (Variant)GetNode(terrainName);
        terrain.AsGodotObject().Call("set_collision_enabled", false);
        terrain.AsGodotObject().Set("storage", ClassDB.Instantiate("Terrain3DStorage"));
        terrain.AsGodotObject().Set("texture_list", ClassDB.Instantiate("Terrain3DTextureList"));
        terrain.AsGodotObject().Set("texture_list", GD.Load("res://terrainData/texture_list.tres"));

        if(mapImage == null)
        {
            mapImage = GenerateTerrain(0, x_axis, y_axis);
        }
        Global.debugLog("import");
        terrain.AsGodotObject().Get("storage").AsGodotObject().Call("import_images", new Image[] { mapImage, null, null }, new Vector3(0, 0, 0), 0.0f, 400.0f);
        GD.Print($"Time elapsed: {stopwatch.Elapsed}");
        //hole testing
        /*        var terrainUtil = ClassDB.Instantiate("Terrain3DUtil");
                int bits = (int)terrainUtil.AsGodotObject().Call("enc_base", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_overlay", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_blend", (0)) |
                   (int)terrainUtil.AsGodotObject().Call("enc_auto", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_nav", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_hole", (1));
                Color hole_color = new Color((float)terrainUtil.AsGodotObject().Call("as_float", bits), 0f, 0f, 1f);

                for (int x = 1000; x < 2000; x++)
                {
                    for (int z = 1000; z < 2000; z++)
                    {
                        terrain.AsGodotObject().Get("storage").AsGodotObject().Call("set_control", new Vector3(x, 0, z), hole_color);
                        terrain.AsGodotObject().Get("storage").AsGodotObject().Call("set_pixel", 1, new Vector3(x, 0, z), hole_color);
                    }
                }
                terrain.AsGodotObject().Get("storage").AsGodotObject().Call("force_update_maps", 1);*/

        GD.Print("navigation");
        // Enable collision. Enable the first if you wish to see it with Debug/Visible Collision Shapes
        //terrain.AsGodotObject().Call("set_show_debug_collision", true);
        terrain.AsGodotObject().Call("set_collision_enabled", true);

        //make some grass
        if (wantGrass) 
        {
            GrassMeshMaker GrassMeshMaker = (GrassMeshMaker)GetNode("GrassMeshMaker");
            GrassMeshMaker.SetupGrass("/Player", mapImage);
        }

        //Enable runtime navigation baking using the terrain
        Node runtime_nav_baker = GetNode("RuntimeNavigationBaker");
        runtime_nav_baker.Set("terrain", terrain);
        runtime_nav_baker.Set("enabled", true);
        GD.Print($"Terrrain Full Time elapsed: {stopwatch.Elapsed}");

    }
}
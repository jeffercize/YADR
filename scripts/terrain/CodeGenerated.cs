using Godot;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot.Collections;

public partial class CodeGenerated : Node
{
    public override void _Ready()
    {
        // Called every time the node is added to the scene.
        // Initialization here.
        GD.Print("Hello from C# to Godot :)");
		GenerateTerrain();
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

    [StructLayout(LayoutKind.Sequential)]
    public struct BlurParams
    {
        public int radius;
        public float sigma;
    }

    public Image ApplyNormalizedBoxFilter(Image image, int neighborRadius)
    {
        Image filtered = Image.Create(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgbaf);

        for (int x = 0; x < image.GetWidth(); x++)
        {
            for (int y = 0; y < image.GetHeight(); y++)
            {
                float avgR = 0, avgG = 0, avgB = 0;
                int count = 0;

                for (int dx = -neighborRadius; dx <= neighborRadius; dx++)
                {
                    for (int dy = -neighborRadius; dy <= neighborRadius; dy++)
                    {
                        int nx = x + dx, ny = y + dy;

                        if (nx >= 0 && nx < image.GetWidth() && ny >= 0 && ny < image.GetHeight())
                        {
                            Color neighbor = image.GetPixel(nx, ny);
                            avgR += neighbor.R;
                            avgG += neighbor.G;
                            avgB += neighbor.B;
                            count++;
                        }
                    }
                }

                filtered.SetPixel(x, y, new Color(avgR / count, avgG / count, avgB / count));
            }
        }

        return filtered;
    }

    public Image ApplyGaussianBlur(Image image, int radius, float sigma)
    {
        Image blurred = Image.Create(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgbaf);

        // Create the Gaussian kernel
        float[,] kernel = new float[radius * 2 + 1, radius * 2 + 1];
        float sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            for (int j = -radius; j <= radius; j++)
            {
                float value = (float)(Math.Exp(-(i * i + j * j) / (2 * sigma * sigma)) / (2 * Math.PI * sigma * sigma));
                kernel[i + radius, j + radius] = value;
                sum += value;
            }
        }

        // Normalize the kernel
        for (int i = 0; i < radius * 2 + 1; i++)
        {
            for (int j = 0; j < radius * 2 + 1; j++)
            {
                kernel[i, j] /= sum;
            }
        }

        // Apply the kernel to each pixel
        for (int x = 0; x < image.GetWidth(); x++)
        {
            for (int y = 0; y < image.GetHeight(); y++)
            {
                float r = 0, g = 0, b = 0;

                for (int i = -radius; i <= radius; i++)
                {
                    for (int j = -radius; j <= radius; j++)
                    {
                        int nx = x + i, ny = y + j;

                        if (nx >= 0 && nx < image.GetWidth() && ny >= 0 && ny < image.GetHeight())
                        {
                            Color neighbor = image.GetPixel(nx, ny);
                            float weight = kernel[i + radius, j + radius];
                            r += neighbor.R * weight;
                            g += neighbor.G * weight;
                            b += neighbor.B * weight;
                        }
                    }
                }

                blurred.SetPixel(x, y, new Color(r, g, b));
            }
        }

        return blurred;
    }

    public void GenerateTerrain()
	{
		var terrain = (Variant)GetNode("Terrain3D");
		terrain.AsGodotObject().Call("set_collision_enabled", false);
		terrain.AsGodotObject().Set("storage", ClassDB.Instantiate("Terrain3DStorage"));
		terrain.AsGodotObject().Set("texture_list", ClassDB.Instantiate("Terrain3DTextureList"));
		
		var terrainMaterial = terrain.AsGodotObject().Get("material");
		terrainMaterial.AsGodotObject().Set("world_background", 2);
		terrainMaterial.AsGodotObject().Set("auto_shader", true);
		terrainMaterial.AsGodotObject().Set("dual_scaling", true);
        terrain.AsGodotObject().Set("material", terrainMaterial);

        terrain.AsGodotObject().Set("texture_list", GD.Load("res://terrainData/texture_list.tres"));
		//AddChild((Node)terrain, true);

		GD.Print("start");

		FastNoiseLite noise = new FastNoiseLite();
		noise.Frequency = 0.0005f;
		noise.Seed = 2;
		int x_axis = 8192;//16000; //if you change these a lot of shaders need re-coded
        int y_axis = 4096;//6000; //if you change these a lot of shaders need re-coded
        Image img = Image.Create(x_axis, y_axis, false, Image.Format.Rgbaf);

		Curve3D path = new Curve3D();
        /*path.AddPoint(new Vector3(0, 0, 0f));
        path.AddPoint(new Vector3(2048, 1024, 0.1f), new Vector3(-50.0f, -50.0f, 0.0f), new Vector3(500.0f, 500.0f, 0.0f));
        path.AddPoint(new Vector3(3048, 3024, 0.5f), new Vector3(-50.0f, -50.0f, 0.0f), new Vector3(500.0f, 1000.0f, 0.0f));
        path.AddPoint(new Vector3(4096, 2500, 0.3f), new Vector3(-500.0f, -500.0f, 0.0f), new Vector3(50.0f, 50.0f, 0.0f));
        path.AddPoint(new Vector3(8192, 0, 0.0f));*/
        path.AddPoint(new Vector3(100, 0, 1.0f));
        path.AddPoint(new Vector3(100, 1500, 0.9f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(200, 500, 0.8f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(300, 1500, 0.7f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(400, 500, 0.6f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(500, 1500, 0.5f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(600, 500, 0.4f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(700, 1500, 0.3f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(800, 500, 0.2f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(900, 1500, 0.1f), new Vector3(-50.0f, 0.0f, 0.0f), new Vector3(50.0f, 0.0f, 0.0f));
        path.AddPoint(new Vector3(8192, 0, 0.0f));

        List<Vector3> data = new List<Vector3>(path.Tessellate(10, 1));
        List<Vector3> path_result = new List<Vector3>();
        for (int i = 0; i < data.Count - 1; i++)
        {
            float diff = MathF.Abs(data[i + 1].X - data[i].X) + MathF.Abs(data[i + 1].Y - data[i].Y);
            float maxGap = 1.0f;
            //GD.Print(diff);
            if (diff > maxGap)
            {
                int steps = (int)MathF.Ceiling(diff/maxGap);
                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    float x = Lerp(data[i].X, data[i + 1].X, t);
                    float y = Lerp(data[i].Y, data[i + 1].Y, t);
                    float z = Lerp(data[i].Z, data[i + 1].Z, t);
                    path_result.Add(new Vector3(x, y, z));
                }
            }
            else
            {
                path_result.Add(data[i]);
            }
        }
        GD.Print("path building");
        //List<Vector3> data = new List<Vector3>(path.GetBakedPoints());
        /*        List<Vector3> sortedData = data.Select(v => new Vector3(MathF.Round(v.X), MathF.Round(v.Y), v.Z))
                                                .OrderBy(v => v.X)
                                                .ThenBy(v => v.Y)
                                                .ToList();
                List<Vector3> path_result = new List<Vector3>();

                for (int i = 0; i < sortedData.Count - 1; i++)
                {
                    path_result.Add(sortedData[i]);
                    // Check if there is a gap in the x values
                    if (sortedData[i + 1].X - sortedData[i].X > 1)
                    {
                        // Interpolate the y and z values and insert a new row
                        for (float x = sortedData[i].X + 1; x < sortedData[i + 1].X; x++)
                        {
                            float t = (x - sortedData[i].X) / (sortedData[i + 1].X - sortedData[i].X);
                            float y = (int)MathF.Round(Lerp(sortedData[i].Y, sortedData[i + 1].Y, t));
                            float z = Lerp(sortedData[i].Z, sortedData[i + 1].Z, t);
                            path_result.Add(new Vector3(x, y, z));
                        }
                    }
                    //check if we should be filling on Y because we have gone vertical or nearly vertical
        *//*            else if(MathF.Abs(sortedData[i + 1].Y - sortedData[i].Y) > 30)

                        float t = (y - sortedData[i].Y) / (sortedData[i + 1].Y - sortedData[i].Y);
                        float z = Lerp(sortedData[i].Z, sortedData[i + 1].Z, t);
                        path_result.Add(new Vector3(sortedData[i].X, , z);
                    }*//*
                    // Check if there is a gap in the y values
                    if (sortedData[i + 1].Y - sortedData[i].Y > 1)
                    {
                        // Interpolate the x and z values and insert a new row
                        for (float y = sortedData[i].Y + 1; y < sortedData[i + 1].Y; y++)
                        {
                            float t = (y - sortedData[i].Y) / (sortedData[i + 1].Y - sortedData[i].Y);
                            float x = sortedData[i].X;
                            float z = Lerp(sortedData[i].Z, sortedData[i + 1].Z, t);
                            path_result.Add(new Vector3(x, y, z));
                        }
                    }
                }
                path_result.Add(sortedData[^1]);*/
        //List<Vector3> path_result = sortedData;

        Image test_img = Image.Create(x_axis, y_axis, false, Image.Format.Rgbaf); 
        var pathDict = path_result.GroupBy(p => p.X).ToDictionary(g => g.Key, g => g.Select(p => p.Y).ToList());
        foreach (var key in pathDict.Keys.OrderBy(x => x))
        {
            //GD.Print($"{key} {pathDict[key].Count}");
            var yValues = pathDict[key];
            foreach (var yVal in yValues.Distinct())
            {
                float height = 0.0f;
                float pathHeight = path_result.FirstOrDefault(p => p.X == key && p.Y == yVal).Z;
                float midVal = yVal;
                for(int innerY = (int)MathF.Round(yVal) - 300; innerY < (int)MathF.Round(yVal) + 300; innerY++) 
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
                        }
                        else if (innerY > yVal - 300.0f && innerY < yVal + 300.0f)
                        {
                            height = pathHeight;
                            //height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) / 300.0f) + noise.GetNoise2D((int)key, innerY) * Lerp(0.0f, 1.0f, (diff - 30.0f) / 300.0f);
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel((int)key, (int)innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel((int)key, (int)innerY, new Color(height, mainPath, newWeight, 1));
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
                        //GD.Print($"{innerX}, {key}");
                        //calculate height and shit
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            //height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) / 300.0f) + noise.GetNoise2D((int)key, yVal) * Lerp(0.0f, 1.0f, (diff - 30.0f) / 300.0f);
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel(innerX, (int)MathF.Round(yVal));
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel(innerX, (int)MathF.Round(yVal), new Color(height, mainPath, newWeight, 1));
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
                        //GD.Print($"{innerX}, {key}");
                        //calculate height and shit
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel(innerX, innerY, new Color(height, mainPath, newWeight, 1));
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
                        //GD.Print($"{innerX}, {key}");
                        //calculate height and shit
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel(innerX, innerY, new Color(height, mainPath, newWeight, 1));
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
                        //GD.Print($"{innerX}, {key}");
                        //calculate height and shit
                        if (innerX > key - 30.0f && innerX < key + 30.0f)
                        {
                            height = pathHeight;
                            mainPath = 1.0f;
                            weight = 1.0f;
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel(innerX, innerY, new Color(height, mainPath, newWeight, 1));
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
                        }
                        else if (innerX > key - 300.0f && innerX < key + 300.0f)
                        {
                            height = pathHeight;
                            weight = Lerp(1.0f, 0.0f, (diff - 30.0f) / 270.0f);
                        }
                        Color pixel = test_img.GetPixel(innerX, innerY);
                        if (pixel.G != 1)
                        {
                            float weightedHeight = (pixel.R * pixel.B + height * weight) / (pixel.B + weight);
                            float newWeight = Math.Max(pixel.B, weight);
                            test_img.SetPixel(innerX, innerY, new Color(height, mainPath, newWeight, 1));
                        }
                    }
                }

            }
        }
       /* for (var x = 0; x < x_axis; x++)
        {
            float height = 0.0f;
            float mainPath = 0.0f;
            float midVal = path_result[x].Y;
            float pathHeight = path_result[x].Z;
            for (int y = 0; y < y_axis; y++)
            {
                float diff = MathF.Abs(y - midVal);

                if (y > midVal - 30.0f && y < midVal + 30.0f)
                {
                    height = pathHeight;
                    mainPath = 1.0f;
                }
                else if (y > midVal - 60.0f && y < midVal + 60.0f)
                {
                    height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) /60.0f) + noise.GetNoise2D(x, y) * Lerp(0.0f, 1.0f, (diff - 30.0f) / 60.0f);
                }
                img.SetPixel(x, y, new Color(height, mainPath, 0, 1));
            }
            for (var innerX = x - 60; innerX < x + 60; innerX++)
            {
                float diff = MathF.Abs(x - midVal);
                if (innerX > midVal - 30.0f && innerX < midVal + 30.0f)
                {
                    height = pathHeight;
                    mainPath = 1.0f;
                }
                else if (innerX > midVal - 60.0f && innerX < midVal + 60.0f)
                {
                    height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) / 60.0f) + noise.GetNoise2D(x, y) * Lerp(0.0f, 1.0f, (diff - 30.0f) / 60.0f);
                }
                img.SetPixel(innerX, (int)path_result[x].Y, new Color(height, mainPath, 0, 1));
            }
           
        }*/

        test_img.SavePng("C:\\Users\\jeffe\\test_images\\path_test.png");
        
        //test_img = Image.LoadFromFile("C:\\Users\\jeffe\\test_images\\path_test_blur2.png");
        
        GD.Print("blur");
        //test_img = ApplyNormalizedBoxFilter(test_img, 5);
        Image out_img = Image.Create(x_axis, y_axis, false, Image.Format.Rgbaf);
        // Create a local rendering device.
        var rd = RenderingServer.CreateLocalRenderingDevice();

        // Load GLSL shader
        RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://scripts/terrain/gausianblur.glsl");
        RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
        Rid shader = rd.ShaderCreateFromSpirV(shaderBytecode);
        
        //Setup Input Image
        RDSamplerState samplerState = new RDSamplerState();
        Rid sampler = rd.SamplerCreate(samplerState);
        RDTextureFormat inputFmt = new RDTextureFormat();
        inputFmt.Width = (uint)test_img.GetWidth();
        inputFmt.Height = (uint)test_img.GetHeight();
        inputFmt.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
        inputFmt.UsageBits = RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.CanUpdateBit;
        RDTextureView inputView = new RDTextureView();

        byte[] inputImageData = test_img.GetData();
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
        fmt.Width = (uint)x_axis;
        fmt.Height = (uint)y_axis;
        fmt.Format = RenderingDevice.DataFormat.R32G32B32A32Sfloat;
        fmt.UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanUpdateBit | RenderingDevice.TextureUsageBits.CanCopyFromBit;
        RDTextureView view = new RDTextureView();
        Image output_image = Image.Create(x_axis, y_axis, false, Image.Format.Rgbaf);
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

        //create the uniformSet
        var uniformSet = rd.UniformSetCreate(new Array<RDUniform> { samplerUniform, outputTexUniform }, shader, 0);

        // Create a compute pipeline
        var pipeline = rd.ComputePipelineCreate(shader);
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
        int threadsPerGroup = 32;
        uint xGroups = (uint)(out_img.GetWidth() + threadsPerGroup - 1) / (uint)threadsPerGroup;
        uint yGroups = (uint)(out_img.GetHeight() + threadsPerGroup - 1) / (uint)threadsPerGroup;
        rd.ComputeListDispatch(computeList, xGroups, yGroups, 1);
        rd.ComputeListEnd();

        // Submit to GPU and wait for sync
        rd.Submit();
        rd.Sync();

        //Get Data
        var byteData = rd.TextureGetData(output_tex, 0);
        out_img = Image.CreateFromData(x_axis, y_axis, false, Image.Format.Rgbaf, byteData);
        //Image outputImg = Image.CreateFromData(x_axis, y_axis, false, Image.Format.Rgbaf, rd.TextureGetData(outputImage, 0));
        //outputImg.SavePng("C:\\Users\\jeffe\\test_images\\blur_test.png");
        out_img.SavePng("C:\\Users\\jeffe\\test_images\\blur_test_GLSL.png");

        GD.Print("loop");
        for (int x = 0; x < x_axis; x++)
		{
			//GD.Print("X");
			bool edge_height = false;
            if (x < (0 + 200))
            {
				edge_height = true;
            }

			float height = 0.0f;
            for (int y = 0; y < y_axis; y++)
			{
				height = noise.GetNoise2D(x, y) * 1.0f;

				if (height < 0)
				{
					height = height / ((1 - height) * (1.0f - height));
				}
                if (y < (0 + 200))
                {
                    height = height * (y / 200.0f);
                }
                else if (y > (y_axis - 200))
                {
                    height = height * ((y_axis - y) / 200.0f);
                }
                if (edge_height)
                {
                    height = height * (x / 200.0f);
                }

                img.SetPixel(x, y, new Color(height, 0, 0, 1));
			}
        }
        Image final_img = Image.Create(x_axis, y_axis, false, Image.Format.Rgbaf);
        for (int x = 0; x < x_axis; x++)
        {
            for(int y = 0; y < y_axis; y++)
            {
                Color terrainPix = img.GetPixel(x, y);
                float terrainHeight = terrainPix.R;
                Color pathPix = out_img.GetPixel(x, y);
                float pathHeight = pathPix.R;
                float pathWeight = pathPix.B;
                float newHeight = (pathHeight * pathWeight) + (terrainHeight * (1 - pathWeight));
                //GD.Print($"{pathHeight}, {pathWeight}| {terrainHeight}, {newHeight}");
                final_img.SetPixel(x, y, new Color( newHeight, 0, 0, 1));
            }
        }



        final_img.SavePng("C:\\Users\\jeffe\\test_images\\test.png");
		GD.Print("import");
		terrain.AsGodotObject().Get("storage").AsGodotObject().Call("import_images", new Image[] { final_img, null, null }, new Vector3(0, -2024, -2024), 0.0f, 400.0f);

        //hole testing
        var terrainUtil = ClassDB.Instantiate("Terrain3DUtil");
        int bits = (int)terrainUtil.AsGodotObject().Call("enc_base", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_overlay", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_blend", (0)) |
           (int)terrainUtil.AsGodotObject().Call("enc_auto", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_nav", (0)) | (int)terrainUtil.AsGodotObject().Call("enc_hole", (1));
        Color hole_color = new Color((float)terrainUtil.AsGodotObject().Call("as_float", bits), 0f, 0f, 1f);

        for (int x = 1000; x < 2000; x++)
		{
			for (int z = 1000; z < 2000; z++)
			{
				//terrain.AsGodotObject().Get("storage").AsGodotObject().Call("set_control", new Vector3(x, 0, z), hole_color);
                //terrain.AsGodotObject().Get("storage").AsGodotObject().Call("set_pixel", 1, new Vector3(x, 0, z), hole_color);
			}
		}
		terrain.AsGodotObject().Get("storage").AsGodotObject().Call("force_update_maps", 1);

        GD.Print("navigation");
		// Enable collision. Enable the first if you wish to see it with Debug/Visible Collision Shapes
		terrain.AsGodotObject().Call("set_show_debug_collision", true);
		terrain.AsGodotObject().Call("set_collision_enabled", true);

        //Enable runtime navigation baking using the terrain
        Node runtime_nav_baker = GetNode("RuntimeNavigationBaker");
		runtime_nav_baker.Set("terrain", terrain);
		runtime_nav_baker.Set("enabled", true);

		//Retreive 512x512 region blur map showing where the regions are
		//var rbmap_rid: RID = terrain.material.get_region_blend_map()

		//img = RenderingServer.texture_2d_get(rbmap_rid)
    }
}
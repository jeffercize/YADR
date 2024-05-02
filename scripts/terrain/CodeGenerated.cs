using Godot;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Buffers.Text;

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
		noise.Seed = 1;
		int x_axis = 8192;//16000;
		int y_axis = 4096;//6000;
        Image img = Image.Create(x_axis, y_axis, false, Image.Format.Rf);

		Curve3D path = new Curve3D();
		path.AddPoint(new Vector3(0, 0, 0f));
		path.AddPoint(new Vector3(2048, 1024, 0.1f), new Vector3(-50.0f, -50.0f, 0.0f), new Vector3(500.0f, 500.0f, 0.0f));
		path.AddPoint(new Vector3(3048, 3024, 0.5f), new Vector3(-50.0f, -50.0f, 0.0f), new Vector3(500.0f, 1000.0f, 0.0f));
		path.AddPoint(new Vector3(4096, 2500, 0.3f), new Vector3(-500.0f, -500.0f, 0.0f), new Vector3(50.0f, 50.0f, 0.0f));
		path.AddPoint(new Vector3(8192, 0, 0.5f));
		

		List<Vector3> newArray = new List<Vector3>();
		Dictionary<string, bool> seen = new Dictionary<string, bool>();

		List<Vector3> data = new List<Vector3>(path.Tessellate(7, 6));//path.GetBakedPoints());
        List<Vector3> sortedData = data.Select(v => new Vector3(MathF.Round(v.X), v.Y, v.Z))
                                       .OrderBy(v => v.X)
                                       .ToList();

        List<Vector3> result = new List<Vector3>();

        for (int i = 0; i < sortedData.Count - 1; i++)
        {
            result.Add(sortedData[i]);
            // Check if there is a gap in the x values
            if (sortedData[i + 1].X - sortedData[i].X > 1)
            {
                // Interpolate the z value and insert a new row
                for (float x = sortedData[i].X + 1; x < sortedData[i + 1].X; x++)
                {
                    float t = (x - sortedData[i].X) / (sortedData[i + 1].X - sortedData[i].X);
                    float y = Lerp(sortedData[i].Y, sortedData[i + 1].Y, t);
                    float z = Lerp(sortedData[i].Z, sortedData[i + 1].Z, t);
                    result.Add(new Vector3(x, y, z));
                }
            }
        }

        // Don't forget to add the last data point
        result.Add(sortedData[^1]);
		GD.Print("loop");
		float prevHeight = 0.0f;
		float prevPathHeight = 0.0f;
		for (int x = 0; x < x_axis; x++)
		{
			//GD.Print("X");
			bool edge_height = false;
            if (x < (0 + 200))
            {
				edge_height = true;
            }
			bool end_height = false;
			if (x > (x_axis - 300))
			{
				end_height = true;
            }

			Vector3 path_vector = result[x];
			float midVal = path_vector.Y;
			float pathHeight = path_vector.Z;
			float height = 0.0f;
            for (int y = 0; y < y_axis; y++)
			{
				float diff = MathF.Abs(y - midVal);
				//if (y < 1) 
				//{
                    //GD.Print("(" +x+", "+ y+")" + ": " + diff + ", " + midVal);
                //}
				
                if (y > midVal - 30.0f && y < midVal + 30.0f)
				{
					height = pathHeight;
				}
				else if (y > midVal - 60.0f && y < midVal + 60.0f)
				{
					//GD.Print(y);
					//GD.Print(pathHeight * Lerp(1.0f, 0.0f, diff * 0.0025f) + noise.GetNoise2D(x, y));
					//GD.Print(noise.GetNoise2D(x, y) * Lerp(0.0f, 1.0f, diff * 0.0025f));

					height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) * 0.0025f) + noise.GetNoise2D(x, y) * Lerp(0.0f, 1.0f, (diff-30.0f) * 0.0025f);
                }
				else if (y > midVal - 430.0f && y < midVal + 430.0f)
				{
					height = pathHeight * Lerp(1.0f, 0.0f, (diff - 30.0f) * 0.0025f) + noise.GetNoise2D(x, y) * Lerp(0.0f, 1.0f, (diff - 30.0f) * 0.0025f);
                }
                else
				{
					height = noise.GetNoise2D(x, y) * 1.0f;
				}
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
/*				else if (end_height)cfvcf
				{
					height = height * (1.0f + (x / x_axis));
				}*/

                img.SetPixel(x, y, new Color(height, 0, 0, 1));
			}
        }
		img.SavePng("C:\\Users\\jeffe\\test_images\\test.png");
		GD.Print("import");
		terrain.AsGodotObject().Get("storage").AsGodotObject().Call("import_images", new Image[] { img, null, null }, new Vector3(0, 0, 0), 0.0f, 400.0f);

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
                terrain.AsGodotObject().Get("storage").AsGodotObject().Call("set_pixel", 1, new Vector3(x, 0, z), hole_color);
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
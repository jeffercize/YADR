using Godot;
using Godot.NativeInterop;
using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using Godot.Collections;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

public partial class TerrainChunk : Node3D
{
    public static int highQuality = 1;
    public static int midQuality = 16;
    public static int lowQuality = 32;

    ImageTexture heightMapTexture;
    CompressedTexture2D rock;
    CompressedTexture2D grass;
    CompressedTexture2D road;

    CompressedTexture2D rockNormal;
    CompressedTexture2D grassNormal;

    Shader terrainShader;

    Image mapImage;
    Image paddedImg;
    float heightScale;
    int x_axis;
    int y_axis;
    int offsetX;
    int offsetY;
    int quality;
    bool wantGrass;
    GrassMeshMaker myGrassMeshMaker;
    TerrainGeneration parent;
    RDShaderFile blendShaderFile;
    Shader grassShader;
    Vector3 myGlobalPosition;

    Rid shape;
    Rid staticBody;
    Rid mesh;
    Rid instance;
    Rid materialShader;
    Rid terrainMaterial;

    Rid region;

    Thread setupMeshThread;

    bool cleaningUp = false;
    bool deployedMesh = false;

    Dictionary shape_data;

    //get rid of for production
    StaticBody3D _debugStaticBody;
    CollisionShape3D debugColShape;
    HeightMapShape3D hshape;

    public TerrainChunk(Image mapImage, Image paddedImg, float heightScale, int x_axis, int y_axis, int offsetX, int offsetY, bool wantGrass, int quality, TerrainGeneration parent, RDShaderFile blendShaderFile, Shader grassShader, CompressedTexture2D rock, CompressedTexture2D grass, CompressedTexture2D road, CompressedTexture2D rockNormal, CompressedTexture2D grassNormal, Shader terrainShader)
	{
        this.mapImage = mapImage;
        this.paddedImg = paddedImg;
        this.heightScale = heightScale;
        this.x_axis = x_axis;
        this.y_axis = y_axis;
        this.offsetX = offsetX;
        this.offsetY = offsetY;
        this.wantGrass = wantGrass;
        this.quality = quality;
        this.parent = parent;
        this.blendShaderFile = blendShaderFile;
        this.grassShader = grassShader;
        this.rock = rock;
        this.grass = grass;
        this.road = road;
        this.rockNormal = rockNormal;
        this.grassNormal = grassNormal;
        this.terrainShader = terrainShader;
    }
    public override void _Ready()
    {
        Stopwatch sw = Stopwatch.StartNew();

        if(quality <= highQuality)
        {
            //BuildDebugCollision(paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX * 4.0f, 0.0f, offsetY * 4.0f), quality);
            Thread setupColliderThread = new Thread(() => BuildCollision(paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX*4.0f, 0.0f, offsetY*4.0f), quality));
            setupColliderThread.Start();
        }
        setupMeshThread = new Thread(() => BuildMesh(paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX*4.0f, 0.0f, offsetY*4.0f), quality));
        setupMeshThread.Start();                                  
        //BuildMesh(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX, 0.0f, offsetY));

        //Thread buildNavMeshThread = new Thread(() => terrainChunk.BuildNavigationMesh(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX, 0.0f, offsetY)));
        //buildNavMeshThread.Start();
        //terrainChunk.BuildNavigationMesh(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX, 0.0f, offsetY), navMap); //TODO this seems like the correct way to do
        //it but after a long stutter there is still no nav mesh, not sure what to try next, check docs or terrain3d more :|

        if (wantGrass)
        {
            myGrassMeshMaker = new GrassMeshMaker();
            AddChild(myGrassMeshMaker);
            //grassMeshMakerNode.AddChild(myGrassManager);
            Thread setupGrassThread = new Thread(() => myGrassMeshMaker.SetupGrass(paddedImg, offsetX, offsetY, x_axis, y_axis, null, null, this, parent, blendShaderFile, grassShader));
            setupGrassThread.Start();
            //myGrassManager.SetupGrass(paddedImg, offsetX, offsetY, x_axis, y_axis, null, null);
        }

        if (sw.ElapsedMilliseconds > 4)
        {
            GD.Print($"Full Chunk Ready Time elapsed: {sw.ElapsedMilliseconds}");
        }
    }

    public void PrepForFree()
    {
        //RenderingServer.InstanceSetVisible(instance, false);
    }

    public void RebuildChunk(Image mapImage, Image paddedImg, int x_axis, int y_axis, int offsetX, int offsetY)
    {
        this.mapImage = mapImage;
        this.paddedImg = paddedImg;
        this.x_axis = x_axis;
        this.y_axis = y_axis;
        this.offsetX = offsetX;
        this.offsetY = offsetY;


        //paddedImg.SavePng("C:\\Users\\jeffe\\test_images\\paddedmap" + "(" + offsetX + "," + offsetY + ")" + ".png");
        //mapImage.SavePng("C:\\Users\\jeffe\\test_images\\heightmap" + "(" + offsetX + "," + offsetY + ")" + ".png");


        //we need to check if the mesh setup thread is running because it is potentially still working
        Stopwatch sw = Stopwatch.StartNew();
        while (setupMeshThread.IsAlive)
        {
            setupMeshThread.Join();
            if(sw.ElapsedMilliseconds > 4)
            {
                GD.Print("WAITING FOR THREAD CRInGe");
            }
            if(!deployedMesh)
            {
                //shouldn't be possible if we pre-load on launch properly :)
                GD.Print("yo the mesh isnt even deployed");
            }
        }
        Thread rebuildMeshThread = new Thread(() => RebuildMesh(new Vector3(offsetX*4.0f, 0.0f, offsetY*4.0f)));
        rebuildMeshThread.Start();
        if (quality <= highQuality)
        {
            //CallDeferred(TerrainChunk.MethodName.RebuildDebugCollision, paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX * 4.0f, 0.0f, offsetY * 4.0f), quality);
            // RebuildDebugCollision(paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX * 4.0f, 0.0f, offsetY * 4.0f), quality);
            RebuildCollisionMesh(paddedImg, heightScale, x_axis, y_axis, new Vector3(offsetX * 4.0f, 0.0f, offsetY * 4.0f), quality);
        }


        //RebuildNavigationMesh();

        if (wantGrass)
        {
            //myGrassMeshMaker.RebuildGrass();
        }
    }

    public void BuildCollision(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition, int resolution)
    {
        width = (width / resolution) + 1;
        depth = (depth / resolution) + 1;
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                int iFilterDirection = 1;
                int jFilterDirection = 1;
                if(i < width - 1)
                {
                    if(j < depth - 1)
                    {
                       //lazy if statements 
                    }
                    else
                    {
                        jFilterDirection = 0;
                    }
                }
                else if(j < depth - 1)
                {
                    iFilterDirection = 0;
                }
                else
                {
                    iFilterDirection = 0;
                    jFilterDirection = 0;
                }

                float s1 = heightMap.GetPixel(i * resolution + 15 , j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                float s2 = heightMap.GetPixel(i * resolution + 15 + iFilterDirection, j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                float s3 = heightMap.GetPixel(i * resolution + 15 , j * resolution + 15 + jFilterDirection).R * heightScale; //+16 because we are using the padded image
                float s4 = heightMap.GetPixel(i * resolution + 15 + iFilterDirection, j * resolution + 15 + jFilterDirection).R * heightScale; //+16 because we are using the padded image

                mapData[index] = (s1+s2+s3+s4)/4.0f;
                //mapData[index] = heightMap.GetPixel(i * resolution + 15 , j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                if (mapData[index] < minHeight)
                {
                    minHeight = mapData[index];
                }
                if (mapData[index] > maxHeight)
                {
                    maxHeight = mapData[index];
                }
            }
        }

        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), new Vector3(width * 0.5f - 0.5f, 0.0f, depth * 0.5f - 0.5f)); 
        xform = xform.Scaled(new Vector3((float)4.0f*resolution, 1.0f, (float)4.0f*resolution)); //multiple by vertex spacing and the resolution
        xform = xform.Translated(globalPosition);


        shape_data = new Dictionary();
        shape_data["width"] = width;
        shape_data["depth"] = depth;
        shape_data["heights"] = mapData;
        shape_data["min_height"] = minHeight;
        shape_data["max_height"] = maxHeight;
        if (cleaningUp == false)
        {
            CallDeferred(TerrainChunk.MethodName.DeployCollision, shape_data, xform);
        }
    }
    public bool DeployCollision(Dictionary shape_data, Transform3D xform)
	{
        Stopwatch sw = Stopwatch.StartNew();
        staticBody = PhysicsServer3D.BodyCreate();
        PhysicsServer3D.BodySetMode(staticBody, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodySetSpace(staticBody, GetWorld3D().Space);
        PhysicsServer3D.BodyAttachObjectInstanceId(staticBody, GetInstanceId());
        shape = PhysicsServer3D.HeightmapShapeCreate();
        PhysicsServer3D.ShapeSetData(shape, shape_data);
        PhysicsServer3D.BodySetCollisionMask(staticBody, 1);
        PhysicsServer3D.BodySetCollisionLayer(staticBody, 1);
        PhysicsServer3D.BodySetCollisionPriority(staticBody, 1);

        if(sw.ElapsedMilliseconds > 2)
        {
            GD.Print("Collision Deploy Time: " + sw.ElapsedMilliseconds);
        }
        parent.queuedPhysicsShapes.Enqueue((staticBody, shape, xform));
        //this line takes 99% of deploy time and can spike to 150ms (randomly?)
        //PhysicsServer3D.BodyAddShape(staticBody, shape, xform);
        return true;
    }

    public bool BuildDebugCollision(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition, int resolution)
    {
        //GD.Print("Building debug collision. Disable this mode for releases");
        _debugStaticBody = new StaticBody3D();
        _debugStaticBody.Name = "StaticBody3D";
        AddChild(_debugStaticBody);
        shape = PhysicsServer3D.HeightmapShapeCreate();
        width = (width / resolution) + 1;
        depth = (depth / resolution) + 1;
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                int iFilterDirection = 1;
                int jFilterDirection = 1;
                if (i < width - 1)
                {
                    if (j < depth - 1)
                    {
                        //lazy if statements 
                    }
                    else
                    {
                        jFilterDirection = 0;
                    }
                }
                else if (j < depth - 1)
                {
                    iFilterDirection = 0;
                }
                else
                {
                    iFilterDirection = 0;
                    jFilterDirection = 0;
                }

                float s1 = heightMap.GetPixel(i * resolution + 15, j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                float s2 = heightMap.GetPixel(i * resolution + 15 + iFilterDirection, j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                float s3 = heightMap.GetPixel(i * resolution + 15, j * resolution + 15 + jFilterDirection).R * heightScale; //+16 because we are using the padded image
                float s4 = heightMap.GetPixel(i * resolution + 15 + iFilterDirection, j * resolution + 15 + jFilterDirection).R * heightScale; //+16 because we are using the padded image

                mapData[index] = (s1 + s2 + s3 + s4) / 4.0f;
                //mapData[index] = heightMap.GetPixel(i * resolution + 15 , j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                if (mapData[index] < minHeight)
                {
                    minHeight = mapData[index];
                }
                if (mapData[index] > maxHeight)
                {
                    maxHeight = mapData[index];
                }
            }
        }

        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), new Vector3(width * 0.5f - 0.5f, 0.0f, depth * 0.5f - 0.5f)); 
        debugColShape = new CollisionShape3D();
        xform = xform.Scaled(new Vector3((float)4.0f*resolution, 1.0f, (float)4.0f*resolution)); //multiple by vertex spacing and the resolution
        xform = xform.Translated(globalPosition);
        debugColShape.Name = "CollisionShape3D";
        _debugStaticBody.AddChild(debugColShape);
        debugColShape.Owner = _debugStaticBody;

        hshape = new HeightMapShape3D();
        hshape.MapWidth = width;
        hshape.MapDepth = depth;
        hshape.MapData = mapData;
        debugColShape.Shape = hshape;
        debugColShape.GlobalTransform = xform;
        _debugStaticBody.CollisionMask = 1;
        _debugStaticBody.CollisionLayer = 1;
        _debugStaticBody.CollisionPriority = 1;

        return true;
    }

    public bool BuildNavigationMesh(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition, Rid navMap)
    {
        // Create the NavigationMeshSourceGeometryData3D instance
        NavigationMeshSourceGeometryData3D sourceGeometry = new NavigationMeshSourceGeometryData3D();

        NavigationMesh navigationMesh = new NavigationMesh();


        // Create an array for the vertices
        Vector3[] vertices = new Vector3[width * depth];

        // Populate the vertices array
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                int index = i * width + j;
                float height = heightMap.GetPixel(i, j).R * heightScale;
                vertices[index] = new Vector3(i, height, j);
            }
        }

        // Set the vertices
        navigationMesh.Vertices = vertices;

        // Populate the polygons array
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                // First triangle
                int[] polygon1 = new int[3];
                polygon1[0] = i * width + j;
                polygon1[1] = (i + 1) * width + j;
                polygon1[2] = i * width + j + 1;
                navigationMesh.AddPolygon(polygon1);

                // Second triangle
                int[] polygon2 = new int[3];
                polygon2[0] = (i + 1) * width + j;
                polygon2[1] = (i + 1) * width + j + 1;
                polygon2[2] = i * width + j + 1;
                navigationMesh.AddPolygon(polygon2);
            }
        }


        // Bake the navigation mesh
        //NavigationServer3D.BakeFromSourceGeometryData(navigationMesh, sourceGeometry); //NO POLYGONS GUH

        region = NavigationServer3D.RegionCreate();
        NavigationServer3D.RegionSetTransform(region, new Transform3D(Basis.Identity, globalPosition));
        NavigationServer3D.RegionSetMap(region, navMap);

        NavigationServer3D.RegionSetNavigationMesh(region, navigationMesh);

        NavigationServer3D.RegionSetEnabled(region, true);
        //GD.Print(NavigationServer3D.RegionGetConnectionsCount(region));

        return true;
    }

    private void BuildMesh(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition, int resolution)
    {
        myGlobalPosition = globalPosition;
        // Create an array for the vertices
        width = (width / resolution) + 1;
        depth = (depth / resolution) + 1;
        Vector3[] p_vertices = new Vector3[width * depth];

        // Populate the vertices array
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                int index = i * width + j;
                p_vertices[index] = new Vector3(i * resolution * 4, 0.0f, j * resolution * 4);
            }
        }

        // Create an array for the indices
        int[] p_indices = new int[(width - 1) * (depth - 1) * 6];

        // Populate the indices array
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                int index = i * (width - 1) + j;
                p_indices[index * 6 + 0] = i * width + j;
                p_indices[index * 6 + 1] = (i + 1) * width + j;
                p_indices[index * 6 + 2] = i * width + j + 1;
                p_indices[index * 6 + 3] = (i + 1) * width + j;
                p_indices[index * 6 + 4] = (i + 1) * width + j + 1;
                p_indices[index * 6 + 5] = i * width + j + 1;
            }
        }

        // Create the AABB
        Aabb p_aabb = new Aabb(new Vector3(0, -2000.0f, 0), new Vector3(width*resolution*4, 4000.0f, depth*resolution*4)); // Adjust the height as needed

        // Create an array for the mesh data
        Godot.Collections.Array arrays = new Godot.Collections.Array();
        arrays.Resize((int)RenderingServer.ArrayType.Max);

        // Set the vertices and indices
        arrays[(int)RenderingServer.ArrayType.Vertex] = p_vertices;
        arrays[(int)RenderingServer.ArrayType.Index] = p_indices;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);

        // Create the mesh
        mesh = RenderingServer.MeshCreate();
        RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.Triangles, arrays);
        // Set the custom AABB
        RenderingServer.MeshSetCustomAabb(mesh, p_aabb);

        if (cleaningUp == false)
        {
            //Callable callable = Callable.From(() => DeployMesh(arrays, p_aabb, globalPosition));
            //RenderingServer.CallOnRenderThread(callable);
            parent.queuedChunkShapes.Enqueue(this);
            //CallDeferred(TerrainChunk.MethodName.DeployMesh, arrays, p_aabb, globalPosition);
            //DeployMesh(arrays, p_aabb, globalPosition);
        }
    }

    public bool DeployMesh()
    {
        instance = RenderingServer.InstanceCreate2(mesh, GetWorld3D().Scenario);
        // Set the transform
        Transform3D xform = new Transform3D(Basis.Identity, myGlobalPosition);

        RenderingServer.InstanceSetTransform(instance, xform);
        if(quality >= 8)
        {
            RenderingServer.InstanceGeometrySetVisibilityRange(instance, 1468.0f, 0.0f, 500.0f, 0.0f, RenderingServer.VisibilityRangeFadeMode.Self);
        }
        ShaderMaterial terrainMat = new ShaderMaterial();
        terrainMat.Shader = terrainShader;
        // Create a RID for the material and set its shader
        materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, terrainMat.Shader.Code);
        terrainMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetParam(terrainMaterial, "texture_repeat", false);//TODO this doesnt work, there has to be a way to change wrapping behavior for texture sampling
        RenderingServer.MaterialSetShader(terrainMaterial, materialShader);
        // Set the shader parameters

        RenderingServer.MaterialSetParam(terrainMaterial, "heightMap", heightMapTexture.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "rockTexture", rock.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "grassTexture", grass.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "roadTexture", road.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "rockNormalMap", rockNormal.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "grassNormalMap", grassNormal.GetRid());
        RenderingServer.MaterialSetParam(terrainMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(terrainMaterial, "heightScale", heightScale);
        RenderingServer.InstanceGeometrySetMaterialOverride(instance, terrainMaterial);
        //RenderingServer.MeshSurfaceSetMaterial(mesh, 0, terrainMaterial);
        deployedMesh = true;
        return true;
    }

    public void RebuildMesh(Vector3 globalPosition)
    {
        //RenderingServer.InstanceSetVisible(instance, false);
        heightMapTexture = ImageTexture.CreateFromImage(paddedImg);
        RenderingServer.MaterialSetParam(terrainMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(terrainMaterial, "heightMap", heightMapTexture.GetRid());
        Transform3D xform = new Transform3D(Basis.Identity, globalPosition);
        RenderingServer.InstanceSetTransform(instance, xform);
        RenderingServer.InstanceSetVisible(instance, true);
    }

    public void RebuildDebugCollision(Image heightMap,  float heightScale, int width, int depth, Vector3 globalPosition, int resolution)
    {
        GD.Print("rebuild collider");
        width = (width / resolution) + 1;
        depth = (depth / resolution) + 1;
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i * resolution + 15, j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                if (mapData[index] < minHeight)
                {
                    minHeight = mapData[index];
                }
                if (mapData[index] > maxHeight)
                {
                    maxHeight = mapData[index];
                }
            }
        }

        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), new Vector3(width * 0.5f - 0.5f, 0.0f, depth * 0.5f - 0.5f));
        xform = xform.Scaled(new Vector3((float)4.0f * resolution, 1.0f, (float)4.0f * resolution)); //multiple by vertex spacing and the resolution
        xform = xform.Translated(globalPosition);
        hshape.MapWidth = width;
        hshape.MapDepth = depth;
        CallDeferred(TerrainChunk.MethodName.RedeployDebugCollision, xform, mapData);
    }
    public void RedeployDebugCollision(Transform3D xform, float[] mapData)
    {
        hshape.MapData = mapData;
        debugColShape.GlobalTransform = xform;
        debugColShape.Shape = hshape;
    }

    public void RebuildCollisionMesh(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition, int resolution = 2)
    {
        width = (width / resolution) + 1;
        depth = (depth / resolution) + 1;
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i * resolution + 15, j * resolution + 15).R * heightScale; //+16 because we are using the padded image
                if (mapData[index] < minHeight)
                {
                    minHeight = mapData[index];
                }
                if (mapData[index] > maxHeight)
                {
                    maxHeight = mapData[index];
                }
            }
        }

        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), new Vector3(width * 0.5f - 0.5f, 0.0f, depth * 0.5f - 0.5f));
        xform = xform.Scaled(new Vector3((float)4.0f * resolution, 1.0f, (float)4.0f * resolution)); //multiple by vertex spacing and the resolution
        xform = xform.Translated(globalPosition);

        shape_data = new Dictionary();
        shape_data["width"] = width;
        shape_data["depth"] = depth;
        shape_data["heights"] = mapData;
        shape_data["min_height"] = minHeight;
        shape_data["max_height"] = maxHeight;
        if (cleaningUp == false)
        {
            CallDeferred(TerrainChunk.MethodName.RedeployCollision, shape_data, xform);
        }
    }

    public void RedeployCollision(Dictionary shape_data, Transform3D xform)
    {
        Stopwatch sw = Stopwatch.StartNew();
        PhysicsServer3D.ShapeSetData(shape, shape_data);
        PhysicsServer3D.BodySetShape(staticBody, 0, shape);
        PhysicsServer3D.BodySetShapeTransform(staticBody, 0, xform);

        if (sw.ElapsedMilliseconds > 2)
        {
            GD.Print("Collision Re-Deploy Time: " + sw.ElapsedMilliseconds);
        }
    }

    public void CleanUp()
    {
        Stopwatch sw = Stopwatch.StartNew();
        cleaningUp = true;

        if(shape.IsValid)
        {
            PhysicsServer3D.FreeRid(shape);
        }
        if (staticBody.IsValid)
        {
            PhysicsServer3D.FreeRid(staticBody);
        }
        RenderingServer.FreeRid(mesh);
        RenderingServer.FreeRid(instance);
        RenderingServer.FreeRid(materialShader);
        RenderingServer.FreeRid(terrainMaterial);

        if(region.IsValid)
        {
            NavigationServer3D.FreeRid(region);
        }

        if (myGrassMeshMaker != null)
        {
            myGrassMeshMaker.CleanUp();
        }
        GD.Print("CleanUp Time: " + sw.ElapsedMilliseconds);
        //this.QueueFree();
    }
}

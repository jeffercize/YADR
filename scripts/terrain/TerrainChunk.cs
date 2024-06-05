using Godot;
using Godot.NativeInterop;
using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using Godot.Collections;
using System.Diagnostics;
using System.Threading;

public partial class TerrainChunk : Node3D
{
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
    bool wantGrass;
    GrassMeshMaker myGrassMeshMaker;
    TerrainGeneration parent;
    RDShaderFile blendShaderFile;
    Shader grassShader;


    Rid shape;
    Rid staticBody;
    Rid mesh;
    Rid instance;
    Rid materialShader;
    Rid terrainMaterial;

    Rid region;

    bool cleaningUp = false;

    public TerrainChunk(Image mapImage, Image paddedImg, float heightScale, int x_axis, int y_axis, int offsetX, int offsetY, bool wantGrass, TerrainGeneration parent, RDShaderFile blendShaderFile, Shader grassShader, CompressedTexture2D rock, CompressedTexture2D grass, CompressedTexture2D road, CompressedTexture2D rockNormal, CompressedTexture2D grassNormal, Shader terrainShader)
	{
        this.mapImage = mapImage;
        this.paddedImg = paddedImg;
        this.heightScale = heightScale;
        this.x_axis = x_axis;
        this.y_axis = y_axis;
        this.offsetX = offsetX;
        this.offsetY = offsetY;
        this.wantGrass = wantGrass;
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
        Thread setupColliderThread = new Thread(() => BuildCollision(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX - 0.5f, 0.0f, offsetY - 0.5f)));
        setupColliderThread.Start();
        //BuildCollision(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX - 0.5f, 0.0f, offsetY - 0.5f)); //TODO kinda weird that we adjust by 0.5 here

        Thread setupMeshThread = new Thread(() => BuildMesh(mapImage, heightScale, x_axis, y_axis, new Vector3(offsetX, 0.0f, offsetY)));
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
    float[] mapData;
    Transform3D xform;
    Dictionary shape_data;
    public void BuildCollision(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition)
    {
        mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i, j).R * heightScale;
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
        //Transform3D xform = new Transform3D(Basis.Identity, globalPosition);
        xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), globalPosition + new Vector3(width * 0.5f, 0.0f, depth * 0.5f)); CollisionShape3D debugColShape = new CollisionShape3D();

        // scale the xform if we want to increase or decrease mesh_vertex_spacing, I think a value of 1 is good for now
        //xform.scale(Vector3(_mesh_vertex_spacing, 1.f, _mesh_vertex_spacing));

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

        
        parent.queuedPhysicsShapes.Enqueue((staticBody, shape, xform));
        //this line takes 99% of deploy time and can spike to 150ms (randomly?)
        //PhysicsServer3D.BodyAddShape(staticBody, shape, xform);
        return true;
    }

    public bool BuildDebugCollision(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition)
    {
        //GD.Print("Building debug collision. Disable this mode for releases");
        StaticBody3D _debugStaticBody = new StaticBody3D();
        _debugStaticBody.Name = "StaticBody3D";
        AddChild(_debugStaticBody);

        shape = PhysicsServer3D.HeightmapShapeCreate();
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i, j).R * heightScale;
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

        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), globalPosition + new Vector3(width * 0.5f, 0.0f, depth * 0.5f)); CollisionShape3D debugColShape = new CollisionShape3D();
        debugColShape.Name = "CollisionShape3D";
        _debugStaticBody.AddChild(debugColShape);
        debugColShape.Owner = _debugStaticBody;

        HeightMapShape3D hshape = new HeightMapShape3D();
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

    private void BuildMesh(Image heightMap, float heightScale, int width, int depth, Vector3 globalPosition)
    {
        // Create an array for the vertices
        Vector3[] p_vertices = new Vector3[width * depth];

        // Populate the vertices array
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                int index = i * width + j;
                p_vertices[index] = new Vector3(i, 0.0f, j);
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
        Aabb p_aabb = new Aabb(new Vector3(0, -2000.0f, 0), new Vector3(width, 4000.0f, depth)); // Adjust the height as needed

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
            CallDeferred(TerrainChunk.MethodName.DeployMesh, arrays, p_aabb, globalPosition);
            //DeployMesh(arrays, p_aabb, globalPosition);
        }
    }
    public bool DeployMesh(Godot.Collections.Array arrays, Aabb p_aabb, Vector3 globalPosition)
    {
        Stopwatch sw = Stopwatch.StartNew();
        instance = RenderingServer.InstanceCreate2(mesh, GetWorld3D().Scenario);
        GD.Print("After Instance Create: " + sw.ElapsedMilliseconds);
        // Set the transform
        Transform3D xform = new Transform3D(Basis.Identity, globalPosition);

        RenderingServer.InstanceSetTransform(instance, xform);
        Stopwatch sw2 = Stopwatch.StartNew();
        ShaderMaterial terrainMat = new ShaderMaterial();
        terrainMat.Shader = terrainShader;
        GD.Print("Midway Mesh: " + sw.ElapsedMilliseconds);
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
        GD.Print("Deploy Mesh: " + sw.ElapsedMilliseconds);

        return true;
    }

    public void CleanUp()
    {
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
        //this.QueueFree();
    }
}

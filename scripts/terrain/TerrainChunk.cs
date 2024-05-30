using Godot;
using Godot.NativeInterop;
using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using Godot.Collections;
using System.Diagnostics;

public partial class TerrainChunk : Node3D
{
    Rid staticBody = new Rid();
	public TerrainChunk()
	{
	}
	public bool BuildCollision(Image heightMap, int width, int depth, Vector3 globalPosition)
	{
        GD.Print("Building collision with physics server");
        staticBody = PhysicsServer3D.BodyCreate();
        PhysicsServer3D.BodySetMode(staticBody, PhysicsServer3D.BodyMode.Static);
        PhysicsServer3D.BodySetSpace(staticBody, GetWorld3D().Space);
        PhysicsServer3D.BodyAttachObjectInstanceId(staticBody, GetInstanceId());
        
        Rid shape = PhysicsServer3D.HeightmapShapeCreate();
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i, j).R * 400.0f;
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
        Transform3D xform = new Transform3D(new Basis(new Vector3(0f, 1f, 0f), Mathf.Pi * 0.5f), globalPosition + new Vector3(width * 0.5f, 0.0f, depth * 0.5f)); CollisionShape3D debugColShape = new CollisionShape3D();

        // scale the xform if we want to increase or decrease mesh_vertex_spacing, I think a value of 1 is good for now
        //xform.scale(Vector3(_mesh_vertex_spacing, 1.f, _mesh_vertex_spacing));

        Dictionary shape_data = new Dictionary();
        shape_data["width"] = width;
        shape_data["depth"] = depth;
        shape_data["heights"] = mapData;
        shape_data["min_height"] = minHeight;
        shape_data["max_height"] = maxHeight;
        PhysicsServer3D.ShapeSetData(shape, shape_data);
        PhysicsServer3D.BodyAddShape(staticBody, shape, xform);
        PhysicsServer3D.BodySetCollisionMask(staticBody, 1);
        PhysicsServer3D.BodySetCollisionLayer(staticBody, 1);
        PhysicsServer3D.BodySetCollisionPriority(staticBody, 1);
        return true;
    }

    public bool BuildDebugCollision(Image heightMap, int width, int depth, Vector3 globalPosition)
    {
        GD.Print("Building debug collision. Disable this mode for releases");
        StaticBody3D _debugStaticBody = new StaticBody3D();
        _debugStaticBody.Name = "StaticBody3D";
        AddChild(_debugStaticBody);

        Rid shape = PhysicsServer3D.HeightmapShapeCreate();
        float[] mapData = new float[width * depth];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        for (int i = 0; i < width; i++)
        {
            for (int j = depth - 1; j >= 0; j--)
            {
                int index = i * depth + (depth - 1 - j);
                mapData[index] = heightMap.GetPixel(i, j).R * 400.0f;
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

    public bool DestroyCollision()
    {
        if (staticBody.IsValid)
        {
            GD.Print("Freeing physics body");
            Rid shape = PhysicsServer3D.BodyGetShape(staticBody, 0);
            PhysicsServer3D.FreeRid(shape);
            PhysicsServer3D.FreeRid(staticBody);
            staticBody = new Rid();
            return true;
        }
        return false;
    }

    public bool BuildMesh(Image heightMap, int width, int depth, Vector3 globalPosition)
    {
        // Create an array for the vertices
        Vector3[] p_vertices = new Vector3[width * depth];

        // Populate the vertices array
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                int index = i * width + j;
                float height = heightMap.GetPixel(i, j).R * 400.0f; // Adjust the multiplier as needed
                p_vertices[index] = new Vector3(i, height, j);
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

        // Create and set the normals
        Vector3[] normals = new Vector3[p_vertices.Length];
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                int index = i * width + j;
                Vector3 right = new Vector3(1, 0, heightMap.GetPixel(i + 1, j).R - heightMap.GetPixel(i, j).R);
                Vector3 down = new Vector3(0, 1, heightMap.GetPixel(i, j + 1).R - heightMap.GetPixel(i, j).R);
                Vector3 normal = right.Cross(down).Normalized();
                normals[index] = normal;
            }
        }
        arrays[(int)RenderingServer.ArrayType.Normal] = normals;

        // Create and set the tangents
        float[] tangents = new float[p_vertices.Length * 4];
        for (int i = 0; i < width - 1; i++)
        {
            for (int j = 0; j < depth - 1; j++)
            {
                int index = i * width + j;
                Vector3 tangent = new Vector3(1, 0, heightMap.GetPixel(i + 1, j).R - heightMap.GetPixel(i, j).R).Normalized();
                tangents[index * 4 + 0] = tangent.X;
                tangents[index * 4 + 1] = tangent.Y;
                tangents[index * 4 + 2] = tangent.Z;
                tangents[index * 4 + 3] = 1.0f; // This can be -1 or 1 to flip the bitangent if needed
            }
        }
        arrays[(int)RenderingServer.ArrayType.Tangent] = tangents;

        // Create the mesh
        Rid mesh = RenderingServer.MeshCreate();
        RenderingServer.MeshAddSurfaceFromArrays(mesh, RenderingServer.PrimitiveType.Triangles, arrays);

        // Set the custom AABB
        RenderingServer.MeshSetCustomAabb(mesh, p_aabb);


        Rid instance = RenderingServer.InstanceCreate2(mesh, GetWorld3D().Scenario);

        // Set the transform
        Transform3D xform = new Transform3D(Basis.Identity, globalPosition);
        RenderingServer.InstanceSetTransform(instance, xform);

        return true;
    }
}

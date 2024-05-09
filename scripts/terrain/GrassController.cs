using Godot;
using System;

public partial class GrassController : Node
{

    uint NUM_GRASS = (32 * 32) * 3;
    uint GRASS_SEGMENTS_LOW = 1;
    uint GRASS_SEGMENTS_HIGH = 6;
    uint GRASS_VERTICES_LOW;
    uint GRASS_VERTICES_HIGH;
    uint GRASS_LOD_DIST = 15;
    uint GRASS_MAX_DIST = 100;
    uint GRASS_PATCH_SIZE = 5 * 2;
    float GRASS_WIDTH = 0.1f;
    float GRASS_HEIGHT = 1.5f;

    public override void _Ready()
    {
        AddGrass();
    }
    public override void _Process(double delta)
    {
        // Called every frame. Delta is time since the last frame.
        // Update game logic here.
    }

    public void AddGrass()
    {
        GRASS_VERTICES_LOW = (GRASS_SEGMENTS_LOW + 1) * 2;
        GRASS_VERTICES_HIGH = (GRASS_SEGMENTS_HIGH + 1) * 2;
        GD.Print("hello?");
        // Called every time the node is added to the scene.
        // Initialization here.
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(0, 0, 0), // Bottom-left corner
            new Vector3(1, 0, 0), // Bottom-right corner
            new Vector3(1, 1, 0), // Top-right corner
            new Vector3(0, 1, 0), // Top-left corner

            new Vector3(1, 2, 0), // Top-right corner
            new Vector3(0, 2, 0), // Top-left corner

            new Vector3(1, 3, 0), // Top-right corner
            new Vector3(0, 3, 0), // Top-left corner

            new Vector3(1, 4, 0), // Top-right corner
            new Vector3(0, 4, 0), // Top-left corner

            new Vector3(1, 5, 0), // Top-right corner
            new Vector3(0, 5, 0), // Top-left corner

            new Vector3(0.8f, 6, 0), // Top-right corner
            new Vector3(0.2f, 6, 0), // Top-left corner

            new Vector3(0.5f, 7, 0) // Top corner
        };
        for (int i = 0; i < NUM_GRASS; i++)
        {
            st.Begin(Mesh.PrimitiveType.Triangles);
            st.SetColor(new Color(0, 1, 0));
            st.SetUV(new Vector2(0, 0));

            foreach (Vector3 vec in highLODVertices)
            {
                st.AddVertex(vec);
            }
            //this is the loop?
            st.GenerateNormals();
            //MeshInstance3D grassBlade = new MeshInstance3D();
            //grassBlade.Mesh = st.Commit();
            MeshInstance3D grassBlade = new MeshInstance3D();
            grassBlade.Mesh = ((MeshInstance3D)GetNode("GrassInstance3D")).Mesh;
            var transform = new Transform3D();
            transform.Origin = new Vector3(i % 32, 0, i / 32);

            AddChild(grassBlade);
            grassBlade.GlobalTransform = transform;
            GD.Print(grassBlade.GlobalPosition);
            GD.Print(grassBlade.Mesh);
        }
    }


}

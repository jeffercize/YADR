using Godot;
using System;

public partial class renderservertest : Node3D
{
    BoxMesh boxMesh;
    Mesh highLODMesh;
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
	{
        
        boxMesh = new BoxMesh();
        Rid boxInstance = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetBase(boxInstance, boxMesh.GetRid());
        RenderingServer.InstanceSetScenario(boxInstance, GetWorld3D().Scenario);
        Transform3D trans = new Transform3D(Basis.Identity, new Vector3(2,0,2));
        RenderingServer.InstanceSetTransform(boxInstance, trans);


        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://scripts/terrain/grass/grassShader.gdshader");
        grassMat.Shader = grassShader;
        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        highLODMesh = CreateHighLODGrassBlade(0.1f, 0.5f, null);
        Rid bladeInstance = RenderingServer.InstanceCreate();
        RenderingServer.InstanceSetBase(bladeInstance, highLODMesh.GetRid());
        RenderingServer.InstanceSetScenario(bladeInstance, GetWorld3D().Scenario);
        Transform3D trans2 = new Transform3D(Basis.Identity, new Vector3(0, 0, 0));
        RenderingServer.InstanceSetTransform(bladeInstance, trans2);

        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);

        // Set the shader parameters
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", 0.1f);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", 0.5f);

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(highLODMesh.GetRid(), 0, grassMaterial);


        Rid grassChunk = RenderingServer.MultimeshCreate();

        RenderingServer.MultimeshSetMesh(grassChunk, highLODMesh.GetRid());
        int instanceCount = 2048;

        int rowLength = (int)Math.Sqrt(instanceCount);

        // Create a new array to hold the transform data for all instances
        float[] instanceData = new float[12 * instanceCount];
        Random rand = new Random();
        // Fill the array with the transform data for each instance
        for (int k = 0; k < instanceCount; k++)
        {
            float x_loc = (k % rowLength) / (float)rowLength * 30 - 30 / 2;
            float y_loc = (k / rowLength) / (float)rowLength * 30 - 30 / 2;
            float x_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;
            float y_jitter = (float)rand.NextDouble() * 0.9f - 0.45f;

            // Create a new transform for this instance
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3((x_loc + x_jitter), 0, (y_loc + y_jitter)));

            // Add the transform data to the array
            instanceData[k * 12 + 0] = transform.Basis.X.X;
            instanceData[k * 12 + 1] = transform.Basis.X.Y;
            instanceData[k * 12 + 2] = transform.Basis.X.Z;
            instanceData[k * 12 + 3] = transform.Origin.X;
            instanceData[k * 12 + 4] = transform.Basis.Y.X;
            instanceData[k * 12 + 5] = transform.Basis.Y.Y;
            instanceData[k * 12 + 6] = transform.Basis.Y.Z;
            instanceData[k * 12 + 7] = transform.Origin.Y;
            instanceData[k * 12 + 8] = transform.Basis.Z.X;
            instanceData[k * 12 + 9] = transform.Basis.Z.Y;
            instanceData[k * 12 + 10] = transform.Basis.Z.Z;
            instanceData[k * 12 + 11] = transform.Origin.Z;
        }

        RenderingServer.MultimeshAllocateData(grassChunk, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false);
        RenderingServer.MultimeshSetBuffer(grassChunk, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(grassChunk, instanceCount);

        // Create a new instance for the multimesh
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(0, 0, 0)));

    }
    private Mesh CreateHighLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth, myGrassHeight * 0.15f, 0), // Top-right corner
            new Vector3(0, myGrassHeight * 0.1f, 0), // Top-left corner

            new Vector3(myGrassWidth, myGrassHeight * 0.3f, 0), // Top-right corner
            new Vector3(0, myGrassHeight * 0.2f, 0), // Top-left corner

            new Vector3(myGrassWidth, myGrassHeight * 0.45f, 0), // Top-right corner
            new Vector3(0, myGrassHeight * 0.3f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.95f, myGrassHeight * 0.6f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.05f, myGrassHeight * 0.6f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.75f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.75f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.7f, myGrassHeight * 0.9f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.3f, myGrassHeight * 0.9f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.5f, myGrassHeight, 0) // Top corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
        int index = 0;
        foreach (Vector3 vec in highLODVertices)
        {
            st.AddVertex(vec);
            if (index >= 3 && index % 2 != 0)
            {
                // Add two triangles to form a square (front face)
                st.AddIndex(index - 3);
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);

                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
                st.AddIndex(index);

                // Add two triangles to form a square (back face)
                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
                st.AddIndex(index - 3);

                st.AddIndex(index);
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);
            }
            else if (index == highLODVertices.Length - 1)
            {
                // Add the final triangle at the top (front face)
                st.AddIndex(index - 2);
                st.AddIndex(index - 1);
                st.AddIndex(index);

                // Add the final triangle at the top (back face)
                st.AddIndex(index);
                st.AddIndex(index - 1);
                st.AddIndex(index - 2);
            }
            index++;
        }
        st.GenerateNormals();
        highLODMesh = st.Commit();
        // Create a RID for the mesh and add the arrayMesh's surface to it
        //Rid highLODGrassBlade = highLODMesh.GetRid();

        // Create a RID for the shader and set its code
        //Rid materialShader = RenderingServer.ShaderCreate();
        //RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        // Create a RID for the material and set its shader
       // Rid grassMaterial = RenderingServer.MaterialCreate();
       // RenderingServer.MaterialSetShader(grassMaterial, materialShader);

        // Set the shader parameters
        //RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", myGrassWidth);
        //RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", myGrassHeight);

        // Set the material for the mesh surface
       // RenderingServer.MeshSurfaceSetMaterial(highLODGrassBlade, 0, grassMaterial);

        return highLODMesh;
    }
}

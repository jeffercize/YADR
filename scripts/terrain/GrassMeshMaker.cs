using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

public partial class GrassMeshMaker : Node3D
{
    float totalTime = 0.0f;
    int rowCount = 15;
    int columnCount = 15;
    ValueTuple<Rid, Rid>[,] grassChunks;
    ValueTuple<uint, Rid, uint, Rid>[,] grassChunkMultimeshs;
    List<Rid> activeGrassChunks = new List<Rid>();
    List<Rid> newActiveGrassChunks = new List<Rid>();

    int instanceCount = 8192;
    int rowLength = (int)Math.Sqrt(8192);
    int randomSeed;
    bool grassReady = false;

    Mesh highLODMesh;
    Mesh mediumLODMesh;
    Mesh lowLODMesh;

    ImageTexture heightMapTexture;
    Image heightMap;

    CharacterBody3D player;



    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Random rand = new Random();
        randomSeed = rand.Next(int.MaxValue - 1000000); //subtract 1 mil because we use it in math and I dont want to overflow
    }

    public (float factor1, float factor2) CalculateFactorsOld(float distance)
    {
        float factor1, factor2;

        if (distance >= 0 && distance <= 1) //fade out 1 high lod
        {
            factor1 = 1;
            factor2 = 1 - distance;
        }
        else if (distance > 2 && distance <= 3) //fade in medium lod and fade out high lod
        {
            factor1 = 1 - (distance - 2);
            factor2 = distance - 2;
        }
        else if (distance > 3 && distance <= 4) //fade out medium lod and fade in low lod
        {
            factor1 = distance - 3;
            factor2 = 1 - (distance - 3);
        }
        else //maintain low lod
        {
            factor1 = 1;
            factor2 = 0;
        }

        return (factor1, factor2);
    }

    public (float factor1, float factor2) CalculateFactors(float distance)
    {
        float factor1 = 0.0f;
        float factor2 = 0.0f;
        if(distance < 2)
        {
            factor1 = 1.0f;
            factor2 = 0.0f;
        }
        else if (distance <= 12)
        {
            distance = (distance / 4) - 0.5f; //we divide by 2 to slow down the cosine wave so now its is 0-2 per flip
            //start 2 go to 6, instead divide by 4 so its start at .5 go to 1.5 so subtract .5 go from 0 to 1.0 then back again for low lod fade in thus 2-12 or 0->1->2(0), normalized :)
            factor1 = 0.5f * (1 + (float)Math.Cos(distance * Math.PI));
            factor2 = 0.5f * (1 + (float)Math.Cos(distance+1 * Math.PI));
        }
        else
        {
            factor1 = 1.0f;
            factor2 = 0.0f;
        }
        

        return (factor1, factor2);
    }


    int i = 0;
    int j = 0;
    int k = 0;
    int l = 0;
    int widthIndex;
    int heightIndex;
// Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        totalTime += (float)delta;
        //UpdateGrassChunks();
        if (grassReady)
        {
            if (Input.IsKeyPressed(Key.J))
            {
                //return;
            }
            else
            {
                processGrassClumps();
                cleanupGrassClumps();
            }

            if (Input.IsKeyPressed(Key.T))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 0.0);
            }
            else if (Input.IsKeyPressed(Key.F))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", MathF.PI/2);
            }
            else if(Input.IsKeyPressed(Key.B))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", MathF.PI);
            }
            else if(Input.IsKeyPressed(Key.H))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 3*MathF.PI/2);
            }
            else if (Input.IsKeyPressed(Key.Y))
            {
                RenderingServer.GlobalShaderParameterSet("windDirection", 7*MathF.PI/4);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
    }
    /// <summary>
    /// Clears the Rids and sets visible to 0 for multi-meshes that are out of range
    /// DOESNT ACTUALLY DELETE THE MULTIMESH not sure why TODO how do you actually clear something from RenderingServer
    /// ALSO TODO use visibility ranges to just fade in-out, then we can just say fuck it and generate a bunch of chunks and let them sit for a long time
    /// after we generate it a ring out from us we can generate beyond that into the fade out distance, the problem is still how to update mesh and distance stuff
    /// re-examine expert approach
    /// </summary>
    public void cleanupGrassClumps()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int chunksCleanedThisFrame = 0;
        int maxChunksCleanedPerFrame = 100;

        widthIndex = (int)(player.Transform.Origin.X / 30);
        heightIndex = (int)(player.Transform.Origin.Z / 30);
        // Cleanup
        for (; k < grassChunks.GetLength(0); k++)
        {
            for (; l < grassChunks.GetLength(1); l++)
            {
                if (k < widthIndex - 10 || k > widthIndex + 10 || l < heightIndex - 10 || l > heightIndex + 10)
                {
                    if (grassChunks[k, l].Item1.IsValid || grassChunks[k, l].Item2.IsValid)
                    {
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[k, l].Item2, 0);
                        RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[k, l].Item4, 0);
                        RenderingServer.FreeRid(grassChunks[k, l].Item1);
                        RenderingServer.FreeRid(grassChunks[k, l].Item2);
                        RenderingServer.FreeRid(grassChunkMultimeshs[k, l].Item2);
                        RenderingServer.FreeRid(grassChunkMultimeshs[k, l].Item4);
                        grassChunks[k, l].Item1 = new Rid();
                        grassChunks[k, l].Item2 = new Rid();
                        grassChunkMultimeshs[k, l].Item2 = new Rid();
                        grassChunkMultimeshs[k, l].Item4 = new Rid();
                    }
                }
                chunksCleanedThisFrame += 1;
                if (chunksCleanedThisFrame > maxChunksCleanedPerFrame)
                {
                    //GD.Print($"Cleanup Time elapsed: {k} {l} {stopwatch.Elapsed}");
                    return;
                }
            }
            l = 0;
        }
        k = 0;
        //GD.Print($"Cleanup Time elapsed: {stopwatch.Elapsed}");
    }
    /// <summary>
    /// Should be reworked to dispatch the compute shader for each desired cluster (in view + some?), cluster will then manage their own culling
    /// </summary>
    public void processGrassClumps()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int chunksUpdatedThisFrame = 0;
        int maxChunksPerFrame = 4; // Adjust this value for performance vs grass loading
        for (; i <= 7; i++)
        {
            for (; j <= i; j++)
            {
                //GD.Print($"{widthIndex}:{i} | {heightIndex}:{j}");
                // Top side, from left to right
                processGrassClump(widthIndex - i, heightIndex + j);

                // Right side, from top to bottom
                processGrassClump(widthIndex + j, heightIndex + i);

                // Bottom side, from right to left
                processGrassClump(widthIndex + i, heightIndex - j);

                // Left side, from bottom to top
                processGrassClump(widthIndex - j, heightIndex - i);
                chunksUpdatedThisFrame += 4;
                if(chunksUpdatedThisFrame > maxChunksPerFrame)
                {
                    return;
                }
            }
            //GD.Print("set J to -I");
            j = -i;
        }
        //GD.Print("set J to -I");
        i = 0;
    }
    /// <summary>
    /// This should be re-written to just be a compute shader with a RenderingServerWrapper
    /// Currently it takes in a chunkWidth index and chunkHeight index and initializes that grass clump
    /// </summary>
    /// <param name="currentWidthIndex">index in chunks</param>
    /// <param name="currentHeightIndex">index in chunks</param>
    public void processGrassClump(int currentWidthIndex, int currentHeightIndex)
    {
        // Ensure the indices are within the bounds of the grassChunks array
        if (currentWidthIndex >= 0 && currentWidthIndex < grassChunks.GetLength(0) &&
            currentHeightIndex >= 0 && currentHeightIndex < grassChunks.GetLength(1))
        {
            // Calculate the distance from the player to the current chunk
            int distance = Math.Max(Math.Abs(i), Math.Abs(j));
            if (!grassChunks[currentWidthIndex, currentHeightIndex].Item1.IsValid && !grassChunks[currentWidthIndex, currentHeightIndex].Item2.IsValid)
            {
                grassChunks[currentWidthIndex, currentHeightIndex].Item1 = InitializeRenderServerGrassClump(lowLODMesh, currentWidthIndex, currentHeightIndex, rowLength, instanceCount, 30, 30, randomSeed, 0);
                grassChunks[currentWidthIndex, currentHeightIndex].Item2 = InitializeRenderServerGrassClump(mediumLODMesh, currentWidthIndex, currentHeightIndex, rowLength, instanceCount, 30, 30, randomSeed, 1);
                RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, instanceCount / 4);
                RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, instanceCount / 16);
                grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 2;
                grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item3 = 1;
            }
            // Calculate the distance from the player to the current chunk

            Vector3 chunkPosition = new Vector3(currentWidthIndex * 30 + 15, 0, currentHeightIndex * 30 + 15); //TODO REPLACE 0 with a local Y heightmap check and player Y
            Vector3 playerPosition = new Vector3(player.Transform.Origin.X, 0, player.Transform.Origin.Z);

            float real_distance = (chunkPosition - playerPosition).Length() / 30.0f;
            (float transitionFactor1, float transitionFactor2) = CalculateFactors(real_distance);                                                                         

            // Ensure the transition factors are within the range [0, 1]
            transitionFactor1 = Math.Max(0, Math.Min(1, transitionFactor1));
            transitionFactor2 = Math.Max(0, Math.Min(1, transitionFactor2));

            // Calculate the number of instances for each LOD
            int item2LODInstanceCount = (int)(instanceCount * transitionFactor1);
            int item4LODInstanceCount = (int)(instanceCount * transitionFactor2 / 4);

            //Item4 management between high and low
            if (real_distance <= 6 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 != 0)
            {
                RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, highLODMesh.GetRid());
                grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 0;
            }
            else if (real_distance > 6 && grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 != 2)
            {
                RenderingServer.MultimeshSetMesh(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, lowLODMesh.GetRid());
                grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 = 2;
            }

            if (real_distance > 6 || grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item1 == 2)
            {
                item2LODInstanceCount = item2LODInstanceCount / 16;
            }
            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item2, item2LODInstanceCount);
            RenderingServer.MultimeshSetVisibleInstances(grassChunkMultimeshs[currentWidthIndex, currentHeightIndex].Item4, item4LODInstanceCount);
        }
    }

    public void SetupGrass(String target, Image givenHeightMap)
    {
        heightMap = givenHeightMap;
        player = GetNode<CharacterBody3D>("../../"+target);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ShaderMaterial grassMat = new ShaderMaterial();
        Shader grassShader = GD.Load<Shader>("res://shaders/terrain/grassShader.gdshader");
        grassMat.Shader = grassShader;

        heightMapTexture = ImageTexture.CreateFromImage(heightMap);
        GD.Print(heightMapTexture.GetWidth());
        GD.Print(heightMapTexture.GetHeight());
        int chunkIndexWidth = heightMapTexture.GetWidth() / 30;
        int chunkIndexHeight = heightMapTexture.GetHeight() / 30;
        grassChunks = new ValueTuple<Rid, Rid>[chunkIndexWidth, chunkIndexHeight];
        grassChunkMultimeshs = new ValueTuple<uint, Rid, uint, Rid>[chunkIndexWidth, chunkIndexHeight];

        float grassWidth = 0.3f;
        float grassHeight = 1.5f;
        highLODMesh = CreateHighLODGrassBlade(grassWidth, grassHeight, grassMat);
        mediumLODMesh = CreateMediumLODGrassBlade(grassWidth*2, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        lowLODMesh = CreateLowLODGrassBlade(grassWidth*4, grassHeight, grassMat); //we progressively widen the grass for lower lods to help it fill the screen with less blades/triangles
        
        //create and assign a shader per mesh
        Rid materialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(materialShader, grassMat.Shader.Code);

        Rid mediumMaterialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(mediumMaterialShader, grassMat.Shader.Code);

        Rid lowMaterialShader = RenderingServer.ShaderCreate();
        RenderingServer.ShaderSetCode(lowMaterialShader, grassMat.Shader.Code);


        // Create a RID for the material and set its shader HIGH
        Rid grassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(grassMaterial, materialShader);
        // Set the shader parameters HIGH
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalWidth", grassWidth);
        RenderingServer.MaterialSetParam(grassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(grassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(grassMaterial, "heightMap", heightMapTexture.GetRid());

        // Create a RID for the material and set its shader MEDIUM
        Rid mediumGrassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(mediumGrassMaterial, mediumMaterialShader);
        // Set the shader parameters MEDIUM
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "grassTotalWidth", grassWidth*2);
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(mediumGrassMaterial, "heightMap", heightMapTexture.GetRid());

        // Create a RID for the material and set its shader LOW
        Rid lowGrassMaterial = RenderingServer.MaterialCreate();
        RenderingServer.MaterialSetShader(lowGrassMaterial, lowMaterialShader);
        // Set the shader parameters LOW
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalWidth", grassWidth*4);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "grassTotalHeight", grassHeight);
        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightParams", new Vector2(heightMapTexture.GetWidth(), heightMapTexture.GetHeight()));
        RenderingServer.MaterialSetParam(lowGrassMaterial, "heightMap", heightMapTexture.GetRid());

        //Set global wind direction
        RenderingServer.GlobalShaderParameterSet("windDirection", 7*MathF.PI/4);
        RenderingServer.GlobalShaderParameterSet("windStrength", 0.5);

        // Set the material for the mesh surface
        RenderingServer.MeshSurfaceSetMaterial(highLODMesh.GetRid(), 0, grassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(mediumLODMesh.GetRid(), 0, mediumGrassMaterial);
        RenderingServer.MeshSurfaceSetMaterial(lowLODMesh.GetRid(), 0, lowGrassMaterial);

        Thread shaderParamThread = new Thread(() =>
        {
            while (true) //change to isRunning and shutdown on tree exit
            {
                SetShaderStuff();
                Thread.Sleep(16); // Wait for 16 milliseconds
            }
        });
        shaderParamThread.Start();

        widthIndex = (int)(player.Transform.Origin.X / 30);
        heightIndex = (int)(player.Transform.Origin.Z / 30);

        grassReady = true;
        GD.Print($"Setup Time elapsed: {stopwatch.Elapsed}");
    }

    public void SetShaderStuff()
    {
        RenderingServer.GlobalShaderParameterSet("time", totalTime);
    }

    public Rid InitializeRenderServerGrassClump(Mesh grassBlade, int widthIndex, int heightIndex, int rowLength = 64, int instanceCount = 4096, float fieldWidth = 30f, float fieldHeight = 30f, int randomSeed = 0, int innerClumpIndex = 0)
    {
        Stopwatch stopwatch3 = Stopwatch.StartNew();
        //lookup cantor pairing functions, the result will apparently always be unique for all combinations of width and height index
        Random rand = new Random(randomSeed + CantorPair(CantorPair(widthIndex, heightIndex), innerClumpIndex));
        //GD.Print($"begin make multimesh: {stopwatch3.Elapsed}");
        Rid grassChunk = RenderingServer.MultimeshCreate();
        // Create a RID for the shader and set its code
        RenderingServer.MultimeshSetMesh(grassChunk, grassBlade.GetRid());
        // Create a new array to hold the transform data for all instances
        //GD.Print($"declare array size: {stopwatch3.Elapsed}");
        float[] instanceData = new float[16 * instanceCount];
        //GD.Print($"pre-array: {stopwatch3.Elapsed}");
        // Fill the array with the transform data for each instance
        int instanceDataIndex = 0;

        List<int> indices = Enumerable.Range(0, instanceCount).ToList();

        // Shuffle the list
        indices = indices.OrderBy(x => rand.Next()).ToList();

        int desiredClumpCount = instanceCount / 6; //10 blades of grass per clump on average
        int arraySize = (int)MathF.Ceiling(MathF.Sqrt(desiredClumpCount)); // Calculate the size of the array
        Tuple<float, float, float, int, float>[,] clumpPoints = new Tuple<float, float, float, int, float>[arraySize + 2, arraySize + 2]; //x,y,height,type
        float clumpingValue = 0.4f;

        float spacing = fieldWidth / (MathF.Sqrt(desiredClumpCount) - 1);

        // Populate the array of clumps
        for (int i = -1; i <= arraySize; i++)
        {
            for (int j = -1; j <= arraySize; j++)
            {
                float x = i * spacing - fieldWidth / 2;
                float y = j * spacing - fieldHeight / 2;
                float jitterFactor = 3.0f;
                float x_jitter = rand.NextSingle() * spacing * jitterFactor - spacing / 2;
                float y_jitter = rand.NextSingle() * spacing * jitterFactor - spacing / 2;
                float rangeMin = 0.5f;
                float rangeMax = 0.6f;
                float randomNum = rangeMin + (rand.NextSingle() * (rangeMax - rangeMin));
                float facing = (randomNum * 2 * MathF.PI); // Generate a random angle in radians (NOT USED)
                clumpPoints[i + 1, j + 1] = new Tuple<float, float, float, int, float>(x + x_jitter, y + y_jitter, rand.NextSingle()+0.4f, 1, facing); //random height from 0 to 1 for now, all grass is type 1
            }
        }
        float chunkHeight = heightMap.GetPixel((int)(widthIndex * fieldWidth + (fieldWidth / 2)), (int)(heightIndex * fieldHeight + (fieldHeight / 2))).R * 400.0f;
        //we randomly insert 0 -> instanceCount and they are also randomly placed with x_loc and y_loc
        foreach (int i in indices)
        {
            float x_jitter = rand.NextSingle() * 0.9f - 0.45f;
            float y_jitter = rand.NextSingle() * 0.9f - 0.45f;
            float x_loc = (rand.NextSingle() * fieldWidth - fieldWidth / 2) + x_jitter;
            float y_loc = (rand.NextSingle() * fieldHeight - fieldHeight / 2) + y_jitter;

            //use transform to find and pick this instances clump point
            Tuple<float, float, float, int, float> closestClump = new Tuple<float, float, float, int, float>(0.0f,0.0f,0.0f,1, 0.0f);
            float closestClumpDistance = float.MaxValue;
            for (int x_index = Math.Max(0, (int)((x_loc + fieldWidth / 2) / fieldWidth * arraySize)); x_index <= Math.Min(arraySize, (int)((x_loc + fieldWidth / 2) / fieldWidth * arraySize) + 2); x_index++)
            {
                for (int y_index = Math.Max(0, (int)((y_loc + fieldHeight / 2) / fieldHeight * arraySize)); y_index <= Math.Min(arraySize, (int)((y_loc + fieldHeight / 2) / fieldHeight * arraySize) + 2); y_index++)
                {
                    Vector2 clumpPoint = new Vector2(clumpPoints[x_index, y_index].Item1, clumpPoints[x_index, y_index].Item2);
                    float currentDistance = clumpPoint.DistanceTo(new Vector2(x_loc, y_loc));
                    if (currentDistance < closestClumpDistance)
                    {
                        closestClump = clumpPoints[x_index, y_index];
                        closestClumpDistance = currentDistance;
                    }
                }
            }

            // Calculate the direction from the grass blade to the clump point
            Vector2 directionToClump = new Vector2(closestClump.Item1, closestClump.Item2) - new Vector2(x_loc, y_loc);

            // Move the grass blade towards the clump point
            x_loc += directionToClump.X * clumpingValue;
            y_loc += directionToClump.Y * clumpingValue;


            // Create a new transform for this instance
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3((x_loc), -chunkHeight, (y_loc)));

            //Rotational Basis
            // Calculate the angle in radians
            float angleInRadians = Mathf.Atan2(directionToClump.Y, directionToClump.X);
            // Convert the angle to degrees
            float faceDirection = Mathf.RadToDeg(angleInRadians);
            Basis rotationalBasis = new Basis(new Quaternion(new Vector3(0, 1, 0), faceDirection));
           
            if (closestClump.Item3 < 1.0f)
            {
                transform.Basis = rotationalBasis * transform.Basis;
            }

            // Add the transform data to the array
            instanceData[instanceDataIndex * 16 + 0] = transform.Basis.X.X;
            instanceData[instanceDataIndex * 16 + 1] = transform.Basis.X.Y;
            instanceData[instanceDataIndex * 16 + 2] = transform.Basis.X.Z;
            instanceData[instanceDataIndex * 16 + 3] = transform.Origin.X;
            instanceData[instanceDataIndex * 16 + 4] = transform.Basis.Y.X;
            instanceData[instanceDataIndex * 16 + 5] = transform.Basis.Y.Y;
            instanceData[instanceDataIndex * 16 + 6] = transform.Basis.Y.Z;
            instanceData[instanceDataIndex * 16 + 7] = transform.Origin.Y;
            instanceData[instanceDataIndex * 16 + 8] = transform.Basis.Z.X;
            instanceData[instanceDataIndex * 16 + 9] = transform.Basis.Z.Y;
            instanceData[instanceDataIndex * 16 + 10] = transform.Basis.Z.Z;
            instanceData[instanceDataIndex * 16 + 11] = transform.Origin.Z;

            // Add custom data at the end
            Color customData = new Color(closestClump.Item1, closestClump.Item2, closestClump.Item3, closestClump.Item4);

            instanceData[instanceDataIndex * 16 + 12] = customData.R; //
            instanceData[instanceDataIndex * 16 + 13] = customData.G; //
            instanceData[instanceDataIndex * 16 + 14] = customData.B; //height
            instanceData[instanceDataIndex * 16 + 15] = customData.A; //grassType

            instanceDataIndex++;
        }
        //GD.Print($"post-array: {stopwatch3.Elapsed}");
        // Set the buffer data for the MultiMesh
        RenderingServer.MultimeshAllocateData(grassChunk, instanceCount, RenderingServer.MultimeshTransformFormat.Transform3D, false, true);
        RenderingServer.MultimeshSetBuffer(grassChunk, instanceData);
        RenderingServer.MultimeshSetVisibleInstances(grassChunk, instanceCount);

        // Create a new instance for the multimesh
        Rid instance = RenderingServer.InstanceCreate2(grassChunk, this.GetWorld3D().Scenario);
        Aabb multiMeshAABB = RenderingServer.MultimeshGetAabb(grassChunk);
        multiMeshAABB = multiMeshAABB.Expand(new Vector3(0, 400, 0));
        RenderingServer.InstanceSetCustomAabb(instance, multiMeshAABB);
        RenderingServer.InstanceGeometrySetCastShadowsSetting(instance, RenderingServer.ShadowCastingSetting.Off);
        RenderingServer.InstanceSetTransform(instance, new Transform3D(Basis.Identity, new Vector3(widthIndex * fieldWidth + (fieldWidth/2), chunkHeight, heightIndex * fieldHeight + (fieldHeight / 2))));
        //RenderingServer.InstanceGeometrySetVisibilityRange(instance, 0.0f, 300.0f, 0.0f, 50.0f, RenderingServer.VisibilityRangeFadeMode.Self);

        if (innerClumpIndex == 0)
        {
            grassChunkMultimeshs[widthIndex, heightIndex].Item2 = grassChunk;
        }
        else
        {
            grassChunkMultimeshs[widthIndex, heightIndex].Item4 = grassChunk;
        }
        return instance;
    }

    int CantorPair(int a, int b)
    {
        return (a + b) * (a + b + 1) / 2 + b;
    }

    private Mesh CreateHighLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.975f, myGrassHeight * 0.1f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.025f, myGrassHeight * 0.1f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.95f, myGrassHeight * 0.2f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.05f, myGrassHeight * 0.2f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.3f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.3f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.85f, myGrassHeight * 0.4f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.4f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.8f, myGrassHeight * 0.5f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.2f, myGrassHeight * 0.5f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.7f, myGrassHeight * 0.6f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.3f, myGrassHeight * 0.6f, 0), // Top-left corner

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


        return highLODMesh;
    }

    //mediumLODGrass is broken right now
    private Mesh CreateMediumLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.25f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.25f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.9f, myGrassHeight * 0.5f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.1f, myGrassHeight * 0.5f, 0), // Top-left corner

            new Vector3(myGrassWidth * 0.85f, myGrassHeight * 0.75f, 0), // Top-right corner
            new Vector3(myGrassWidth * 0.15f, myGrassHeight * 0.75f, 0), // Top-left corner

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
        mediumLODMesh = st.Commit();

        return mediumLODMesh;
    }

    private Mesh CreateLowLODGrassBlade(float myGrassWidth, float myGrassHeight, ShaderMaterial grassMat)
    {
        SurfaceTool st = new SurfaceTool();
        Vector3[] highLODVertices = new Vector3[]
        {
            // First rectangle
            new Vector3(myGrassWidth, 0, 0), // Bottom-right corner
            new Vector3(0, 0, 0), // Bottom-left corner

            new Vector3(myGrassWidth, myGrassHeight, 0), // Top-right corner
            new Vector3(.0f, myGrassHeight, 0), // Top-left corner
        };
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (Vector3 vec in highLODVertices)
        {
            st.AddVertex(vec);
        }
        // Add two triangles to form a square (front face)
        st.AddIndex(0);
        st.AddIndex(1);
        st.AddIndex(2);

        st.AddIndex(2);
        st.AddIndex(1);
        st.AddIndex(3);

        // Add two triangles to form a square (back face)
        st.AddIndex(2);
        st.AddIndex(1);
        st.AddIndex(0);

        st.AddIndex(3);
        st.AddIndex(1);
        st.AddIndex(2);
        st.GenerateNormals();
        lowLODMesh = st.Commit();

        return lowLODMesh;
    }
    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
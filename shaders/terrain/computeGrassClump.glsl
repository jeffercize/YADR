#[compute]
#version 450

layout(local_size_x = 1, local_size_y = 1) in;

layout(set = 0, binding = 0) restrict buffer FieldDimensions {
    float fieldWidth;
    float fieldHeight;
    float chunkHeight;
};

layout(set = 0, binding = 1) restrict buffer IntData {
    int randSeed;
    int arraySize;
};

struct ClumpPoint {
    float clumpX;
    float clumpY;
    float clumpHeight;
    int clumpType;
};
layout(set = 0, binding = 2, std430) buffer ClumpPointsBuffer {
    ClumpPoint clumpPoints[];
};

layout(set = 0, binding = 3, std430) restrict buffer InstanceDataBuffer {
    float instanceData[];
};

float rand(float n){return fract(sin(n) * 43758.5453123);}

void main() {


    float x_jitter = rand(randSeed + 1.0) * 0.9 - 0.45;
    float y_jitter = rand(randSeed + 2.0) * 0.9 - 0.45;
    float x_loc = (rand(randSeed + 3.0) * fieldWidth - fieldWidth / 2.0) + x_jitter;
    float y_loc = (rand(randSeed + 4.0) * fieldHeight - fieldHeight / 2.0) + y_jitter;

    float closestClumpX = 0.0;
    float closestClumpY = 0.0;
    float closestClumpHeight = 0.0;
    int closestClumpType = 1;
    float closestClumpDistance = 999999.9;
    //these nest for loops are using the x_loc and y_loc to check the 9 surrounding clump points
    for (int x_index = max(0, int((x_loc + fieldWidth / 2.0) / fieldWidth * arraySize)); x_index <= min(arraySize, int((x_loc + fieldWidth / 2.0) / fieldWidth * arraySize) + 2); x_index++)
    {
        for (int y_index = max(0, int((y_loc + fieldHeight / 2.0) / fieldHeight * arraySize)); y_index <= min(arraySize, int((y_loc + fieldHeight / 2.0) / fieldHeight * arraySize) + 2); y_index++)
        {
            int index = y_index * arraySize + x_index;
            vec2 clumpPoint = vec2(clumpPoints[index].clumpX, clumpPoints[index].clumpY);
            float currentDistance = distance(clumpPoint, vec2(x_loc, y_loc));
            if (currentDistance < closestClumpDistance)
            {
                closestClumpX = clumpPoints[index].clumpX;
                closestClumpY = clumpPoints[index].clumpY;
                closestClumpHeight = clumpPoints[index].clumpHeight;
                closestClumpType = clumpPoints[index].clumpType;

                closestClumpDistance = currentDistance;
            }
        }
    }

    // Calculate the direction from the grass blade to the clump point
    vec2 directionToClump = (vec2(closestClumpX, closestClumpY) - vec2(x_loc, y_loc));

    // Move the grass blade towards the clump point (CLUMPING VALUE OF 0.4)
    x_loc += directionToClump.x * 0.4;
    y_loc += directionToClump.y * 0.4;

    //this code uses mat4 to create a transform matrix for the grass blade
    //i believe this will work correctly based on my rough understanding
    //but this was an idea provided by copilot so i am not sure TODO
    //currently I have omitted the last column of the matrix as it is 
    //omitted in the godots transform3D because its is always 0001

    // Create a new transform for this instance
    mat4 transform = mat4(1.0); // Identity matrix
    transform[3] = vec4(x_loc, -chunkHeight, y_loc, 1.0); // Translation

    //Rotational Basis
    // Calculate the angle in radians
    float angleInRadians = atan(directionToClump.y, directionToClump.x);
    // Convert the angle to degrees
    float faceDirection = degrees(angleInRadians);
    vec3 axis = vec3(0, 1, 0);
    float angle = radians(faceDirection);
    mat4 rotationalBasis = mat4(1.0); // Identity matrix
    rotationalBasis[0][0] = cos(angle);
    rotationalBasis[0][2] = sin(angle);
    rotationalBasis[2][0] = -sin(angle);
    rotationalBasis[2][2] = cos(angle);

    if (closestClumpHeight < 1.0)
    {
        transform = rotationalBasis * transform;
    }

    // Add the transform data to the array
    uint instanceDataIndex = gl_GlobalInvocationID.x;
    instanceData[instanceDataIndex * 16 + 0] = transform[0][0]; // Basis.X.X
    instanceData[instanceDataIndex * 16 + 1] = transform[0][1]; // Basis.X.Y
    instanceData[instanceDataIndex * 16 + 2] = transform[0][2]; // Basis.X.Z
    instanceData[instanceDataIndex * 16 + 3] = transform[3][0]; // Origin.X
    instanceData[instanceDataIndex * 16 + 4] = transform[1][0]; // Basis.Y.X
    instanceData[instanceDataIndex * 16 + 5] = transform[1][1]; // Basis.Y.Y
    instanceData[instanceDataIndex * 16 + 6] = transform[1][2]; // Basis.Y.Z
    instanceData[instanceDataIndex * 16 + 7] = transform[3][1]; // Origin.Y
    instanceData[instanceDataIndex * 16 + 8] = transform[2][0]; // Basis.Z.X
    instanceData[instanceDataIndex * 16 + 9] = transform[2][1]; // Basis.Z.Y
    instanceData[instanceDataIndex * 16 + 10] = transform[2][2]; // Basis.Z.Z
    instanceData[instanceDataIndex * 16 + 11] = transform[3][2]; // Origin.Z

    // Add custom data at the end
    instanceData[instanceDataIndex * 16 + 12] = closestClumpX; //
    instanceData[instanceDataIndex * 16 + 13] = closestClumpY; //
    instanceData[instanceDataIndex * 16 + 14] = closestClumpHeight; //height
    instanceData[instanceDataIndex * 16 + 15] = closestClumpType; //grassType
}
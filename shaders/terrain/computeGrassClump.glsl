#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 1) in;

layout(set = 0, binding = 0) restrict buffer FieldDimensions {
    float fieldWidth;
    float fieldHeight;
    float chunkHeight;
    float globalPosX;
    float globalPosZ;
    float heightParamsX;
    float heightParamsY;
};

layout(set = 0, binding = 1) restrict buffer IntData {
    int randSeed;
    int arraySize;
    int instanceCount;
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

layout(set = 0, binding = 4) uniform sampler2D heightMap;

layout(set = 0, binding = 5) uniform sampler2D pathMap;




float rand(float n){return fract(sin(n) * 43758.5453123);}

uint murmurHash12(uvec2 src) {
  const uint M = 0x5bd1e995u;
  uint h = 1190494759u;
  src *= M; src ^= src>>24u; src *= M;
  h *= M; h ^= src.x; h *= M; h ^= src.y;
  h ^= h>>13u; h *= M; h ^= h>>15u;
  return h;
}

float hash12(vec2 src) {
  uint h = murmurHash12(floatBitsToUint(src));
  return uintBitsToFloat(h & 0x007fffffu | 0x3f800000u) - 1.0;
}

void main() {
    int randSeedLocal = randSeed;
    if (gl_GlobalInvocationID.x >= instanceCount) {
        return;
    }

    //add all these random numbers to give us a real random number that is deterministic
    float x_jitter = hash12(vec2(randSeedLocal, gl_GlobalInvocationID.x + globalPosX + globalPosZ));
    float y_jitter = hash12(vec2(randSeedLocal, 1 + gl_GlobalInvocationID.x + globalPosX + globalPosZ));
    float x_loc = (hash12(vec2(randSeedLocal, 2 + gl_GlobalInvocationID.x + globalPosX + globalPosZ)) * fieldWidth - fieldWidth / 2.0) + x_jitter;
    float y_loc = (hash12(vec2(randSeedLocal, 3 + gl_GlobalInvocationID.x + globalPosX + globalPosZ)) * fieldHeight - fieldHeight / 2.0) + y_jitter;

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

    // Move the grass blade towards the clump point (CLUMPING VALUE OF 0.2)
    x_loc += directionToClump.x * 0.1;
    y_loc += directionToClump.y * 0.1;

    // Create a new transform for this instance
    mat4 transform = mat4(1.0); // Identity matrix
    transform[3] = vec4(x_loc, -chunkHeight, y_loc, 1.0); // Translation TODO height here

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

    ivec2 coord = ivec2(transform[3][0]+globalPosX+17.0, transform[3][2]+globalPosZ+17.0);
    vec2 heightmap_uv = vec2(coord) / vec2(544.0, 544.0);
    heightmap_uv = clamp(heightmap_uv, 0.0, 1.0);
    vec2 f = fract(heightmap_uv * vec2(544.0, 544.0));
	// Sample the closest texels
	float h00 = texture(heightMap, heightmap_uv).r;
	float h10 = texture(heightMap, heightmap_uv + vec2(1.0, 0.0) / vec2(544.0, 544.0)).r;
	float h01 = texture(heightMap, heightmap_uv + vec2(0.0, 1.0) / vec2(544.0, 544.0)).r;
    float h11 = texture(heightMap, heightmap_uv + vec2(1.0, 1.0) / vec2(544.0, 544.0)).r;
    float hm1 = texture(heightMap, heightmap_uv - vec2(1.0, 0.0) / vec2(544.0, 544.0)).r;
	float h0m1 = texture(heightMap, heightmap_uv - vec2(0.0, 1.0) / vec2(544.0, 544.0)).r;


    

    //calculate slope
    vec2 gradient = vec2(h10 - hm1, h01 - h0m1);
	float slope = length(gradient)*200.0;

    //calculate control value
    float control = texture(pathMap, heightmap_uv).r;

    //set instance height
    float height = mix(mix(h00, h10, f.x), mix(h01, h11, f.x), f.y)*400.0-0.1;
	transform[3] = vec4(transform[3][0], transform[3][1]+height, transform[3][2], transform[3][3]);

    //reuse x_jitter as our slope jitter and y_jitter as our control jitter because why not
    if(control - (y_jitter * 0.10) > 0.9 || slope - (x_jitter * 0.1) > 0.6)
    {
        float controlVal = -5000.1337;
        // Add the transform data to the array
        instanceData[gl_GlobalInvocationID.x * 16 + 0] = controlVal; // Basis.X.X
        instanceData[gl_GlobalInvocationID.x * 16 + 1] = controlVal; // Basis.X.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 2] = controlVal; // Basis.X.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 3] = controlVal; // Origin.X
        instanceData[gl_GlobalInvocationID.x * 16 + 4] = controlVal; // Basis.Y.X
        instanceData[gl_GlobalInvocationID.x * 16 + 5] = controlVal; // Basis.Y.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 6] = controlVal; // Basis.Y.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 7] = controlVal; // Origin.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 8] = controlVal; // Basis.Z.X
        instanceData[gl_GlobalInvocationID.x * 16 + 9] = controlVal; // Basis.Z.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 10] = controlVal; // Basis.Z.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 11] = controlVal; // Origin.Z

        // Add custom data at the end
        instanceData[gl_GlobalInvocationID.x * 16 + 12] = controlVal; //
        instanceData[gl_GlobalInvocationID.x * 16 + 13] = controlVal; //
        instanceData[gl_GlobalInvocationID.x * 16 + 14] = controlVal; //height
        instanceData[gl_GlobalInvocationID.x * 16 + 15] = controlVal; //grassType
    }
    else
    {
        // Add the transform data to the array
        instanceData[gl_GlobalInvocationID.x * 16 + 0] = transform[0][0]; // Basis.X.X
        instanceData[gl_GlobalInvocationID.x * 16 + 1] = transform[0][1]; // Basis.X.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 2] = transform[0][2]; // Basis.X.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 3] = transform[3][0]; // Origin.X
        instanceData[gl_GlobalInvocationID.x * 16 + 4] = transform[1][0]; // Basis.Y.X
        instanceData[gl_GlobalInvocationID.x * 16 + 5] = transform[1][1]; // Basis.Y.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 6] = transform[1][2]; // Basis.Y.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 7] = transform[3][1]; // Origin.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 8] = transform[2][0]; // Basis.Z.X
        instanceData[gl_GlobalInvocationID.x * 16 + 9] = transform[2][1]; // Basis.Z.Y
        instanceData[gl_GlobalInvocationID.x * 16 + 10] = transform[2][2]; // Basis.Z.Z
        instanceData[gl_GlobalInvocationID.x * 16 + 11] = transform[3][2]; // Origin.Z

        // Add custom data at the end
        instanceData[gl_GlobalInvocationID.x * 16 + 12] = closestClumpX; //
        instanceData[gl_GlobalInvocationID.x * 16 + 13] = closestClumpY; //
        instanceData[gl_GlobalInvocationID.x * 16 + 14] = closestClumpHeight; //height
        instanceData[gl_GlobalInvocationID.x * 16 + 15] = closestClumpType; //grassType
    }
}
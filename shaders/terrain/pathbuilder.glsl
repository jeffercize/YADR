#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32) in;

layout(set = 0, binding = 0) uniform sampler2D noiseTexture;

layout(set = 0, binding = 1, rg32f) uniform image2D outputImage;

layout(set = 0, binding = 2) restrict buffer ImageDimensions {
    int imageWidth;
    int imageHeight;
    int offsetX;
    int offsetY;
};

layout(std430, binding = 3) buffer CellIndexBuffer {
    ivec2 cellIndices[];
};

layout(std430, binding = 4) buffer PointsArrayBuffer {
    vec3 points[];
};

// Assuming offsetX, offsetY, imageWidth, and imageHeight are correctly set
ivec2 gridDims = ivec2(16, 16);

ivec2 GetGridCellID(vec2 position) {
    return ivec2(floor(position / 512.0));
}

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 global_coord = ivec2(gl_GlobalInvocationID.x + offsetX, gl_GlobalInvocationID.y + offsetY);
    vec2 uv = vec2(coord) / vec2(imageWidth, imageHeight);
    uv = clamp(uv, 0.0, 1.0);
    float height = texture(noiseTexture, uv).r;

    ivec2 cellID = GetGridCellID(vec2(global_coord));

    vec3 closestPoint = vec3(0.0, 0.0, 0.0);
    float distanceToClosest = 10000.0;
    for (int dy = -100; dy <= 100; dy++) {
        for (int dx = -100; dx <= 100; dx++) {
            ivec2 nearbyCellID = cellID + ivec2(dx, dy);
            // Ensure nearbyCellID is within grid bounds
            if (nearbyCellID.x >= 0 && nearbyCellID.x < gridDims.x && nearbyCellID.y >= 0 && nearbyCellID.y < gridDims.y) {
                int cellIndex = nearbyCellID.x + nearbyCellID.y * gridDims.x;
                if (cellIndex >= 0 && cellIndex < cellIndices.length()) {
                    int startIndex = cellIndices[cellIndex].x;
                    int count = cellIndices[cellIndex].y;
                    for (int i = 0; i < count; i++) {
                        vec3 point = points[startIndex + i];
                        float distance = distance(point.xy, vec2(global_coord));
                        if (distance < distanceToClosest) {
                            distanceToClosest = distance;
                            closestPoint = point;
                        }
                    }
                }
            }
        }
    }
    if (height < 0.0) {
        height = height / ((1.0 - height) * (1.0 - height));
    }
    else 
    {
        height = height * height * height;
    }

    if(distanceToClosest < 20.0)
    {
        height = closestPoint.z;
        imageStore(outputImage, coord, vec4(height, 1.0, 0.0, 1.0));
    }
    else if (distanceToClosest < 60.0)
    {
        float blendFactor = (distanceToClosest - 20.0) / 40.0;
        height = mix(closestPoint.z, height, blendFactor);
        imageStore(outputImage, coord, vec4(height, 0.0, 0.0, 1.0));
    }
    else
    {
        imageStore(outputImage, coord, vec4(height, 0.0, 0.0, 1.0));
    }

    //imageStore(outputImage, coord, vec4(cellID.x/16.0, cellID.y/16.0, 0.0, 1.0));
    //imageStore(outputImage, coord, vec4(gl_GlobalInvocationID.x/8000.0, gl_GlobalInvocationID.y/8000.0, 0.0, 1.0));
}
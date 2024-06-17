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
    int gridDimX;
    int gridDimY;
};

layout(std430, binding = 3) buffer CellIndexBuffer {
    ivec2 cellIndices[];
};

layout(std140, binding = 4) buffer PointsArrayBuffer {
    vec4 points[];
};


ivec2 GetGridCellID(vec2 position) {
    return ivec2(floor(position.x / 256.0), floor(position.y / 256.0));
}

void main() {
    ivec2 gridDims = ivec2(gridDimX, gridDimY);
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    ivec2 global_coord = ivec2(gl_GlobalInvocationID.x + offsetX, gl_GlobalInvocationID.y + offsetY);
    vec2 uv = vec2(coord) / vec2(imageWidth, imageHeight);
    uv = clamp(uv, 0.0, 1.0);
    float height = texture(noiseTexture, uv).r;

    ivec2 cellID = GetGridCellID(vec2(coord));

    vec4 closestPoint = vec4(0.0, 0.0, 0.0, 0.0);
    float distanceToClosest = 10000.0;
    int highestCount = 0;
    int startIndex = 0;
    for (int dy = -1; dy <= 1; dy++) {
        for (int dx = -1; dx <= 1; dx++) {
            ivec2 nearbyCellID = ivec2(cellID.x + dx, cellID.y + dy);
            // Ensure nearbyCellID is within grid bounds
            int cellIndex = (nearbyCellID.x + 1) + ((nearbyCellID.y + 1) * gridDims.x);
            if (cellIndex >= 0 && cellIndex < cellIndices.length()) {
                startIndex = cellIndices[cellIndex].x;
                int count = cellIndices[cellIndex].y;
                for (int i = 0; i < count; i++) {
                    vec4 point = points[startIndex + i];
                    float distance = distance(point.xy, vec2(global_coord));
                    if (distance < distanceToClosest) {
                        distanceToClosest = distance;
                        closestPoint = point;
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
    else
    {
        imageStore(outputImage, coord, vec4(height, 0.0, 0.0, 1.0));
    }
}
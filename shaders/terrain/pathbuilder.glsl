#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32) in;

layout(set = 0, binding = 0) uniform sampler2D noiseTexture;

layout(set = 0, binding = 1, r32f) uniform image2D outputImage;
layout(set = 0, binding = 2, r8) uniform image2D pathOutputImage;

layout(set = 0, binding = 3) restrict buffer ImageDimensions {
    int imageWidth;
    int imageHeight;
    int offsetX;
    int offsetY;
    int gridDimX;
    int gridDimY;
};

layout(std430, binding = 4) buffer CellIndexBuffer {
    ivec2 cellIndices[];
};

layout(std140, binding = 5) buffer PointsArrayBuffer {
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
            //clamp cellIndex as opposed to a if statement to avoid conditional branching
            cellIndex = clamp(cellIndex, 0, cellIndices.length() - 1);
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
    float adjustedHeight = height < 0.0 ? height / ((1.0 - height) * (1.0 - height)) : height * height * height;
    float isClose = step(distanceToClosest, 20.0);
    float isMedium = step(20.0, distanceToClosest) * (1.0 - step(300.0, distanceToClosest));
    float blendFactor = (distanceToClosest - 20.0) / 280.0;
    blendFactor = isMedium * blendFactor + isClose * (1.0 - isMedium);
    float finalHeight = mix(height, closestPoint.z, smoothstep(0.0, 1.0, isClose));
    finalHeight = mix(finalHeight, mix(closestPoint.z, height, smoothstep(0.0, 1.0, blendFactor)), smoothstep(0.0, 1.0, isMedium));
    vec4 color = vec4(finalHeight, isClose, 0.0, 1.0);
    vec4 pathColor = vec4(isClose * (1.0 - isMedium), 0.0, 0.0, 1.0);
    imageStore(outputImage, coord, color);
    imageStore(pathOutputImage, coord, pathColor);
}
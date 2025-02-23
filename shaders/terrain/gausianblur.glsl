#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32) in;

layout(set = 0, binding = 0) uniform sampler2D inputImage;
layout(set = 0, binding = 1, r32f) uniform image2D outputImage;

layout(set = 0, binding = 2) restrict buffer ImageDimensions {
    int imageWidth;
    int imageHeight;
};


// Define the Gaussian function
float gaussian(int x, int y, float sigma) {
    return exp(-(x * x + y * y) / (2 * sigma * sigma)) / (2 * 3.14159265 * sigma * sigma);
}

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    const int radius = 10;
    const float sigma = 5.0;
    vec4 sum = vec4(0.0);
    float weightSum = 0.0;

    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            ivec2 neighborCoord = coord + ivec2(dx, dy);
            vec2 uv = vec2(neighborCoord) / vec2(imageWidth, imageHeight);
            uv = clamp(uv, 0.0, 1.0);
            vec4 neighborColor = texture(inputImage, uv);
            float weight = gaussian(dx, dy, sigma);
            sum += neighborColor * weight;
            weightSum += weight;
        }
    }

    vec4 blurredColor = sum / weightSum;
    imageStore(outputImage, coord, blurredColor);
}
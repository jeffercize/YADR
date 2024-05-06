#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32) in;

layout(set = 0, binding = 0) uniform sampler2D inputImage;
layout(set = 0, binding = 1, rgba32f) uniform image2D outputImage;

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);

    const int radius = 5;
    vec4 sum = vec4(0.0);
    int count = 0;

    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            ivec2 neighborCoord = coord + ivec2(dx, dy);
            vec2 uv = vec2(neighborCoord) / vec2(8192, 4096);
            if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0) {
                vec4 neighborColor = texture(inputImage, uv);
                sum += neighborColor;
                count++;
            }
        }
    }

    vec4 averageColor = sum / float(count);
    imageStore(outputImage, coord, averageColor);
}
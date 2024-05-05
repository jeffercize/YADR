#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32, local_size_z = 1) in;

layout(binding = 0, rgba32f) uniform image2D noise;
layout(binding = 1, rgba32f) uniform image2D result;

layout(std140, binding = 2) uniform Axes {
    float x_axis;
    float y_axis;
};

void main() {
    float x = float(gl_GlobalInvocationID.x);
    float y = float(gl_GlobalInvocationID.y);

    bool edge_height = false;
    if (x < (0 + 200))
    {
        edge_height = true;
    }

    float height = imageLoad(noise, ivec2(gl_GlobalInvocationID.xy)).x;

    if (height < 0)
    {
        height = height / ((1 - height) * (1.0f - height));
    }

    if (y < (0 + 200))
    {
        height = height * (y / 200.0f);
    }
    else if (y > (x_axis - 200))
    {
        height = height * ((x_axis - y) / 200.0f);
    }
    if (edge_height)
    {
        height = height * (x / 200.0f);
    }

    imageStore(result, ivec2(gl_GlobalInvocationID.xy), vec4(height, 0, 0, 1));
}
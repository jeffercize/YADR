#[compute]
#version 450

layout(local_size_x = 32, local_size_y = 32) in;

layout(set = 0, binding = 0) uniform sampler2D noiseTexture;

layout(set = 0, binding = 1, std430) restrict buffer pathBuffer {
    vec3 pathData[];
};


layout(set = 0, binding = 2, rg32f) uniform image2D outputImage;


layout(set = 0, binding = 3) restrict buffer ImageDimensions {
    int imageWidth;
    int imageHeight;
};

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    vec2 uv = vec2(coord) / vec2(imageWidth, imageHeight);
    uv = clamp(uv, 0.0, 1.0);
    float pathHeight = 0.1;
    float height = texture(noiseTexture, uv).r;

    vec3 closestPoint = vec3(0.0, 0.0, 0.0);
    float distanceToClosest = 10000.0;
    for (int i = 0; i < pathData.length(); i += 1) 
    {
        if(pathData[i].x < 1.0 || pathData[i].y < 1.0)
        {
            continue;
        }
        float distance = distance(pathData[i].xy, vec2(coord));
        if (distance < distanceToClosest)
        {
            distanceToClosest = distance;
            closestPoint = pathData[i];
        }
        if (distance < 20.0)
        {
            break;
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
    }
    else if (distanceToClosest < 60.0)
    {
        float blendFactor = (distanceToClosest - 20.0) / 40.0;
        height = mix(closestPoint.z, height, blendFactor);
    }

    imageStore(outputImage, coord, vec4(height, 0.0, 0.0, 1.0));
}
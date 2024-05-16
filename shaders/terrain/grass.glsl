#version 450


void CreateTileGeometry() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    vec2 uv = vec2(coord) / vec2(imageWidth, imageHeight);

    float height = texture(noiseTexture, uv).r;

    if (height < 0.0) {
        height = height / ((1.0 - height) * (1.0 - height));
    }
    else 
    {
        height = height * height * height;
    }

    if (uv.y < 0.2) {
        height *= uv.y / 0.2;
    } 
    else if (uv.y > 0.8) 
    {
        height *= (1.0 - uv.y) / 0.2;
    }

    if (uv.x < 0.2) {
        height *= uv.x / 0.2;
    }

    vec4 pathPix = texture(pathTexture, uv);
    float pathHeight = pathPix.r;
    float pathWeight = pathPix.g;
    float newHeight = (pathHeight * pathWeight) + (height * (1.0 - pathWeight));

    imageStore(outputImage, coord, vec4(newHeight, 0.0, 0.0, 1.0));
}
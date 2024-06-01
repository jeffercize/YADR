shader_type spatial;

uniform sampler2D heightMap : repeat_disable, filter_nearest;
uniform sampler2D rockTexture;
uniform sampler2D grassTexture;
uniform vec2 heightParams = vec2(1.0, 1.0);
uniform float heightScale = 1.0;

varying float vSteepness;

void vertex() {
    UV = VERTEX.xz / heightParams;
    float height = texture(heightMap, UV).r * heightScale;
    VERTEX.y = height;

    // Calculate the normal
    vec2 offset = vec2(0.01, 0.01) / heightParams;
    float heightLeft = texture(heightMap, clamp(UV - vec2(offset.x, 0.0), 0.0, 1.0)).r;
    float heightRight = texture(heightMap, clamp(UV + vec2(offset.x, 0.0), 0.0, 1.0)).r;
    float heightDown = texture(heightMap, clamp(UV - vec2(0.0, offset.y), 0.0, 1.0)).r;
    float heightUp = texture(heightMap, clamp(UV + vec2(0.0, offset.y), 0.0, 1.0)).r;
    NORMAL = normalize(vec3(heightLeft - heightRight, 2.0, heightDown - heightUp));

	// Sample the six closest texels for slope
    float h00 = texture(heightMap, UV).r;
    float h10 = texture(heightMap, min(UV + vec2(1.0, 0.0) / heightParams.xy, 1.0)).r;
    float h01 = texture(heightMap, min(UV + vec2(0.0, 1.0) / heightParams.xy, 1.0)).r;
    float h11 = texture(heightMap, min(UV + vec2(1.0, 1.0) / heightParams.xy, 1.0)).r;
    float hm1 = texture(heightMap, max(UV - vec2(1.0, 0.0) / heightParams.xy, 0.0)).r;
    float h0m1 = texture(heightMap, max(UV - vec2(0.0, 1.0) / heightParams.xy, 0.0)).r;

    vec2 gradient = vec2(h10 - hm1, h01 - h0m1) * 200.0;
	vSteepness = length(gradient);


}

void fragment() {
    vec2 scaledUV = UV * 10.0;

    // Calculate the steepness factor
    float steepnessFactor = clamp(vSteepness, 0.0, 1.0);

    // Scale the UV.x as the steepness goes from 0.9 to 1.0
    float uvYScale = 1.0;
    float uvXScale = 1.0 ;
    scaledUV = vec2(scaledUV.x * uvXScale, scaledUV.y * uvYScale);

    vec3 rockColor = vec3(texture(rockTexture, scaledUV).xyz);
    vec3 grassColor = vec3(texture(grassTexture, scaledUV).xyz);

    // Mix the rock and grass colors based on the steepness
    ALBEDO = mix(grassColor, rockColor, smoothstep(0.0, 1.0, steepnessFactor));
}
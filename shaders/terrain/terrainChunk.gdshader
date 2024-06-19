shader_type spatial;

uniform sampler2D heightMap : repeat_disable;
uniform sampler2D pathMap : repeat_disable;
uniform sampler2D rockTexture;
uniform sampler2D grassTexture;
uniform sampler2D roadTexture;
uniform sampler2D rockNormalMap;
uniform sampler2D grassNormalMap;
uniform vec2 heightParams = vec2(512.0, 512.0);
uniform float heightScale = 1.0;

varying float vSteepness;
varying float vRoad;
varying float vRoadJitter;

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

void vertex() {
    UV = vec2(VERTEX.x/4.0 + 16.0, VERTEX.z/4.0 + 16.0) / heightParams;
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
    vRoad = texture(pathMap, UV).r;
    vRoadJitter = hash12(VERTEX.xz);
}

void fragment() {
    vec2 scaledUV = UV * 40.0;

    // Calculate the steepness factor
    float steepnessFactor = clamp(vSteepness, 0.0, 1.0);

    // Scale the UV.x as the steepness goes from 0.9 to 1.0
    float uvYScale = 1.0;
    float uvXScale = 1.0 ;
    scaledUV = vec2(scaledUV.x * uvXScale, scaledUV.y * uvYScale);

    vec3 rockColor = vec3(texture(rockTexture, scaledUV).xyz);
    vec3 grassColor = vec3(texture(grassTexture, scaledUV).xyz);
    grassColor = grassColor - vec3(0.32, 0.17, 0.25);

    vec3 roadColor = vec3(texture(roadTexture, scaledUV).xyz);
    roadColor = roadColor - vec3(0.4, 0.4, 0.4);


    // Sample the normal maps
    vec3 rockNormal = texture(rockNormalMap, scaledUV).xyz * 2.0 - 1.0;
    vec3 grassNormal = texture(grassNormalMap, scaledUV).xyz * 2.0 - 1.0;

    // Mix the rock and grass colors and normals based on the steepness
    float temp = vRoad - vRoadJitter * 0.1;
    float factor = mix(temp-0.2, 1.0, step(0.905, temp));

    ALBEDO = mix(
        mix(grassColor, rockColor, smoothstep(0.0, 1.0, steepnessFactor)),
        roadColor,
        factor
    );
    //ALBEDO = normalize(grassNormal * 2.0 - 1.0);

    //NORMAL = normalize(mix(grassNormal, rockNormal, smoothstep(0.0, 1.0, steepnessFactor)));
    //NORMAL = normalize(grassNormal * 2.0 - 1.0);
}
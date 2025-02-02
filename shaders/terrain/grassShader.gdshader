shader_type spatial;

global uniform float time = 1.0;

global uniform float windDirection = 0.0;

global uniform float windStrength = 0.0;

uniform float grassTotalWidth = 0.3;
uniform float grassTotalHeight = 1.5;

uniform vec2 globalOffset;

uniform sampler2D heightMap : repeat_disable, filter_nearest;
uniform vec2 heightParams;

//uniform vec4 grassParams;

varying vec4 vGrassParams;
varying float vSteepness;
varying vec3 vNormal;
varying vec3 vNormal2;
varying float vDontDraw;

float remap(float value, float inputMin, float inputMax, float outputMin, float outputMax) {
    return outputMin + ((value - inputMin) / (inputMax - inputMin)) * (outputMax - outputMin);
}

uvec4 murmurHash42(uvec2 src) {
    const uint M = 0x5bd1e995u;
    uvec4 h = uvec4(1190494759u, 2147483647u, 3559788179u, 179424673u);
    src *= M; src ^= src>>24u; src *= M;
    h *= M; h ^= src.x; h *= M; h ^= src.y;
    h ^= h>>13u; h *= M; h ^= h>>15u;
    return h;
}

vec4 hash42(vec2 src) {
  uvec4 h = murmurHash42(floatBitsToUint(src));
  return uintBitsToFloat(h & uvec4(0x007fffffu, 0x007fffffu, 0x007fffffu, 0x007fffffu) | uvec4(0x3f800000u, 0x3f800000u, 0x3f800000u, 0x3f800000u)) - 1.0;
}

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

float noise12(vec2 p) {
  vec2 i = floor(p);

  vec2 f = fract(p);
  vec2 u = smoothstep(vec2(0.0), vec2(1.0), f);

	float val = mix( mix( hash12( i + vec2(0.0, 0.0) ),
                        hash12( i + vec2(1.0, 0.0) ), u.x),
                   mix( hash12( i + vec2(0.0, 1.0) ),
                        hash12( i + vec2(1.0, 1.0) ), u.x), u.y);
  return val * 2.0 - 1.0;
}

mat3 rotateAxis(vec3 axis, float angle) {
  axis = normalize(axis);
  float s = sin(angle);
  float c = cos(angle);
  float oc = 1.0 - c;

  return mat3(
    vec3(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s),
    vec3(oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s),
    vec3(oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c)
  );
}

mat3 rotateY(float angle) {
    return mat3(
        vec3(cos(angle), 0.0, sin(angle)),
        vec3(0.0, 1.0, 0.0),
        vec3(-sin(angle), 0.0, cos(angle))
    );
}

mat3 rotateX(float angle) {
    return mat3(
        vec3(1.0, 0.0, 0.0),
        vec3(0.0, cos(angle), -sin(angle)),
        vec3(0.0, sin(angle), cos(angle))
    );
}

float easeOut(float x, float t) {
	return 1.0 - pow(1.0 - x, t);
}

float easeIn(float x, float t) {
	return pow(x, t);
}

void vertex() {
//Variable Prep\\
	vec3 grassBladeWorldPosTemp = (MODEL_MATRIX * vec4(0.0,0.0,0.0, 1.0)).xyz;
	float distance = distance(INV_VIEW_MATRIX[3].xz, grassBladeWorldPosTemp.xz);
	float distanceMod = clamp((220.0 - distance) / 20.0, 0.0, 1.0);
	VERTEX.y = VERTEX.y * INSTANCE_CUSTOM.z * (hash12(vec2(grassBladeWorldPosTemp.x, grassBladeWorldPosTemp.z))* 0.5 + 0.75) * distanceMod;
	float adjustedGrassTotalHeight = grassTotalHeight * INSTANCE_CUSTOM.z * (hash12(vec2(grassBladeWorldPosTemp.x, grassBladeWorldPosTemp.z))* 0.5 + 0.75) * distanceMod;
    // Store the origin point of the grass for math
    vec3 grassBladeWorldPos = (MODEL_MATRIX * vec4(0.0,0.0,0.0, 1.0)).xyz;
	//percentage goes from 0.0 to 1.0
	float heightPercent = VERTEX.y / adjustedGrassTotalHeight;
	float widthPercent = remap(VERTEX.x, 0.0, grassTotalWidth, 0.0, 1.0);

	//generate Hash Values for use around the shader
	vec4 hashVal1 = hash42(vec2(grassBladeWorldPos.x, grassBladeWorldPos.z));
	//1 = front, -1 = back
	float zSide = sign(NORMAL.z);
	//0 = left, 1 = right
	float xSide = step(grassTotalWidth / 2.0, VERTEX.x);


//Rotating the base and leaning\\

	//dont change angle if wind strength is low, but change it as the wind is stronger
	//so mix between 0 and windDirection with windStrength
	//target randomAngle so that randomAngle - INSTANCE_CUSTOM.y = windDirectionMix
	float windDirectionMix = mix(0.0, windDirection, windStrength);
	//float randomAngle = 0.0;//windDirectionMix - INSTANCE_CUSTOM.y;

    float randomAngle = hashVal1.x * 2.0 * PI; //small jitter on the rotation away from the clump

	//random lean
	float randomLean = remap(hashVal1.w, 0.0, 1.0, 0.1, 0.4) * heightPercent; //replace with sin noise

	//Noise Sampling for Wind\\
	float noiseSample = noise12(vec2(time * 0.35 * 0.5) + grassBladeWorldPos.xz);
	randomLean += noiseSample * heightPercent * 0.2;// * windStrength;



	float windNoiseSample = noise12(grassBladeWorldPos.xz * 0.5 + time*(windStrength+0.5));
	float windLeanAngle = remap(windNoiseSample, -1.0, 1.0, 0.25, 1.0);
	windLeanAngle = easeIn(windLeanAngle, 2.0) * 1.25;
	windLeanAngle *= heightPercent;
	windLeanAngle *= (windStrength+0.5);

	float windHeightMixValue = min(adjustedGrassTotalHeight, 1.0);
	float windStrengthMix = mix(0.2, 1.0, windStrength+0.5);

	float combinedWindMix = mix(0.0, windHeightMixValue, windStrengthMix);
	float windDirectionCombined = mix(randomAngle, windDirection, combinedWindMix);
	vec3 windAxis = vec3(cos(windDirectionCombined), 0.0, sin(windDirectionCombined));



    // Apply the rotation to the vertex position

	VERTEX.xyz = rotateY(randomAngle) * VERTEX.xyz;

	mat3 grassMat = rotateAxis(windAxis, windLeanAngle) * rotateX(randomLean);

	VERTEX.xyz = grassMat * VERTEX.xyz;


	//heightmap sampling

	vec2 heightmap_uv = vec2((grassBladeWorldPos.x-globalOffset.x+16.0) / heightParams.x, (grassBladeWorldPos.z-globalOffset.y+16.0) / heightParams.y);
	heightmap_uv = clamp(heightmap_uv, 0.0, 1.0);
	// Calculate the fractional part of the uv coordinates
	vec2 f = fract(heightmap_uv * heightParams.xy);

	// Sample the four closest texels
	float h00 = texture(heightMap, heightmap_uv).r;
	float h10 = texture(heightMap, heightmap_uv + vec2(1.0, 0.0) / heightParams.xy).r;
	float h01 = texture(heightMap, heightmap_uv + vec2(0.0, 1.0) / heightParams.xy).r;
	float h11 = texture(heightMap, heightmap_uv + vec2(1.0, 1.0) / heightParams.xy).r;
	float hm1 = texture(heightMap, heightmap_uv - vec2(1.0, 0.0) / heightParams.xy).r;
	float h0m1 = texture(heightMap, heightmap_uv - vec2(0.0, 1.0) / heightParams.xy).r;

	// Interpolate between the samples
	float heightMapSample = mix(mix(h00, h10, f.x), mix(h01, h11, f.x), f.y);

	//VERTEX.y += heightMapSample * 400.0 - 0.1;

	//get camera and view direction info for later
	vec3 cameraPosition = INV_VIEW_MATRIX[3].xyz;
	vec3 viewDir = normalize(cameraPosition - grassBladeWorldPos);
	vec3 viewDirXZ = normalize(vec3(viewDir.x, 0.0, viewDir.z));


//Rounded Normals\\
	float totalLean = randomLean *  heightPercent + windLeanAngle;

	float ncurve1 = -totalLean;
	vec3 n1 = vec3(0.0, (heightPercent + 0.01), 0.0);
	n1 = rotateX(ncurve1) * n1;

	float ncurve2 = -totalLean * 0.9;
	vec3 n2 = vec3(0.0, (heightPercent + 0.01) * 0.9, 0.0);
	n2 = rotateX(ncurve2) * n2;

	vec3 ncurve = normalize(n1 - n2);

	vec3 grassVertexNormal = vec3(0.0, -ncurve.z, ncurve.y);
	vec3 grassVertexNormal1 = rotateY(PI * 0.3 * zSide) * grassVertexNormal;
	vec3 grassVertexNormal2 = rotateY(PI * -0.3 * zSide) * grassVertexNormal;
	grassVertexNormal1 *= rotateY(randomAngle);
	grassVertexNormal1 *= grassMat;
	grassVertexNormal1 *= zSide;

	grassVertexNormal2 *= rotateY(randomAngle);
	grassVertexNormal2 *= grassMat;
	grassVertexNormal2 *= zSide;

	vNormal = normalize(MODEL_NORMAL_MATRIX * grassVertexNormal1);
	vNormal2 = normalize(MODEL_NORMAL_MATRIX * grassVertexNormal2);


	NORMAL = grassVertexNormal1;



//ViewSpace Fill\\
	//scary code that wasnt/isnt? working because its wild
	vec3 grassFaceNormal = vec3(0.0, 0.0, 1.0);
	grassFaceNormal *= rotateY(randomAngle);
	grassFaceNormal *= grassMat;
	grassFaceNormal *= zSide;

	float viewDotNormal = clamp(dot(grassFaceNormal, viewDirXZ), 0.0, 1.0);
	float viewSpaceThickenFactor = easeOut(1.0 - viewDotNormal, 4.0) * smoothstep(0.0, 0.2, viewDotNormal);
	//modify by heightpercentage
	//viewSpaceThickenFactor *= heightPercent;
	vGrassParams = vec4(heightPercent, grassBladeWorldPos.y, 1.0, widthPercent);
	VERTEX.x += viewSpaceThickenFactor * (xSide - 0.5) * grassTotalWidth * 0.5 * zSide;
}



void fragment() {
	float steepnessThreshold = 0.9;  // Adjust this value as needed

	//AO
	float density = 0.5;
	float aoForDensity = mix(1.0, 0.25, density);
	float ao = mix(aoForDensity, 1.0, easeIn(vGrassParams.x, 1.0));

	//ALBEDO
	vec3 baseColor = vec3(0.1, 0.15, 0.01);
	vec3 tipColor = vec3(0.15, 0.30, 0.01);
	vec3 diffuseColor = mix(baseColor, tipColor, easeIn(vGrassParams.x, 2.0));
	diffuseColor *= ao;
	ALBEDO = diffuseColor;
	//ALBEDO = vec3(vSteepness, 0.0, 0.0);

	//NORMAL
	vec3 normal2 = normalize(vNormal2);
	vec3 normal = normalize(mix(vNormal, normal2, vGrassParams.w));
	NORMAL = normalize(normal);

	//ALPHA
    // Calculate the distance to the camera
	//vec3 worldPos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	//float distance = length(worldPos - CAMERA_POSITION_WORLD);

	// Calculate the alpha value based on the distance
	//80 is range it is alpha=1 and 120 is alpha=0
    //float alpha = clamp((120.0 - distance) / (120.0 - 80.0), 0.0, 1.0);



	//ALPHA = alpha;

	//ROUGHNESS
	//ROUGHNESS = 0.9;
}

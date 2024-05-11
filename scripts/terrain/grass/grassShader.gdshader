shader_type spatial;

uniform float time = 0.0;

uniform float grassTotalWidth = 0.5;
uniform float grassTotalHeight = 1.0;

uniform sampler2D heightMap;
uniform vec2 heightParams;
//uniform vec4 grassParams;

varying vec4 vGrassParams;
varying vec3 vNormal;
varying vec3 vNormal2;

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
    // Store the origin point of the grass for math
    vec3 grassBladeWorldPos = (MODEL_MATRIX * vec4(0.0,0.0,0.0, 1.0)).xyz;
	//percentage goes from 0.0 to 1.0
	float heightPercent = VERTEX.y / grassTotalHeight;
	float widthPercent = remap(VERTEX.x, 0.0, grassTotalWidth, 0.0, 1.0);

	//generate Hash Values for use around the shader
	vec4 hashVal1 = hash42(vec2(grassBladeWorldPos.x, grassBladeWorldPos.z));
	//1 = front, -1 = back
	float zSide = sign(NORMAL.z);
	//0 = left, 1 = right
	float xSide = 0.0;
	if (VERTEX.x > grassTotalWidth/2.0) {
		xSide = 1.0;
	}

	//heightmap sampling
	vec2 uv = vec2(grassBladeWorldPos.x / heightParams.x, grassBladeWorldPos.z / heightParams.y);
    float heightMapSample = texture(heightMap, uv).r;
	VERTEX.y += heightMapSample*400.0;
	

	//get camera and view direction info for later
	vec3 cameraPosition = INV_VIEW_MATRIX[3].xyz;
	vec3 viewDir = normalize(cameraPosition - grassBladeWorldPos);
	vec3 viewDirXZ = normalize(vec3(viewDir.x, 0.0, viewDir.z));


//Rotating the base and leaning\\
	//Random rotational angle for Y axis (i think thats Y?)
    float randomAngle = hashVal1.x * 2.0 * PI;

	//random lean
	float randomLean = remap(hashVal1.w, 0.0, 1.0, 0.1, 0.4) * heightPercent;

	//Noise Sampling for Wind\\
	float noiseSample = noise12(vec2(time * 0.35) + grassBladeWorldPos.xz);
	randomLean += noiseSample * 0.1;
	
	float windDir = noise12(grassBladeWorldPos.xz * 0.05 + 0.05 * time);
	windDir = remap(windDir, -1.0, 1.0, 0.0, PI * 2.0);

	float windNoiseSample = noise12(grassBladeWorldPos.xz * 0.25 + time);
	float windLeanAngle = remap(windNoiseSample, -1.0, 1.0, 0.25, 1.0);
	windLeanAngle = easeIn(windLeanAngle, 2.0) * 1.25;
	windLeanAngle *= heightPercent;

	vec3 windAxis = vec3(cos(windDir), 0.0, sin(windDir));



	// Multiply the rotation matrixes together
    mat3 grassMat = rotateY(randomAngle) * rotateX(randomLean) * rotateAxis(windAxis, windLeanAngle);

    // Apply the rotation to the vertex position
    VERTEX.xyz = grassMat * VERTEX.xyz;
	

//Rounded Normals\\
	NORMAL = grassMat * NORMAL;
	vNormal = rotateY(PI * 0.3) * NORMAL;
	vNormal2 = rotateY(PI * -0.3) * NORMAL;



//ViewSpace Fill\\
	//scary code
	vec3 grassFaceNormal = vec3(0.0, 0.0, 1.0);
	grassFaceNormal = grassMat * grassFaceNormal;
	grassFaceNormal *= zSide;

	float viewDotNormal = clamp(dot(grassFaceNormal, viewDirXZ), 0.0, 1.0);
	float viewSpaceThickenFactor = smoothstep(0.0, 0.2, viewDotNormal);
	//modify by heightpercentage
	viewSpaceThickenFactor *= heightPercent;

	vGrassParams = vec4(heightPercent, grassBladeWorldPos.y, 1.0, widthPercent);
	VERTEX.x += viewSpaceThickenFactor * (xSide - 0.5) * grassTotalWidth * 0.5 * zSide;
}



void fragment() {
	//AO
	float density = 0.5;
	float aoForDensity = mix(1.0, 0.25, density);
	float ao = mix(aoForDensity, 1.0, easeIn(vGrassParams.x, 1.0));

	//ALBEDO
	vec3 baseColor = vec3(0.05, 0.2, 0.01);
	vec3 tipColor = vec3(0.5, 0.5, 0.1);
	vec3 diffuseColor = mix(baseColor, tipColor, easeIn(vGrassParams.x, 2.0));
	diffuseColor *= ao;
	ALBEDO = diffuseColor;

	//NORMAL
	float normalMixFactor = vGrassParams.w;
	vec3 normal = normalize(mix(vNormal, vNormal2, normalMixFactor));
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


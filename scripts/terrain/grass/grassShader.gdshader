shader_type spatial;

global uniform float time = 1.0;

global uniform float windDirection = 0.0;

global uniform float windStrength = 0.0;

uniform float grassTotalWidth = 0.3;
uniform float grassTotalHeight = 1.5;

uniform sampler2D heightMap;
uniform vec2 heightParams;

//uniform vec4 grassParams;

varying vec4 vGrassParams;
varying float vSteepness;
varying vec3 vNormal;
varying vec3 vNormal2;
varying vec3 color;

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
	VERTEX.y = VERTEX.y * INSTANCE_CUSTOM.z;
	float adjustedGrassTotalHeight = grassTotalHeight * INSTANCE_CUSTOM.z;
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
	float xSide = 0.0;
	if (VERTEX.x > grassTotalWidth/2.0) {
		xSide = 1.0;
	}


//Rotating the base and leaning\\

	//Random rotational angle for Y axis TODO this shit is broken
	vec2 direction = normalize(grassBladeWorldPos.xz - INSTANCE_CUSTOM.xy); //wtf is this getting direction
	float angleToClump = atan(direction.y, direction.x);
	float angleAway = angleToClump + PI;
	//float randomAngle = angleToClump;
    float randomAngle = hashVal1.x * 2.0 * PI; //small jitter on the rotation away from the clump

	//random lean
	float randomLean = remap(hashVal1.w, 0.0, 1.0, 0.1, 0.4) * heightPercent; //replace with sin noise

	//Noise Sampling for Wind\\
	float noiseSample = noise12(vec2(time * 0.35 * 0.5) + grassBladeWorldPos.xz);
	randomLean += noiseSample * windStrength;
	
	

	float windNoiseSample = noise12(grassBladeWorldPos.xz * 0.5 + time*(windStrength+0.5));
	float windLeanAngle = remap(windNoiseSample, -1.0, 1.0, 0.25, 1.0);
	windLeanAngle = easeIn(windLeanAngle, 2.0) * 1.25;
	windLeanAngle *= heightPercent;
	windLeanAngle *= (windStrength+0.5);
	
	float windMixValue = 1.0;
	if(adjustedGrassTotalHeight < 1.0)
	{
		windMixValue = adjustedGrassTotalHeight / 5.0;
	}
	float windDirectionCombined = mix(randomAngle, windDirection, windMixValue);
	vec3 windAxis = vec3(cos(windDirectionCombined), 0.0, sin(windDirectionCombined));



	// Multiply the rotation matrixes together


    // Apply the rotation to the vertex position
    VERTEX.xyz = rotateY(randomAngle) * VERTEX.xyz;

	mat3 grassMat = rotateAxis(windAxis, windLeanAngle) * rotateX(randomLean);

	VERTEX.xyz = grassMat * VERTEX.xyz;

	//heightmap sampling
	vec2 uv = vec2(grassBladeWorldPos.x / heightParams.x, grassBladeWorldPos.z / heightParams.y);

	// Calculate the fractional part of the uv coordinates
	vec2 f = fract(uv * heightParams.xy);

	// Sample the four closest texels
	float h00 = texture(heightMap, uv).r;
	float h10 = texture(heightMap, uv + vec2(1.0, 0.0) / heightParams.xy).r;
	float h01 = texture(heightMap, uv + vec2(0.0, 1.0) / heightParams.xy).r;
	float h11 = texture(heightMap, uv + vec2(1.0, 1.0) / heightParams.xy).r;
	float hm1 = texture(heightMap, uv - vec2(1.0, 0.0) / heightParams.xy).r;
	float h0m1 = texture(heightMap, uv - vec2(0.0, 1.0) / heightParams.xy).r;

	// Interpolate between the samples
	float heightMapSample = mix(mix(h00, h10, f.x), mix(h01, h11, f.x), f.y);

	VERTEX.y += heightMapSample * 400.0;

	vec2 gradient = vec2(h10 - hm1, h01 - h0m1) * 200.0;
	vSteepness = length(gradient);
	//color = vec3(grassBladeWorldPos.x / heightParams.x, grassBladeWorldPos.z / heightParams.y, 0.0);

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
    if (vSteepness > steepnessThreshold) {
        //ALBEDO = vec3(1,1,1);
		discard;
    }
	
	//AO
	float density = 0.5;
	float aoForDensity = mix(1.0, 0.25, density);
	float ao = mix(aoForDensity, 1.0, easeIn(vGrassParams.x, 1.0));

	//ALBEDO
	ALBEDO = color;
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


shader_type particles;
render_mode disable_force, disable_velocity;

uniform uint total_number;
uniform float scale_min = 0.9;
uniform float scale_max = 1.1;

uniform float terrain_region_size = 0.0;
uniform int terrain_region_map_size = 0;
uniform int terrain_region_map[256];
uniform vec2 terrain_region_offsets[256];
uniform float terrain_region_texel_size = 0.0009765625; // = 1./1024.
uniform float terrain_mesh_vertex_spacing = 1.0;
uniform sampler2DArray terrain_height_maps : repeat_disable;
uniform usampler2DArray terrain_control_maps : repeat_disable;
uniform float auto_slope : hint_range(0,10) = 1.0;
uniform float auto_height_reduction : hint_range(0, 1) = 0.1;
uniform int auto_base_texture : hint_range(0,31) = 0;
uniform int auto_overlay_texture : hint_range(0,31) = 1;

// This is unique to grass:
uniform int terrain_grass_materials[32];

global uniform float grass_visibility_range_end;
global uniform float grass_visibility_range_end_margin;

float rand_from_seed(inout uint seed) {
	int k;
	int s = int(seed);
	if (s == 0)
		s = 305420679;
		k = s / 127773;
		s = 16807 * (s - k * 127773) - 2836 * k;
	if (s < 0)
		s += 2147483647;
	seed = uint(s);
	return float(seed % uint(65536)) / 65535.0;
}

uint hash(uint x) {
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = ((x >> uint(16)) ^ x) * uint(73244475);
	x = (x >> uint(16)) ^ x;
	return x;
}

// Copied from Terrain3D shader
// Takes in UV2 region space coordinates, returns vec3 with:
// XY: (0 to 1) coordinates within a region
// Z: region index used for texturearrays, -1 if not in a region
vec3 terrain_get_region_uv2(vec2 uv) {
	ivec2 pos = ivec2(floor(uv)) + (terrain_region_map_size / 2);
	int bounds = int(pos.x>=0 && pos.x<terrain_region_map_size && pos.y>=0 && pos.y<terrain_region_map_size);
	int layer_index = terrain_region_map[ pos.y * terrain_region_map_size + pos.x ] * bounds - 1;
	return vec3(uv - terrain_region_offsets[layer_index], float(layer_index));
}

// Copied from Terrain3D shader
// Takes in UV world space coordinates, returns ivec3 with:
// XY: (0 to _region_size) coordinates within a region
// Z: region index used for texturearrays, -1 if not in a region
ivec3 terrain_get_region_uv(vec2 uv) {
	uv *= terrain_region_texel_size;
	ivec2 pos = ivec2(floor(uv)) + (terrain_region_map_size / 2);
	int bounds = int(pos.x>=0 && pos.x<terrain_region_map_size && pos.y>=0 && pos.y<terrain_region_map_size);
	int layer_index = terrain_region_map[ pos.y * terrain_region_map_size + pos.x ] * bounds - 1;
	return ivec3(ivec2((uv - terrain_region_offsets[layer_index]) * terrain_region_size), layer_index);
}

// Copied from Terrain3D shader
float terrain_get_height(vec2 uv) {
	float height = 0.0;
	vec3 region = terrain_get_region_uv2(uv);
	if (region.z >= 0.) {
		height = texture(terrain_height_maps, region).r;
	}
 	return height;
}

// Copied from Terrain3D shader
vec3 terrain_get_normal(vec2 uv) {
	// Get the height of the current vertex
	float height = terrain_get_height(uv);

	// Get the heights to the right and in front, but because of hardware 
	// interpolation on the edges of the heightmaps, the values are off
	// causing the normal map to look weird. So, near the edges of the map
	// get the heights to the left or behind instead. Hacky solution that 
	// reduces the artifact, but doesn't fix it entirely. See #185.
	float u, v;
	if(mod(uv.y*terrain_region_size, terrain_region_size) > terrain_region_size-2.) {
		v = terrain_get_height(uv + vec2(0, -terrain_region_texel_size)) - height;
	} else {
		v = height - terrain_get_height(uv + vec2(0, terrain_region_texel_size));
	}
	if(mod(uv.x*terrain_region_size, terrain_region_size) > terrain_region_size-2.) {
		u = terrain_get_height(uv + vec2(-terrain_region_texel_size, 0)) - height;		
	} else {
		u = height - terrain_get_height(uv + vec2(terrain_region_texel_size, 0));
	}

	vec3 normal = vec3(u, terrain_mesh_vertex_spacing, v);
	normal = normalize(normal);
	return normal;
}

vec3 anchor_to_terrain(vec3 pos, inout uint seed, out vec3 normal, out float grass_row) {
	vec2 uv = pos.xz;
	vec2 uv2 = (uv + vec2(0.5)) * terrain_region_texel_size;
	pos.y = terrain_get_height(uv2);
	
	normal = terrain_get_normal(uv2);
	if (dot(normal, vec3(0.0, 1.0, 0.0)) < 0.8) {
		pos.y = -1.0 / 0.0;
		return pos;
	}
	
	ivec3 index_uv = terrain_get_region_uv(uv);
	int region = index_uv.z;
	uint control = texelFetch(terrain_control_maps, index_uv, 0).r;
	
	// material & overlay taken from Terrain3D shader's get_material:
	bool auto_shader = region<0 || bool(control & 0x1u);
	int material = int(auto_shader)*auto_base_texture + int(!auto_shader)*int(control >>27u & 0x1Fu);
	int overlay = int(auto_shader)*auto_overlay_texture + int(!auto_shader)*int(control >> 22u & 0x1Fu);
	float blend = float(auto_shader)*clamp(
			dot(vec3(0., 1., 0.), normal * auto_slope*2. - (auto_slope*2.-1.)) 
			- auto_height_reduction*.01*pos.y // Reduce as vertices get higher
			, 0., 1.) + 
			 float(!auto_shader)*float(control >>14u & 0xFFu) * 0.003921568627450; // 1./255.0
	
	int material_grass = terrain_grass_materials[material];
	int overlay_grass = terrain_grass_materials[overlay];
	if (blend > 0.5) {
		grass_row = float(overlay_grass - 1);
	} else {
		grass_row = float(material_grass - 1);
	}
	
	return pos;
}

void start() {
	TRANSFORM = mat4(1.0);
	
	float extent = (grass_visibility_range_end + grass_visibility_range_end_margin) * 0.75;
	
	float area = (2.0 * extent) * (2.0 * extent);
	float cell_size = sqrt(area / float(total_number));
	int rows = int((extent * 2.0) / cell_size);

	vec3 emitter_pos = (EMISSION_TRANSFORM * vec4(vec3(0.0), 1.0)).xyz;
	vec3 snapped_emitter_pos = floor(emitter_pos / cell_size) * cell_size;
	
	ivec2 cell_index = ivec2(int(INDEX) % rows, int(INDEX) / rows);
	vec3 cell_pos = -vec3(extent, 0.0, extent) + vec3(cell_size * float(cell_index.x), 0.0, cell_size * float(cell_index.y));
	vec3 global_cell_pos = cell_pos + snapped_emitter_pos;
	
	uint seed = hash(uint(abs(global_cell_pos.x) + abs(global_cell_pos.z * 100.0)));
	float angle_rand = rand_from_seed(seed) * 2.0 * PI;
	TRANSFORM[0].xz = vec2(cos(angle_rand), -sin(angle_rand));
	TRANSFORM[2].xz = vec2(sin(angle_rand), cos(angle_rand));
	
	float scale_rand = rand_from_seed(seed);
	float scale = mix(scale_min, scale_max, scale_rand);
	TRANSFORM[0][0] = scale;
	TRANSFORM[1][1] = scale;
	TRANSFORM[2][2] = scale;
	
	vec3 pos_offset = vec3(rand_from_seed(seed), 0.0, rand_from_seed(seed)) - 0.5;
	TRANSFORM[3].xyz = cell_pos + pos_offset * cell_size + snapped_emitter_pos;
	TRANSFORM[3].y = emitter_pos.y;
	
	if (terrain_region_map_size > 0) {
		TRANSFORM[3].xyz = anchor_to_terrain(TRANSFORM[3].xyz, seed, CUSTOM.xyz, CUSTOM.w);
	}
}

extends Node3D

const MAX_DENSITY = 8.0

@export var grass: Mesh

var particles: GPUParticles3D

func _ready() -> void:
	_update_settings()

func _enter_tree() -> void:
	particles = $GPUParticles3D
	if grass:
		particles.draw_pass_1 = grass
	
	var terrain := get_parent() as Terrain3D
	
	if !terrain:
		return
	
	_copy_terrain_param(terrain, &"terrain_region_size", &"_region_size")
	_copy_terrain_param(terrain, &"terrain_region_map_size", &"_region_map_size")
	_copy_terrain_param(terrain, &"terrain_region_map", &"_region_map")
	_copy_terrain_param(terrain, &"terrain_region_offsets", &"_region_offsets")
	_copy_terrain_param(terrain, &"terrain_region_texel_size", &"_region_texel_size")
	_copy_terrain_param(terrain, &"terrain_mesh_vertex_spacing", &"_mesh_vertex_spacing")
	_copy_terrain_param(terrain, &"terrain_height_maps", &"_height_maps")
	_copy_terrain_param(terrain, &"terrain_control_maps", &"_control_maps")
	_copy_terrain_param(terrain, &"auto_slope", &"auto_slope")
	_copy_terrain_param(terrain, &"auto_height_reduction", &"auto_height_reduction")
	_copy_terrain_param(terrain, &"auto_base_texture", &"auto_base_texture")
	_copy_terrain_param(terrain, &"auto_overlay_texture", &"auto_overlay_texture")
	
	var grass_materials := PackedByteArray()
	grass_materials.resize(terrain.texture_list.get_texture_count())
	for i in range(terrain.texture_list.get_texture_count()):
		var t := terrain.texture_list.get_texture(i)
		if t.has_meta(&"grass"):
			grass_materials[i] = t.get_meta(&"grass", 0) + 1
	particles.process_material.set_shader_parameter(&"terrain_grass_materials", grass_materials)

func _get_setting_fade_end() -> float:
	return ProjectSettings.get_setting("shader_globals/grass_visibility_range_end").value + ProjectSettings.get_setting("shader_globals/grass_visibility_range_end_margin").value

func _get_setting_density() -> float:
	# Customise this to your needs, using e.g. player's chosen graphics settings
	return MAX_DENSITY

func _update_settings() -> void:
	var fade_end: float = _get_setting_fade_end()
	
	particles.visibility_aabb.position.x = -fade_end
	particles.visibility_aabb.size.x = fade_end * 2.0
	particles.visibility_aabb.position.z = -fade_end
	particles.visibility_aabb.size.z = fade_end * 2.0
	particles.process_material.set_shader_parameter(&"emission_box_extents", Vector3(fade_end, 0.0, fade_end) * 0.75)
	
	particles.amount = int(fade_end * fade_end * _get_setting_density())
	particles.process_material.set_shader_parameter(&"total_number", particles.amount)

func _copy_terrain_param(terrain:Terrain3D, particle_name: StringName, terrain_name: StringName) -> void:
	var value = RenderingServer.material_get_param(terrain.material.get_material_rid(), terrain_name)
	particles.process_material.set_shader_parameter(particle_name, value)

func _process(_delta: float) -> void:
	var camera := get_viewport().get_camera_3d()
	if camera:
		var target: Vector3 = camera.global_transform * Vector3(0, 0, -_get_setting_fade_end() * 0.5)
		global_position.x = target.x
		global_position.z = target.z

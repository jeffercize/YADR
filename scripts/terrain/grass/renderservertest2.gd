extends Node3D


# Called when the node enters the scene tree for the first time.
func _ready():
	var box_instance
	var box_mesh = BoxMesh.new()
	var rs = RenderingServer
	box_instance = rs.instance_create()
	rs.instance_set_base(box_instance, box_mesh)
	rs.instance_set_scenario(box_instance, get_world_3d().scenario)
	var trans = Transform3D(Basis.IDENTITY, Vector3.ZERO)
	rs.instance_set_transform(box_instance, trans)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta):
	pass

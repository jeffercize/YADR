extends VehicleBody3D

const carOn = false
const STEER_SPEED = 1.5
const STEER_LIMIT = 0.4

@export var engine_force_value := 40.0

var previous_speed := linear_velocity.length()
var _steer_target := 0.0

@onready var desired_engine_pitch: float = $EngineSound.pitch_scale


func _physics_process(delta: float):
	if abs(linear_velocity.length() - previous_speed) > 1.0:
		# Sudden velocity change, likely due to a collision. Play an impact sound to give audible feedback,
		# and vibrate for haptic feedback.
		#$ImpactSound.play()
		Input.vibrate_handheld(100)
		for joypad in Input.get_connected_joypads():
			Input.start_joy_vibration(joypad, 0.0, 0.5, 0.1)
	
	if !carOn:
		$EngineSound.stop()
	
	var fwd_mps := (linear_velocity * transform.basis).x

	_steer_target = Input.get_axis(&"VRight", &"VLeft")
	_steer_target *= STEER_LIMIT

	# Engine sound simulation (not realistic, as this car script has no notion of gear or engine RPM).
	desired_engine_pitch = 0.05 + linear_velocity.length() / (engine_force_value * 0.5)
	# Change pitch smoothly to avoid abrupt change on collision.
	$EngineSound.pitch_scale = lerpf($EngineSound.pitch_scale, desired_engine_pitch, 0.2)



	if Input.is_action_pressed(&"VForward"):
		# Increase engine force at low speeds to make the initial acceleration faster.
		var speed := linear_velocity.length()
		if speed < 5.0 and not is_zero_approx(speed):
			engine_force = clampf(engine_force_value * 5.0 / speed, 0.0, 100.0)
		else:
			engine_force = engine_force_value

		# Apply analog throttle factor for more subtle acceleration if not fully holding down the trigger.
		engine_force *= Input.get_action_strength(&"VForward")
	else:
		engine_force = 0.0

	if Input.is_action_pressed(&"VBackward"):
		# Increase engine force at low speeds to make the initial acceleration faster.
		if fwd_mps >= -1.0:
			var speed := linear_velocity.length()
			if speed < 5.0 and not is_zero_approx(speed):
				engine_force = -clampf(engine_force_value * 5.0 / speed, 0.0, 100.0)
			else:
				engine_force = -engine_force_value

			# Apply analog brake factor for more subtle braking if not fully holding down the trigger.
			engine_force *= Input.get_action_strength(&"VBackward")
		else:
			brake = 0.0
	else:
		brake = 0.0

	steering = move_toward(steering, _steer_target, STEER_SPEED * delta)

	previous_speed = linear_velocity.length()

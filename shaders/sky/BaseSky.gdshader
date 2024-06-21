shader_type sky;

uniform vec3 skyColor = vec3(0.5, 0.5, 1.0);
uniform vec3 horizonColor = vec3(0.6, 0.4, 0.8);

void sky() {
	float col = clamp(EYEDIR.y / 0.05, 0.0, 1.0);
	vec3 finalColor = mix(horizonColor,skyColor, col);
	COLOR = finalColor;
}

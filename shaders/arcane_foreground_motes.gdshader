shader_type canvas_item;

uniform float density : hint_range(0.0, 1.0) = 0.16;
uniform float intensity : hint_range(0.0, 1.0) = 0.22;
uniform vec4 cold_color : source_color = vec4(0.36, 0.78, 1.0, 1.0);
uniform vec4 warm_color : source_color = vec4(0.9, 0.34, 1.0, 1.0);

float hash21(vec2 point) {
	return fract(sin(dot(point, vec2(91.7, 217.3))) * 43758.5453);
}

void fragment() {
	vec2 grid_uv = UV * vec2(17.0, 10.0);
	vec2 cell_id = floor(grid_uv);
	float seed = hash21(cell_id);
	float active = step(1.0 - density, seed);
	vec2 local = fract(grid_uv) - 0.5;
	local.x += sin(TIME * 0.13 + seed * 22.0) * 0.22;
	local.y += cos(TIME * 0.09 + seed * 17.0) * 0.18;

	float core = 1.0 - smoothstep(0.014, 0.055, length(local));
	float horizontal_ray = (1.0 - smoothstep(0.0, 0.012, abs(local.y))) *
		(1.0 - smoothstep(0.02, 0.16, abs(local.x)));
	float vertical_ray = (1.0 - smoothstep(0.0, 0.012, abs(local.x))) *
		(1.0 - smoothstep(0.02, 0.16, abs(local.y)));
	float twinkle = 0.4 + 0.6 * sin(TIME * (0.35 + seed * 0.55) + seed * 31.0);
	float sparkle = active * max(core, max(horizontal_ray, vertical_ray) * 0.32) * twinkle;
	vec3 color = mix(cold_color.rgb, warm_color.rgb, seed);
	COLOR = vec4(color * sparkle * intensity, sparkle * intensity * 0.7);
}

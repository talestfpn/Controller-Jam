shader_type canvas_item;

uniform vec4 primary_color : source_color = vec4(0.3, 0.72, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.8, 0.2, 1.0, 1.0);
uniform float speed : hint_range(0.0, 3.0) = 0.45;
uniform float intensity : hint_range(0.0, 1.0) = 0.72;

void fragment() {
	float center_fade = pow(max(0.0, 1.0 - abs(UV.x - 0.5) * 2.0), 0.42);
	float line_core = 1.0 - smoothstep(0.06, 0.46, abs(UV.y - 0.5));
	float travel = 0.5 + 0.5 * sin(UV.x * 16.0 - TIME * speed * 6.28318);
	vec3 color = mix(primary_color.rgb, secondary_color.rgb, travel);
	float spark = pow(max(0.0, sin(UV.x * 54.0 - TIME * speed * 9.0)), 14.0);
	float alpha = center_fade * line_core * (0.42 + intensity * 0.48);
	color += spark * secondary_color.rgb * intensity * 0.45;
	COLOR = vec4(color, alpha);
}

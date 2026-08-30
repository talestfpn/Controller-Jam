shader_type canvas_item;

uniform vec4 empty_color : source_color = vec4(0.025, 0.012, 0.04, 0.9);
uniform vec4 edge_color : source_color = vec4(0.22, 0.08, 0.28, 0.9);
uniform float corner_radius : hint_range(0.0, 0.3) = 0.1;
uniform float edge_width : hint_range(0.0, 0.15) = 0.025;

float rounded_box(vec2 point, vec2 half_size, float radius) {
	vec2 q = abs(point) - half_size + radius;
	return length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0) - radius;
}

void fragment() {
	vec2 point = (UV - vec2(0.5)) * 2.0;
	float distance_field = rounded_box(
		point,
		vec2(0.97, 0.78),
		corner_radius);
	float aa = max(fwidth(distance_field) * 1.5, 0.002);
	float mask = 1.0 - smoothstep(0.0, aa, distance_field);
	float border = mask - (1.0 - smoothstep(-edge_width, -edge_width + aa, distance_field));
	vec3 color = empty_color.rgb;
	color += edge_color.rgb * max(border, 0.0) * 0.82;
	COLOR = vec4(color, max(empty_color.a * mask, edge_color.a * max(border, 0.0)));
}

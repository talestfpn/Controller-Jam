shader_type canvas_item;

uniform float value_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float trail_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float segment_count : hint_range(3.0, 24.0, 1.0) = 12.0;
uniform float thickness : hint_range(0.1, 1.0) = 0.68;
uniform float skew : hint_range(-1.0, 1.0) = 0.42;
uniform float segment_gap : hint_range(0.0, 0.4) = 0.16;
uniform float edge_width : hint_range(0.0, 0.2) = 0.045;
uniform float glow_strength : hint_range(0.0, 1.0) = 0.25;
uniform float impact_pulse : hint_range(0.0, 1.0) = 0.0;
uniform vec4 fill_color : source_color = vec4(1.0, 0.08, 0.3, 1.0);
uniform vec4 trail_color : source_color = vec4(1.0, 0.55, 0.2, 1.0);
uniform vec4 empty_color : source_color = vec4(0.12, 0.08, 0.18, 0.72);
uniform vec4 edge_color : source_color = vec4(1.0, 0.58, 0.72, 1.0);

float sd_parallelogram(vec2 p, float wi, float he, float sk) {
	vec2 e = vec2(sk, he);
	p = (p.y < 0.0) ? -p : p;
	vec2 w = p - e;
	w.x -= clamp(w.x, -wi, wi);
	vec2 d = vec2(dot(w, w), -w.y);
	float s = p.x * e.y - p.y * e.x;
	p = (s < 0.0) ? -p : p;
	vec2 v = p - vec2(wi, 0.0);
	v -= e * clamp(dot(v, e) / dot(e, e), -1.0, 1.0);
	d = min(d, vec2(dot(v, v), wi * he - abs(s)));
	return sqrt(d.x) * sign(-d.y);
}

void fragment() {
	float safe_segments = max(1.0, floor(segment_count + 0.5));
	float scaled_x = UV.x * safe_segments;
	float segment_index = floor(scaled_x);
	vec2 cell_uv = vec2(fract(scaled_x), UV.y);
	vec2 p = (cell_uv - vec2(0.5)) * 2.0;

	float cell_thickness = max(0.08, thickness - segment_gap);
	float distance_field = sd_parallelogram(p, cell_thickness, 0.88, skew);
	float aa = max(fwidth(distance_field) * 1.5, 0.002);
	float shape_mask = 1.0 - smoothstep(0.0, aa, distance_field);
	float inner_mask = 1.0 - smoothstep(-edge_width, -edge_width + aa, distance_field);
	float edge_mask = max(0.0, shape_mask - inner_mask);
	float glow_mask = max(
		0.0,
		(1.0 - smoothstep(0.0, 0.18 + glow_strength * 0.08, distance_field)) - shape_mask);

	float segment_position = (segment_index + 0.5) / safe_segments;
	float is_trail = 1.0 - step(trail_ratio, segment_position);
	float is_filled = 1.0 - step(value_ratio, segment_position);
	vec4 segment_color = mix(empty_color, trail_color, is_trail);
	segment_color = mix(segment_color, fill_color, is_filled);

	float energy = glow_strength * (0.5 + 0.5 * sin(TIME * 4.0 + segment_index * 0.7));
	segment_color.rgb += fill_color.rgb * energy * is_filled * 0.16;
	float sheen_position = fract(TIME * 0.22);
	float sheen = 1.0 - smoothstep(0.0, 0.14, abs(UV.x - sheen_position));
	segment_color.rgb = mix(
		segment_color.rgb,
		vec3(1.0),
		sheen * is_filled * (0.035 + glow_strength * 0.06));
	segment_color.rgb = mix(segment_color.rgb, vec3(1.0), impact_pulse * 0.34);

	vec4 final_color = segment_color * inner_mask;
	final_color += edge_color * edge_mask * (0.75 + impact_pulse * 0.25);
	final_color.rgb += fill_color.rgb * glow_mask * is_filled * glow_strength * 0.7;
	final_color.a = max(
		final_color.a,
		glow_mask * is_filled * glow_strength * 0.36);
	final_color.a *= shape_mask;
	final_color.a = max(
		final_color.a,
		glow_mask * is_filled * glow_strength * 0.36);
	COLOR = final_color;
}

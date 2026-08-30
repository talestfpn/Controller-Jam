shader_type canvas_item;

uniform float energy : hint_range(0.0, 1.0) = 0.14;
uniform float beat_phase : hint_range(0.0, 1.0) = 0.0;
uniform float impact : hint_range(0.0, 1.0) = 0.0;
uniform float tier_flash : hint_range(0.0, 1.0) = 0.0;
uniform float break_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec4 primary_color : source_color = vec4(0.32, 0.76, 1.0, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.72, 0.22, 1.0, 1.0);
uniform vec4 core_color : source_color = vec4(0.018, 0.008, 0.055, 0.0);

void fragment() {
	vec2 p = UV - vec2(0.5);
	p.x *= 0.92;
	float radius = length(p);
	float angle = atan(p.y, p.x);
	float beat = pow(max(0.0, 1.0 - beat_phase), 5.0);
	float travel = TIME * (0.42 + energy * 0.85) + beat_phase * 0.45;

	float spiral_a = sin(angle * 5.0 - radius * 31.0 + travel * 6.28318);
	float spiral_b = sin(angle * -8.0 + radius * 46.0 - travel * 4.4);
	float chroma = clamp(0.5 + spiral_a * 0.3 + spiral_b * 0.18, 0.0, 1.0);
	vec3 neon = mix(primary_color.rgb, secondary_color.rgb, chroma);

	float inner = 1.0 - smoothstep(0.06, 0.46, radius);
	float aura = 1.0 - smoothstep(0.2, 0.68, radius);
	float ring_center = 0.34 + sin(angle * 4.0 + travel * 3.0) * (0.014 + energy * 0.018);
	float ring = 1.0 - smoothstep(0.018 + energy * 0.012, 0.06, abs(radius - ring_center));
	float sparks = pow(max(0.0, sin(angle * 17.0 + travel * 8.0)), 16.0);
	sparks *= 1.0 - smoothstep(0.28, 0.6, radius);

	float strength = 0.1 + energy * 0.48 + beat * energy * 0.22;
	float alpha = aura * strength * (0.24 + chroma * 0.16);
	alpha += ring * (0.08 + energy * 0.24);
	alpha += sparks * energy * 0.24;

	vec3 color = core_color.rgb * core_color.a * inner;
	color += neon * aura * strength;
	color += neon * ring * (0.18 + energy * 0.55);
	color += vec3(1.0) * sparks * energy * 0.45;
	color = mix(color, vec3(1.0), max(impact * 0.42, tier_flash * 0.68) * inner);

	float fracture = step(0.54, fract(angle * 4.2 + radius * 19.0 + TIME * 5.0));
	color = mix(color, vec3(0.025, 0.005, 0.08), break_amount * fracture * 0.78);
	alpha *= 1.0 - break_amount * fracture * 0.72;
	COLOR = vec4(color, clamp(alpha, 0.0, 0.82));
}

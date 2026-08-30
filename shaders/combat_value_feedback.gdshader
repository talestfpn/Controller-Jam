shader_type canvas_item;

uniform vec4 primary_color : source_color = vec4(1.0, 0.34, 0.62, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.42, 0.82, 1.0, 1.0);
uniform float energy : hint_range(0.0, 1.0) = 0.7;
uniform float direction : hint_range(-1.0, 1.0) = 1.0;

void fragment() {
	vec2 p = UV - vec2(0.5);
	p.x *= 2.25;
	float radius = length(p);
	float core = 1.0 - smoothstep(0.08, 0.52, radius);
	float aura = 1.0 - smoothstep(0.2, 0.72, radius);
	float sweep = 0.5 + 0.5 * sin(
		p.x * 11.0 - p.y * 8.0 + TIME * (5.0 + energy * 4.0) * direction);
	float ripples = 0.5 + 0.5 * sin(radius * 30.0 - TIME * 7.0);
	vec3 neon = mix(primary_color.rgb, secondary_color.rgb, sweep);
	float alpha = aura * (0.08 + energy * 0.22) + core * ripples * 0.08;
	COLOR = vec4(neon * (0.28 + energy * 0.52), alpha);
}

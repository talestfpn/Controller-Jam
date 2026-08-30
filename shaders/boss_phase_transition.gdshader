shader_type canvas_item;

uniform vec4 primary_color : source_color = vec4(0.35, 0.08, 0.7, 1.0);
uniform vec4 secondary_color : source_color = vec4(1.0, 0.52, 0.18, 1.0);
uniform float phase_intensity : hint_range(0.0, 1.2, 0.01) = 0.55;

void fragment()
{
    vec2 centered = UV - vec2(0.5);
    float distance_from_center = length(centered);
    float pulse = 0.5 + 0.5 * sin(TIME * 7.0);
    float ring = 0.5 + 0.5 * sin(distance_from_center * 34.0 - TIME * 9.0);
    float scan = 0.5 + 0.5 * sin((UV.y + TIME * 0.18) * 84.0);
    float glitch = step(0.84, fract(sin(floor(UV.y * 42.0) + floor(TIME * 7.0)) * 43758.5453));
    vec3 color = mix(primary_color.rgb, secondary_color.rgb, ring * 0.38 + glitch * 0.18);
    float vignette = 1.0 - smoothstep(0.15, 0.78, distance_from_center);
    float alpha = (0.05 + ring * 0.08 + scan * 0.025 + glitch * 0.06) *
        phase_intensity * vignette * (0.7 + pulse * 0.3);
    COLOR = vec4(color, alpha);
}

shader_type canvas_item;

uniform vec4 glow_color : source_color = vec4(0.95, 0.55, 0.15, 1.0);
uniform float intensity : hint_range(0.0, 2.0) = 0.6;
uniform float speed : hint_range(0.0, 4.0) = 1.0;

void fragment()
{
    vec2 centered = abs(UV - vec2(0.5)) * 2.0;
    float edge = max(centered.x, centered.y);
    float aura = 1.0 - smoothstep(0.72, 1.04, edge);
    float pulse = 0.82 + 0.18 * sin(TIME * speed);
    COLOR = vec4(glow_color.rgb, aura * intensity * pulse);
}

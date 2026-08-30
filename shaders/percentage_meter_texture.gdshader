shader_type canvas_item;

uniform sampler2D body_texture;
uniform vec4 fill_color : source_color = vec4(1.0, 0.08, 0.3, 1.0);
uniform vec4 frame_color : source_color = vec4(1.0, 0.38, 0.62, 1.0);
uniform vec4 empty_color : source_color = vec4(0.025, 0.012, 0.04, 0.9);
uniform float value_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float glow_strength : hint_range(0.0, 1.0) = 0.3;
uniform float inner_left = 0.0833333;
uniform float inner_top = 0.272727;
uniform float inner_right = 0.916667;
uniform float inner_bottom = 0.636364;
uniform float fill_crop_left = 0.0652174;
uniform float fill_crop_right = 0.934783;

void fragment() {
    vec4 body = texture(body_texture, UV);
    vec2 region_size = vec2(inner_right - inner_left, inner_bottom - inner_top);
    vec2 region_uv = (UV - vec2(inner_left, inner_top)) / region_size;
    vec2 fill_uv = vec2(
        mix(fill_crop_left, fill_crop_right, clamp(region_uv.x, 0.0, 1.0)),
        clamp(region_uv.y, 0.0, 1.0));
    vec4 source = texture(TEXTURE, fill_uv);

    float inside_region = step(inner_left, UV.x)
        * step(UV.x, inner_right)
        * step(inner_top, UV.y)
        * step(UV.y, inner_bottom);
    float body_opening = 1.0 - smoothstep(0.0, 0.08, body.a);
    float progress_edge = mix(inner_left, inner_right, value_ratio);
    float progress_mask = step(UV.x, progress_edge);
    float alpha = source.a * inside_region * body_opening * progress_mask;

    float brightness = dot(source.rgb, vec3(0.2126, 0.7152, 0.0722));
    vec3 color = source.rgb * (0.82 + brightness * 0.32);
    float sheen_position = fract(TIME * 0.16);
    float sheen = 1.0 - smoothstep(0.0, 0.11, abs(region_uv.x - sheen_position));
    color += fill_color.rgb * sheen * glow_strength * 0.12;
    color += frame_color.rgb * sheen * glow_strength * 0.025;

    COLOR = vec4(color, alpha * fill_color.a);
}

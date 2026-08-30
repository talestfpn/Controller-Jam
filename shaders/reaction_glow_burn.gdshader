shader_type canvas_item;
render_mode unshaded;

uniform vec4 glow_color : source_color = vec4(0.25, 1.0, 0.35, 1.0);
uniform vec4 burn_color : source_color = vec4(1.0, 0.3, 0.8, 1.0);
uniform float glow_strength : hint_range(0.0, 3.0) = 0.5;
uniform float pulse_speed : hint_range(0.0, 16.0) = 6.0;

// Pixelated radial burn, adapted from the referenced CC0 shader. The noise is
// procedural so the scene does not depend on an extra placeholder texture.
uniform vec2 burn_origin = vec2(0.5, 0.5);
uniform float burn_progress : hint_range(0.0, 1.0) = 0.0;
uniform float burn_start : hint_range(0.0, 0.9) = 0.46;
uniform float border_width : hint_range(0.005, 0.25) = 0.09;
uniform float burn_noise : hint_range(0.0, 0.4) = 0.16;
uniform float pixel_size : hint_range(0.002, 0.08) = 0.018;
uniform float blend_steps : hint_range(2.0, 16.0) = 8.0;

float hash21(vec2 point) {
    point = fract(point * vec2(123.34, 456.21));
    point += dot(point, point + 45.32);
    return fract(point.x * point.y);
}

float artwork_alpha(vec4 texel) {
    // The current reaction sheet already has a real transparent background.
    // Respecting its alpha keeps bright painted details intact.
    return texel.a;
}

void fragment() {
    vec4 texel = texture(TEXTURE, UV);
    float base_alpha = artwork_alpha(texel);

    // The crop leaves transparent room around each drawing, allowing the glow
    // to expand without needing a visual node created by code.
    vec2 glow_step = TEXTURE_PIXEL_SIZE * 7.0;
    float glow_alpha = 0.0;
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV + vec2(glow_step.x, 0.0))));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV - vec2(glow_step.x, 0.0))));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV + vec2(0.0, glow_step.y))));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV - vec2(0.0, glow_step.y))));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV + glow_step)));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV - glow_step)));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV + vec2(glow_step.x, -glow_step.y))));
    glow_alpha = max(glow_alpha, artwork_alpha(texture(TEXTURE, UV + vec2(-glow_step.x, glow_step.y))));

    vec2 safe_region_size = max(REGION_RECT.zw, vec2(0.00001));
    vec2 local_uv = (UV - REGION_RECT.xy) / safe_region_size;
    vec2 snapped_uv = floor(local_uv / pixel_size) * pixel_size;
    float noise_value = hash21(floor(snapped_uv / pixel_size));
    float distance_from_origin = length(burn_origin - snapped_uv) +
        noise_value * burn_noise;

    float burn_amount = smoothstep(burn_start, 1.0, burn_progress);
    float radius = mix(-0.22, 1.18, burn_amount);
    float survives = step(radius, distance_from_origin);
    float distance_to_border = abs(distance_from_origin - radius);
    float border_mask = 1.0 - smoothstep(
        border_width,
        border_width * 1.8,
        distance_to_border
    );
    border_mask = floor(border_mask * blend_steps) / max(blend_steps, 1.0);
    border_mask *= step(0.001, burn_amount);

    float pulse = 0.93 + 0.07 * sin(TIME * pulse_speed);
    float outer_glow = max(glow_alpha - base_alpha, 0.0);
    float painted_core = smoothstep(0.18, 0.78, base_alpha);
    vec3 artwork_color = mix(glow_color.rgb, texel.rgb, painted_core);
    vec3 color = mix(artwork_color, burn_color.rgb, border_mask * 0.9);
    color += glow_color.rgb * (
        base_alpha * 0.035 + outer_glow * 0.35 * glow_strength * pulse
    );

    float final_alpha = max(base_alpha, outer_glow * 0.32 * glow_strength);
    final_alpha *= survives;
    COLOR = vec4(color, clamp(final_alpha, 0.0, 1.0));
}

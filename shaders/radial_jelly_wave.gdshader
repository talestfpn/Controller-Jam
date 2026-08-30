shader_type canvas_item;
render_mode unshaded;

// Radial adaptation of the asymmetric wave-line idea. The prompt timing is
// still controlled by C#; this shader only deforms the rendered ring.
uniform vec4 primary_color : source_color = vec4(1.0, 0.72, 0.3, 1.0);
uniform vec4 secondary_color : source_color = vec4(1.0, 0.25, 0.72, 1.0);

uniform float base_radius : hint_range(0.2, 0.95) = 0.78;
uniform float thickness : hint_range(0.005, 0.12) = 0.022;
uniform float glow_width : hint_range(0.01, 0.2) = 0.075;

uniform float wave_amplitude : hint_range(0.0, 0.2) = 0.035;
uniform float wave_frequency : hint_range(1.0, 24.0) = 7.0;
uniform float wave_speed : hint_range(-12.0, 12.0) = 2.2;
uniform float left_falloff : hint_range(0.1, 8.0) = 1.0;
uniform float right_falloff : hint_range(0.1, 8.0) = 1.0;

// Allows a larger drawing canvas without changing the apparent ring size.
// ReactionWave uses this extra transparent margin to avoid rectangular clipping.
uniform float effect_scale : hint_range(0.25, 1.5) = 1.0;

// 0=ambient, 1=perfect, 2=good, 3=ok, 4=parry, 5=miss.
uniform int reaction_mode : hint_range(0, 5) = 0;
uniform float reaction_progress : hint_range(0.0, 1.0) = 0.0;
uniform float opacity : hint_range(0.0, 1.0) = 1.0;

float gaussian(float value, float center, float width) {
    float normalized = (value - center) / max(width, 0.0001);
    return exp(-(normalized * normalized));
}

void fragment() {
    vec2 point = (UV - vec2(0.5)) * 2.0;
    float radius = length(point);
    float angle = atan(point.y, point.x);
    float angular_uv = angle / TAU + 0.5;

    // Preserve the asymmetric falloff idea from the source line shader, but
    // wrap it around the circumference instead of a horizontal strip.
    float dist_left = clamp(0.5 - angular_uv, 0.0, 0.5) * 2.0;
    float dist_right = clamp(angular_uv - 0.5, 0.0, 0.5) * 2.0;
    float falloff_left = pow(1.0 - dist_left, left_falloff);
    float falloff_right = pow(1.0 - dist_right, right_falloff);
    float asymmetric_falloff = angular_uv <= 0.5 ? falloff_left : falloff_right;
    float falloff = mix(0.42, 1.0, asymmetric_falloff);

    float progress = clamp(reaction_progress, 0.0, 1.0);
    float fade = 1.0;
    float radial_shift = 0.0;
    float amplitude_boost = 0.0;
    float frequency_multiplier = 1.0;
    float asymmetry = 0.0;
    float echo_strength = 0.0;
    float echo_offset = 0.0;

    if (reaction_mode == 1) {
        // PERFECT: broad expansion with a bright secondary ripple.
        radial_shift = sin(progress * PI) * 0.19;
        amplitude_boost = (1.0 - progress) * 0.075;
        frequency_multiplier = 1.7;
        echo_strength = sin(progress * PI) * 0.8;
        echo_offset = -0.11 - progress * 0.08;
    } else if (reaction_mode == 2) {
        // GOOD: controlled, rounded pulse.
        radial_shift = sin(progress * PI) * 0.115;
        amplitude_boost = (1.0 - progress) * 0.045;
        frequency_multiplier = 1.25;
        echo_strength = sin(progress * PI) * 0.35;
        echo_offset = -0.08;
    } else if (reaction_mode == 3) {
        // OK: smaller jelly wobble with visible asymmetry.
        radial_shift = sin(progress * PI) * 0.055;
        amplitude_boost = (1.0 - progress) * 0.06;
        frequency_multiplier = 0.8;
        asymmetry = sin(angle - progress * TAU) * 0.045 * (1.0 - progress);
    } else if (reaction_mode == 4) {
        // PARRY: fast inward snap followed by a strong outward shockwave.
        float snap = -0.12 * gaussian(progress, 0.1, 0.085);
        float burst_progress = smoothstep(0.1, 1.0, progress);
        radial_shift = snap + sin(burst_progress * PI) * 0.27;
        amplitude_boost = (1.0 - progress) * 0.095;
        frequency_multiplier = 2.4;
        echo_strength = sin(burst_progress * PI) * 1.0;
        echo_offset = -0.15 - burst_progress * 0.12;
    } else if (reaction_mode == 5) {
        // MISS: uneven collapse and fragmentation.
        radial_shift = -smoothstep(0.0, 1.0, progress) * 0.34;
        amplitude_boost = (1.0 - progress) * 0.11;
        frequency_multiplier = 0.65;
        asymmetry = sin(angle * 2.0 + progress * 5.0) * 0.09 * progress;
    }

    if (reaction_mode != 0) {
        fade = 1.0 - smoothstep(0.68, 1.0, progress);
    }

    float frequency = wave_frequency * frequency_multiplier;
    float organic_wave = sin(angle * frequency + TIME * wave_speed);
    organic_wave += sin(
        angle * (frequency * 0.47 + 1.0) - TIME * wave_speed * 0.63 + 1.7
    ) * 0.42;

    float safe_effect_scale = max(effect_scale, 0.01);
    float amplitude = (wave_amplitude + amplitude_boost) * falloff;
    float ring_radius = (
        base_radius + radial_shift + asymmetry + organic_wave * amplitude
    ) * safe_effect_scale;
    float distance_to_ring = abs(radius - ring_radius);

    float scaled_thickness = thickness * safe_effect_scale;
    float scaled_glow_width = glow_width * safe_effect_scale;
    float core = 1.0 - smoothstep(
        scaled_thickness,
        scaled_thickness + 0.012 * safe_effect_scale,
        distance_to_ring
    );
    float glow = 1.0 - smoothstep(
        scaled_thickness,
        scaled_glow_width,
        distance_to_ring
    );

    float echo = 0.0;
    if (echo_strength > 0.0) {
        float echo_distance = abs(
            radius - (ring_radius + echo_offset * safe_effect_scale)
        );
        echo = (1.0 - smoothstep(
            scaled_thickness * 0.75,
            scaled_glow_width * 0.82,
            echo_distance
        )) * echo_strength;
    }

    float psychedelic_phase = 0.5 + 0.5 * sin(
        angle * 2.0 - TIME * 0.55 + organic_wave * 0.8
    );
    vec3 color = mix(primary_color.rgb, secondary_color.rgb, psychedelic_phase * 0.72);
    color += secondary_color.rgb * echo * 0.36;

    float alpha = max(core, glow * 0.46) + echo * 0.48;
    if (reaction_mode == 5) {
        float fragments = smoothstep(
            -0.2,
            0.55,
            sin(angle * 9.0 + progress * 15.0)
        );
        alpha *= mix(1.0, fragments, progress * 0.9);
    }

    alpha = clamp(alpha * opacity * fade, 0.0, 1.0);
    COLOR = vec4(color, alpha * mix(primary_color.a, secondary_color.a, 0.5));
}

shader_type canvas_item;
render_mode unshaded;

// Adaptado para Godot 4 e sincronizado ao relógio musical do projeto a partir de
// "2D Nebula Shader" (CC0):
// https://godotshaders.com/shader/2d-nebula-shader/

uniform float rhythm_time = 0.0;
uniform float beat_phase : hint_range(0.0, 1.0) = 0.0;
uniform float beat_pulse : hint_range(0.0, 1.0) = 0.0;
uniform float measure_pulse : hint_range(0.0, 1.0) = 0.0;

uniform int octave_count : hint_range(3, 8, 1) = 6;
uniform vec4 cloud_1_color : source_color = vec4(0.24, 0.48, 0.88, 0.42);
uniform vec4 cloud_2_color : source_color = vec4(0.98, 0.62, 0.30, 0.14);
uniform vec4 cloud_3_color : source_color = vec4(0.69, 0.18, 0.72, 0.88);
uniform vec4 cloud_4_color : source_color = vec4(0.12, 0.035, 0.28, 1.0);
uniform vec4 space_color : source_color = vec4(0.018, 0.008, 0.075, 1.0);
uniform vec4 star_color : source_color = vec4(0.86, 0.90, 1.0, 1.0);
uniform vec4 beat_glow_color : source_color = vec4(0.72, 0.30, 1.0, 1.0);

uniform float zoom_scale : hint_range(1.0, 10.0, 0.1) = 4.6;
uniform float cloud_motion : hint_range(0.0, 0.2, 0.001) = 0.045;
uniform float star_scale : hint_range(5.0, 100.0, 1.0) = 38.0;
uniform float star_probability : hint_range(0.9, 0.999, 0.001) = 0.982;
uniform float star_strength : hint_range(0.0, 2.0, 0.01) = 0.72;
uniform float aspect_ratio : hint_range(0.5, 3.0, 0.001) = 1.777778;
uniform vec2 center_offset = vec2(0.0);
uniform float exposure : hint_range(0.2, 2.0, 0.01) = 0.88;
uniform float vignette_strength : hint_range(0.0, 1.0, 0.01) = 0.35;

float random_value(vec2 input_value) {
    return fract(sin(dot(input_value, vec2(23.53, 44.0))) * 42350.45);
}

float perlin_noise(vec2 input_value) {
    vec2 cell = floor(input_value);
    vec2 local = fract(input_value);
    vec2 interpolation = smoothstep(vec2(0.0), vec2(1.0), local);

    float a = random_value(cell);
    float b = random_value(cell + vec2(1.0, 0.0));
    float c = random_value(cell + vec2(0.0, 1.0));
    float d = random_value(cell + vec2(1.0, 1.0));
    return mix(
        mix(a, b, interpolation.x),
        mix(c, d, interpolation.x),
        interpolation.y
    );
}

float cloud_noise(vec2 input_value, float minimum, bool soft_edge) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int index = 0; index < 8; index++) {
        if (index >= octave_count) {
            break;
        }
        value += perlin_noise(input_value) * amplitude;
        input_value *= 2.0;
        amplitude *= 0.5;
    }

    float normalized = (
        smoothstep(minimum, 1.0, value) - minimum
    ) / max(1.0 - minimum, 0.001);
    return soft_edge
        ? smoothstep(0.0, 1.0, normalized)
        : clamp(normalized, 0.0, 1.0);
}

float star_layer(vec2 uv, float scale, float probability, float seed) {
    vec2 grid_uv = uv * scale;
    vec2 cell = floor(grid_uv);
    vec2 local = fract(grid_uv) - vec2(0.5);
    float star_seed = random_value(cell + vec2(seed));
    if (star_seed <= probability) {
        return 0.0;
    }

    float distance_to_center = length(local);
    float core = smoothstep(0.16, 0.0, distance_to_center);
    float rays = smoothstep(
        0.055,
        0.0,
        min(abs(local.x), abs(local.y))
    ) * smoothstep(0.42, 0.02, distance_to_center);
    float twinkle = 0.72 + 0.28 * sin(
        rhythm_time * 6.283185 + star_seed * 35.0
    );
    return max(core, rays * 0.48) * twinkle;
}

void fragment() {
    // UV centralizado e corrigido para 16:9: o foco permanece no meio da tela
    // em janela e fullscreen.
    vec2 centered_uv = UV - vec2(0.5);
    centered_uv.x *= aspect_ratio;
    centered_uv += center_offset;
    float beat_breath = sin(beat_phase * 3.141593);
    vec2 nebula_uv = centered_uv * zoom_scale *
        (1.0 - beat_breath * 0.006) + vec2(0.5);

    float motion_time = rhythm_time * cloud_motion;
    float slow_wave = sin(rhythm_time * 0.34) * 0.035;
    float measure_wave = cos(rhythm_time * 0.17) * 0.04;
    vec2 drift_1 = vec2(
        sin(motion_time * 1.7),
        cos(motion_time * 1.3)
    ) * 0.18;
    vec2 drift_2 = vec2(
        cos(motion_time * 1.1),
        sin(motion_time * 1.5)
    ) * 0.16;

    float cloud_1 = cloud_noise(
        nebula_uv + drift_1,
        0.24 + slow_wave,
        false
    );
    float cloud_2 = cloud_noise(
        nebula_uv * 0.9 - drift_2 + vec2(2.3),
        0.34 - slow_wave,
        true
    );
    float cloud_3 = cloud_noise(
        nebula_uv * 0.76 + drift_2 + vec2(4.8),
        0.27 + measure_wave,
        true
    );
    float cloud_4 = cloud_noise(
        nebula_uv * 0.61 - drift_1 + vec2(7.2),
        0.42 - measure_wave,
        true
    );

    vec3 color = space_color.rgb;
    color += cloud_1_color.rgb * cloud_1 * cloud_1_color.a;
    color += cloud_2_color.rgb * cloud_2 * cloud_2_color.a;
    color = mix(color, cloud_3_color.rgb, cloud_3 * cloud_3_color.a);
    color = mix(color, cloud_4_color.rgb, cloud_4 * cloud_4_color.a);

    float star_1 = star_layer(
        centered_uv + drift_1 * 0.08,
        star_scale,
        star_probability,
        13.0
    );
    float star_2 = star_layer(
        centered_uv - drift_2 * 0.05,
        star_scale * 1.85,
        min(star_probability + 0.009, 0.997),
        47.0
    );
    float beat_star_glow = 1.0 + beat_pulse * 2.2 + measure_pulse * 2.8;
    color += star_color.rgb * (star_1 + star_2 * 0.55) *
        star_strength * beat_star_glow;

    // Pulso musical discreto: amplia a luz da nebulosa sem piscar a tela inteira.
    float central_glow = exp(-dot(centered_uv, centered_uv) * 1.75);
    color += beat_glow_color.rgb * central_glow *
        (beat_pulse * 0.34 + measure_pulse * 0.55);
    color *= 1.0 + beat_pulse * 0.10 + measure_pulse * 0.12;
    color *= exposure;

    vec2 vignette_uv = UV * (vec2(1.0) - UV.yx);
    float vignette = pow(
        clamp(16.0 * vignette_uv.x * vignette_uv.y, 0.0, 1.0),
        0.22
    );
    color *= mix(1.0, 0.68 + 0.32 * vignette, vignette_strength);
    COLOR = vec4(color, 1.0);
}

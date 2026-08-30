shader_type canvas_item;
render_mode unshaded;

// Adaptação 2D do conceito de "Cartoon Sky Shader" para o ColorRect do
// combate. O original usa shader_type sky e WorldEnvironment; este mantém a
// estrutura visual configurada na cena e recebe apenas o relógio musical do C#.

uniform float rhythm_time = 0.0;
uniform float beat_phase : hint_range(0.0, 1.0) = 0.0;
uniform float beat_pulse : hint_range(0.0, 1.0) = 0.0;
uniform float measure_pulse : hint_range(0.0, 1.0) = 0.0;

uniform vec4 sky_color : source_color = vec4(0.035, 0.018, 0.16, 1.0);
uniform vec4 horizon_color : source_color = vec4(0.34, 0.10, 0.48, 1.0);
uniform float horizon_position : hint_range(0.0, 1.0, 0.01) = 0.38;
uniform float horizon_blur : hint_range(0.01, 0.5, 0.01) = 0.18;

uniform vec4 sun_color : source_color = vec4(1.0, 0.63, 0.90, 1.0);
uniform vec2 sun_position = vec2(0.72, 0.27);
uniform float sun_size : hint_range(0.01, 0.5, 0.01) = 0.105;
uniform float sun_blur : hint_range(0.001, 0.2, 0.001) = 0.018;
uniform float sun_glow : hint_range(0.0, 2.0, 0.01) = 0.48;

uniform vec4 cloud_color : source_color = vec4(0.83, 0.55, 0.94, 1.0);
uniform vec4 cloud_light_color : source_color = vec4(1.0, 0.77, 0.98, 1.0);
uniform vec4 cloud_shadow_color : source_color = vec4(0.16, 0.035, 0.30, 1.0);
uniform float cloud_edge_sharpness : hint_range(0.01, 0.5, 0.01) = 0.09;
uniform float cloud_scale : hint_range(0.1, 10.0, 0.1) = 2.45;
uniform float cloud_speed : hint_range(0.0, 0.5, 0.001) = 0.027;
uniform float cloud_threshold : hint_range(0.0, 1.0, 0.01) = 0.51;

uniform float aspect_ratio : hint_range(0.5, 3.0, 0.001) = 1.777778;
uniform float exposure : hint_range(0.2, 2.0, 0.01) = 0.92;
uniform float vignette_strength : hint_range(0.0, 1.0, 0.01) = 0.22;

float hash_value(vec2 point) {
    return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453123);
}

float value_noise(vec2 point) {
    vec2 cell = floor(point);
    vec2 local = fract(point);
    vec2 interpolation = local * local * local *
        (local * (local * 6.0 - 15.0) + 10.0);

    float a = hash_value(cell);
    float b = hash_value(cell + vec2(1.0, 0.0));
    float c = hash_value(cell + vec2(0.0, 1.0));
    float d = hash_value(cell + vec2(1.0, 1.0));
    return mix(
        mix(a, b, interpolation.x),
        mix(c, d, interpolation.x),
        interpolation.y
    );
}

float fbm(vec2 point) {
    float value = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 4; octave++) {
        value += amplitude * value_noise(point);
        point = point * 2.03 + vec2(13.7, 9.2);
        amplitude *= 0.5;
    }
    return value;
}

float cloud_shape(vec2 point, float threshold_offset) {
    float cloud_noise = fbm(point);
    float threshold = cloud_threshold + threshold_offset;
    return smoothstep(
        threshold,
        threshold + cloud_edge_sharpness,
        cloud_noise
    );
}

void fragment() {
    float sky_height = 1.0 - UV.y;
    float horizon_mask = smoothstep(
        horizon_position - horizon_blur,
        horizon_position + horizon_blur,
        sky_height
    );
    vec3 color = mix(horizon_color.rgb, sky_color.rgb, horizon_mask);

    // Disco solar cartoon com halo suave e respiração musical discreta.
    vec2 sun_delta = UV - sun_position;
    sun_delta.x *= aspect_ratio;
    float sun_distance = length(sun_delta);
    float animated_sun_size = sun_size *
        (1.0 + beat_pulse * 0.07 + measure_pulse * 0.11);
    float sun_disc = 1.0 - smoothstep(
        animated_sun_size - sun_blur,
        animated_sun_size + sun_blur,
        sun_distance
    );
    float sun_halo = exp(
        -sun_distance * sun_distance /
        max(animated_sun_size * animated_sun_size * 3.8, 0.0001)
    );
    color += sun_color.rgb * sun_halo * sun_glow * 0.45;
    color = mix(color, sun_color.rgb, sun_disc);

    // Duas camadas de nuvens com formas grandes. A camada de sombra cria o
    // recorte de desenho animado sem adicionar informação visual excessiva.
    float motion = rhythm_time * cloud_speed;
    float beat_breath = sin(beat_phase * 3.141593) * 0.025;
    vec2 cloud_uv = vec2(UV.x * aspect_ratio, UV.y) * cloud_scale;

    vec2 lower_uv = cloud_uv + vec2(motion, 1.35 + beat_breath);
    float lower_shadow = cloud_shape(lower_uv + vec2(0.06, 0.10), -0.035);
    float lower_cloud = cloud_shape(lower_uv, 0.0);
    float lower_band = smoothstep(0.18, 0.43, UV.y);
    lower_cloud *= lower_band;
    lower_shadow *= lower_band;

    vec2 upper_uv = cloud_uv * vec2(0.82, 1.08) +
        vec2(-motion * 0.63 + 5.7, 0.28 - beat_breath * 0.7);
    float upper_shadow = cloud_shape(upper_uv + vec2(0.05, 0.08), 0.018);
    float upper_cloud = cloud_shape(upper_uv, 0.052);
    float upper_band = 1.0 - smoothstep(0.55, 0.86, UV.y);
    upper_cloud *= upper_band * 0.72;
    upper_shadow *= upper_band * 0.72;

    color = mix(color, cloud_shadow_color.rgb, max(lower_shadow, upper_shadow));
    color = mix(color, cloud_color.rgb, max(lower_cloud, upper_cloud));

    float cloud_highlight = max(
        lower_cloud - cloud_shape(lower_uv + vec2(-0.055, -0.07), 0.025),
        upper_cloud - cloud_shape(upper_uv + vec2(-0.045, -0.06), 0.075)
    );
    color = mix(
        color,
        cloud_light_color.rgb,
        clamp(cloud_highlight * 2.4 + measure_pulse * lower_cloud * 0.16, 0.0, 1.0)
    );

    // O pulso clareia as cores, mas não pisca a tela nem interfere na leitura.
    color *= 1.0 + beat_pulse * 0.09 + measure_pulse * 0.12;
    color *= exposure;

    vec2 vignette_uv = UV * (vec2(1.0) - UV.yx);
    float vignette = pow(
        clamp(16.0 * vignette_uv.x * vignette_uv.y, 0.0, 1.0),
        0.24
    );
    color *= mix(1.0, 0.70 + 0.30 * vignette, vignette_strength);
    COLOR = vec4(color, 1.0);
}

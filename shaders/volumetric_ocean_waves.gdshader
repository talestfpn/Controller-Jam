shader_type canvas_item;
render_mode unshaded;

// Adapted for this project from "Volumetric Ocean Waves" (MIT):
// https://godotshaders.com/shader/volumetric-ocean-waves/
// Original shader credited by the adaptation page:
// https://www.shadertoy.com/view/tdlXDM

uniform float time_speed : hint_range(0.0, 3.0) = 0.32;
uniform bool flip_y = false;

uniform vec4 sea_color_dark : source_color = vec4(0.018, 0.006, 0.15, 1.0);
uniform vec4 sea_color_light : source_color = vec4(0.22, 0.035, 0.72, 1.0);
uniform vec4 refraction_color : source_color = vec4(1.0, 0.42, 0.92, 1.0);
uniform vec4 light_shaft_color : source_color = vec4(0.65, 0.3, 1.0, 1.0);

// UV-based framing keeps the focal point centered inside the Background node,
// independently from window position and fullscreen scaling.
uniform float aspect_ratio : hint_range(0.5, 3.0) = 1.7777778;
uniform vec2 center_offset = vec2(0.0, 0.0);
uniform float horizontal_scale : hint_range(0.1, 1.2) = 0.45;
uniform float vertical_scale : hint_range(0.1, 1.2) = 0.5;
uniform float light_focus_x : hint_range(-1.0, 1.0) = 0.0;
uniform float light_shaft_strength : hint_range(0.0, 3.0) = 1.65;
uniform float horizon_glow_strength : hint_range(0.0, 1.5) = 0.38;
uniform float horizon_height : hint_range(-0.5, 0.5) = 0.15;
uniform float exposure : hint_range(0.2, 2.0) = 0.92;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.42;

struct RaymarchResult {
    vec3 hit_pos;
    float hit_t;
    float dist;
};

float hash(vec2 point) {
    return 0.5 * (
        sin(dot(point, vec2(271.319, 413.975)) +
        1217.13 * point.x * point.y)
    ) + 0.5;
}

float noise(vec2 point) {
    vec2 weight = fract(point);
    weight = weight * weight * (3.0 - 2.0 * weight);
    point = floor(point);
    return mix(
        mix(hash(point), hash(point + vec2(1.0, 0.0)), weight.x),
        mix(
            hash(point + vec2(0.0, 1.0)),
            hash(point + vec2(1.0, 1.0)),
            weight.x
        ),
        weight.y
    );
}

float map_octave(vec2 uv) {
    uv = (uv + noise(uv)) / 2.5;
    uv = vec2(
        uv.x * 0.6 - uv.y * 0.8,
        uv.x * 0.8 + uv.y * 0.6
    );
    vec2 uv_sin = 1.0 - abs(sin(uv));
    vec2 uv_cos = abs(cos(uv));
    uv = mix(uv_sin, uv_cos, uv_sin);
    return 1.0 - pow(uv.x * uv.y, 0.65);
}

float map(vec3 point) {
    vec2 uv = point.xz + (TIME * time_speed) / 2.0;
    float amplitude = 0.6;
    float frequency = 2.0;
    float value = 0.0;
    for (int index = 0; index < 3; ++index) {
        value += map_octave(uv) * amplitude;
        amplitude *= 0.3;
        uv *= frequency;
    }

    uv = point.xz - 1000.0 - (TIME * time_speed) / 2.0;
    amplitude = 0.6;
    frequency = 2.0;
    for (int index = 0; index < 3; ++index) {
        value += map_octave(uv) * amplitude;
        amplitude *= 0.3;
        uv *= frequency;
    }
    return value + 3.0 - point.y;
}

vec3 get_normal(vec3 point, float epsilon) {
    float center = map(point);
    float delta_x = map(point + vec3(epsilon, 0.0, 0.0)) - center;
    float delta_z = map(point + vec3(0.0, 0.0, epsilon)) - center;
    return normalize(vec3(delta_x, epsilon, delta_z));
}

RaymarchResult raymarch(vec3 ray_origin, vec3 ray_direction, float epsilon) {
    RaymarchResult result;
    float left = 0.0;
    float right = 26.0;
    float distance_to_surface = 1000000.0;
    for (int index = 0; index < 16; ++index) {
        float middle = (right + left) / 2.0;
        float mapped_middle = map(ray_origin + ray_direction * middle);
        distance_to_surface = min(distance_to_surface, abs(mapped_middle));
        if (mapped_middle > 0.0) {
            left = middle;
        } else {
            right = middle;
        }
        if (right - left < epsilon) {
            break;
        }
    }

    result.hit_pos = ray_origin + ray_direction * left;
    result.hit_t = left;
    result.dist = distance_to_surface;
    return result;
}

float fbm(vec2 point) {
    float total = 0.0;
    float amplitude = 1.0;
    for (int index = 0; index < 5; ++index) {
        total += noise(point) * amplitude;
        point += point;
        amplitude *= 0.4;
    }
    return total;
}

float light_shafts(vec2 uv) {
    const float angle = -0.2;
    vec2 original_uv = uv;
    float animation_time = (TIME * time_speed) / 16.0;
    uv = vec2(
        uv.x * cos(angle) - uv.y * sin(angle),
        uv.x * sin(angle) + uv.y * cos(angle)
    );
    float value = fbm(vec2(
        uv.x * 2.0 + 200.0 + animation_time,
        uv.y / 4.0
    ));
    value += fbm(vec2(
        uv.x * 2.0 + 200.0 - animation_time,
        uv.y / 4.0
    ));
    value /= 3.0;

    float vertical_mask = pow(
        clamp(1.0 - abs(original_uv.y - 0.15), 0.0, 1.0) * 0.49 + 0.5,
        2.0
    );
    float horizontal_mask =
        clamp(1.0 - abs(original_uv.x - light_focus_x), 0.0, 1.0) *
        0.49 + 0.5;
    return pow(value * vertical_mask * horizontal_mask, 2.0);
}

vec2 bubble(vec2 uv, float scale) {
    if (uv.y > 0.2) {
        return vec2(0.0);
    }

    float animation_time = (TIME * time_speed) / 4.0;
    vec2 cell_uv = uv * scale;
    vec2 cell = floor(cell_uv);
    vec2 bias = vec2(0.0, 4.0 * sin(cell.x * 128.0 + animation_time));
    float mask = smoothstep(
        0.1,
        0.2,
        -cos(cell.x * 128.0 + animation_time)
    );
    cell_uv += bias;
    vec2 animated_cell = floor(cell_uv);
    cell_uv = fract(cell_uv);
    float size = noise(animated_cell) * 0.07 + 0.01;
    vec2 position = vec2(
        noise(vec2(animation_time, animated_cell.y * 64.1)) * 0.8 + 0.1,
        0.5
    );
    if (length(cell_uv - position) < size) {
        return (cell_uv + position) * vec2(0.1, 0.2) * mask;
    }
    return vec2(0.0);
}

void fragment() {
    float epsilon = max(SCREEN_PIXEL_SIZE.x, SCREEN_PIXEL_SIZE.y);
    vec2 uv = (UV - vec2(0.5)) * 2.0;
    uv.x *= aspect_ratio * horizontal_scale;
    uv.y *= vertical_scale;
    uv += center_offset;
    if (flip_y) {
        uv.y *= -1.0;
    }
    vec2 framing_uv = uv;
    uv += bubble(uv, 12.0) + bubble(uv, 24.0);

    vec3 ray_origin = vec3(0.0, 0.0, 2.0);
    vec3 ray_direction = normalize(vec3(uv, -1.0));
    vec3 light_position = vec3(light_focus_x, 3.0, -3.0);

    RaymarchResult result = raymarch(
        ray_origin,
        ray_direction,
        epsilon
    );
    vec3 surface_normal = get_normal(result.hit_pos, epsilon);
    float diffuse = dot(surface_normal, ray_direction) * 0.5 + 0.5;
    vec3 color = mix(sea_color_dark.rgb, sea_color_light.rgb, diffuse);
    color += vec3(pow(diffuse, 12.0));

    vec3 refraction_direction = normalize(refract(
        result.hit_pos - light_position,
        surface_normal,
        0.05
    ));
    float refraction = clamp(
        dot(refraction_direction, ray_direction),
        0.0,
        1.0
    );
    color += refraction_color.rgb * 0.6 * pow(refraction, 1.5);

    float surface_distance = clamp(result.dist, 0.0, 1.0);
    vec3 final_color = mix(
        color,
        sea_color_dark.rgb,
        pow(surface_distance, 0.2)
    );
    final_color += light_shaft_color.rgb *
        light_shafts(uv) * light_shaft_strength;
    float centered_horizon = exp(
        -pow((framing_uv.x - light_focus_x) * 1.65, 2.0)
    );
    float horizon_band = exp(
        -abs(framing_uv.y - horizon_height) * 12.0
    );
    vec3 horizon_color = mix(
        light_shaft_color.rgb,
        refraction_color.rgb,
        0.58
    );
    final_color += horizon_color * centered_horizon * horizon_band *
        horizon_glow_strength;
    final_color = (
        final_color * final_color + sin(final_color)
    ) / vec3(1.8, 1.8, 1.9);
    final_color *= exposure;

    vec2 vignette_uv = UV * (1.0 - UV.yx);
    float vignette = pow(
        clamp(16.0 * vignette_uv.x * vignette_uv.y, 0.0, 1.0),
        0.2
    );
    final_color *= mix(1.0, 0.7 + 0.3 * vignette, vignette_strength);
    COLOR = vec4(final_color, 1.0);
}

shader_type canvas_item;

// Transição por máscara: o C# apenas anima `factor`; a cena mantém o
// material, o gradiente e o padrão configurados no Inspector.
uniform vec4 base_color : source_color = vec4(0.025, 0.008, 0.065, 1.0);
uniform vec2 node_resolution = vec2(1152.0, 648.0);

uniform float factor : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform float width : hint_range(0.05, 1.0, 0.01) = 0.32;

group_uniforms gradient;
uniform sampler2D gradient_texture;
uniform bool gradient_fixed = false;

group_uniforms shape;
uniform sampler2D shape_texture;
uniform float shape_tiling = 3.0;
uniform float shape_rotation = -8.0;
uniform vec2 shape_scroll = vec2(0.018, -0.012);
uniform float shape_feathering : hint_range(0.0, 1.0, 0.01) = 0.08;
uniform float shape_treshold : hint_range(0.0, 2.0, 0.01) = 0.72;

float read_gradient(vec2 uv, vec2 fixed_uv, bool fixed)
{
	return fixed
		? texture(gradient_texture, fixed_uv).r
		: texture(gradient_texture, uv).r;
}

vec2 rotate_uv(vec2 uv, vec2 pivot, float angle)
{
	float s = sin(angle);
	float c = cos(angle);
	mat2 rotation = mat2(vec2(c, -s), vec2(s, c));
	return (uv - pivot) * rotation + pivot;
}

void fragment()
{
	float safe_width = max(width, 0.001);
	float progress = mix(-safe_width, 1.0, clamp(factor, 0.0, 1.0));
	float aspect = node_resolution.y / max(node_resolution.x, 1.0);
	vec2 aspect_uv = ((UV - vec2(0.0, 0.5)) * vec2(1.0, aspect)) +
		vec2(0.0, 0.5);

	float gradient_value = read_gradient(UV, aspect_uv, gradient_fixed);
	float value = clamp((gradient_value - progress) / safe_width, 0.0, 1.0);

	vec2 tiled_uv = rotate_uv(
		mod((aspect_uv + vec2(TIME) * shape_scroll) * shape_tiling, 1.0),
		vec2(0.5),
		radians(shape_rotation));
	float shape_value = 1.0 - texture(shape_texture, tiled_uv).r;

	float feather = shape_feathering * 0.5;
	shape_value = mix(feather, 1.0 - feather, shape_value);
	float shaped_gradient = smoothstep(
		value - feather,
		value + feather,
		shape_treshold - shape_value);

	// Garante que factor=0 seja realmente transparente, mesmo com uma
	// textura de padrão que possua valores claros no primeiro frame.
	float transition_alpha = smoothstep(0.0, 0.035, clamp(factor, 0.0, 1.0));
	COLOR = vec4(base_color.rgb, base_color.a * shaped_gradient * transition_alpha);
}

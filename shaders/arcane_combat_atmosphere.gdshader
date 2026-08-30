shader_type canvas_item;

uniform vec2 enemy_position = vec2(0.195, 0.447);
uniform vec2 prompt_position = vec2(0.5, 0.509);
uniform vec2 combo_position = vec2(0.842, 0.488);
uniform float aspect_ratio = 1.777778;
uniform float beat_phase : hint_range(0.0, 1.0) = 0.0;
uniform float health_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float resistance_ratio : hint_range(0.0, 1.0) = 1.0;
uniform float combo_energy : hint_range(0.0, 1.0) = 0.0;
uniform float combo_tier : hint_range(0.0, 3.0) = 0.0;
uniform float enemy_seed = 0.0;
uniform float ornament_style = 0.0;
uniform float telegraph_strength : hint_range(0.0, 1.0) = 0.0;
uniform float telegraph_progress : hint_range(0.0, 1.0) = 0.0;
uniform float hit_progress : hint_range(-1.0, 1.2) = -1.0;
uniform float hit_strength : hint_range(0.0, 1.0) = 0.0;
uniform float stagger_flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 enemy_primary : source_color = vec4(0.75, 0.25, 1.0, 1.0);
uniform vec4 enemy_secondary : source_color = vec4(1.0, 0.72, 0.2, 1.0);
uniform vec4 impact_color : source_color = vec4(0.55, 1.0, 0.24, 1.0);
uniform vec4 combo_primary : source_color = vec4(0.32, 0.72, 1.0, 1.0);
uniform vec4 combo_secondary : source_color = vec4(0.8, 0.24, 1.0, 1.0);

float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

vec2 metric(vec2 point) {
	return point * vec2(aspect_ratio, 1.0);
}

vec2 curve_point(vec2 start, vec2 end, float progress, float bend) {
	vec2 point = mix(start, end, progress);
	point.y += sin(progress * 3.14159265) * bend;
	return point;
}

float curve_distance(vec2 point, vec2 start, vec2 end, float bend) {
	float distance_to_curve = 10.0;
	for (int index = 0; index < 28; index++) {
		float progress = float(index) / 27.0;
		vec2 sample_point = curve_point(start, end, progress, bend);
		distance_to_curve = min(
			distance_to_curve,
			length(metric(point - sample_point)));
	}
	return distance_to_curve;
}

float soft_circle(vec2 point, vec2 center, float radius, float feather) {
	return 1.0 - smoothstep(radius, radius + feather, length(metric(point - center)));
}

void fragment() {
	vec2 uv = UV;
	float beat = pow(max(0.0, 1.0 - beat_phase), 5.0);
	float danger = 1.0 - resistance_ratio;
	float wounded = 1.0 - health_ratio;
	vec3 color = vec3(0.0);
	float alpha = 0.0;

	// Aura elíptica da carta: reage ao beat, à vida e à Resistência.
	vec2 enemy_field = (uv - enemy_position) / vec2(0.185, 0.34);
	float enemy_radius = length(enemy_field);
	float enemy_angle = atan(enemy_field.y, enemy_field.x);
	float aura_wave = sin(enemy_angle * (5.0 + ornament_style) - TIME * 0.9 + enemy_seed * 9.0);
	float aura_ring = 1.0 - smoothstep(
		0.025,
		0.075,
		abs(enemy_radius - (1.0 + aura_wave * 0.035)));
	float inner_aura = 1.0 - smoothstep(0.12, 1.12, enemy_radius);
	float enemy_energy = 0.11 + beat * 0.1 + danger * 0.25 + wounded * 0.08;
	vec3 enemy_neon = mix(enemy_primary.rgb, enemy_secondary.rgb, 0.5 + aura_wave * 0.32);
	color += enemy_neon * (inner_aura * enemy_energy * 0.32 + aura_ring * enemy_energy);
	alpha += inner_aura * enemy_energy * 0.16 + aura_ring * enemy_energy * 0.42;

	// Ornamento próprio de cada Arcano: anéis, pontos e glifos mudam pelo seed.
	float spokes = 5.0 + mod(ornament_style, 5.0);
	float angular_glyph = pow(abs(sin(enemy_angle * spokes + enemy_seed * 13.0)), 18.0);
	float glyph_ring = (1.0 - smoothstep(0.018, 0.055, abs(enemy_radius - 1.18))) * angular_glyph;
	float outer_ring = 1.0 - smoothstep(0.016, 0.045, abs(enemy_radius - 1.34));
	outer_ring *= 0.35 + 0.65 * step(0.42, fract(enemy_angle * 2.4 + enemy_seed));
	color += mix(enemy_secondary.rgb, enemy_primary.rgb, angular_glyph) *
		(glyph_ring * 0.42 + outer_ring * 0.18) * (0.45 + danger * 0.45);
	alpha += glyph_ring * 0.28 + outer_ring * 0.1;

	// Trilhas rítmicas permanentes ligando carta, prompt e flow/combo.
	float enemy_path = curve_distance(uv, enemy_position, prompt_position, -0.075);
	float combo_path = curve_distance(uv, prompt_position, combo_position, 0.085);
	float enemy_trace = 1.0 - smoothstep(0.002, 0.008, enemy_path);
	float combo_trace = 1.0 - smoothstep(0.002, 0.009, combo_path);
	float trace_pulse = 0.42 + 0.28 * sin(TIME * 1.4 + beat_phase * 6.28318);
	color += enemy_primary.rgb * enemy_trace * trace_pulse * 0.12;
	color += combo_primary.rgb * combo_trace * (0.08 + combo_energy * 0.24);
	alpha += enemy_trace * 0.055 + combo_trace * (0.045 + combo_energy * 0.08);

	// Runas espaçadas na rota, mantendo contraste baixo para não competir com o timing.
	for (int index = 0; index < 11; index++) {
		float rune_index = float(index);
		float progress = (rune_index + 0.6) / 11.6;
		vec2 rune_position = curve_point(enemy_position, prompt_position, progress, -0.075);
		float rune = soft_circle(uv, rune_position, 0.0032, 0.0045);
		float gate = step(0.34, hash21(vec2(rune_index, floor(enemy_seed * 19.0))));
		color += enemy_secondary.rgb * rune * gate * (0.12 + beat * 0.1);
		alpha += rune * gate * 0.08;
	}

	// Telegraph inimigo -> círculo. O alvo musical não é alterado.
	vec2 telegraph_position = curve_point(
		enemy_position,
		prompt_position,
		telegraph_progress,
		-0.075);
	float telegraph_packet = soft_circle(uv, telegraph_position, 0.012, 0.035);
	float telegraph_tail = enemy_trace * telegraph_strength *
		(0.35 + 0.65 * telegraph_progress);
	color += vec3(1.0, 0.18, 0.3) *
		(telegraph_packet * telegraph_strength * 1.25 + telegraph_tail * 0.42);
	alpha += telegraph_packet * telegraph_strength * 0.6 + telegraph_tail * 0.22;

	// Eco de acerto círculo -> carta, com rastro e explosão no destino.
	float valid_hit = step(0.0, hit_progress) * step(hit_progress, 1.0);
	vec2 hit_position = curve_point(prompt_position, enemy_position, clamp(hit_progress, 0.0, 1.0), 0.075);
	float hit_packet = soft_circle(uv, hit_position, 0.014, 0.045) * valid_hit;
	float arrival = smoothstep(0.72, 1.0, hit_progress) * valid_hit;
	float arrival_ring = 1.0 - smoothstep(
		0.012,
		0.04,
		abs(length(metric(uv - enemy_position)) - (0.025 + arrival * 0.12)));
	color += impact_color.rgb *
		(hit_packet * hit_strength * 1.4 + arrival_ring * arrival * hit_strength * 0.9);
	alpha += hit_packet * hit_strength * 0.62 + arrival_ring * arrival * hit_strength * 0.42;

	// Flow do combo ocupa o quadrante direito com arcos que crescem por tier.
	vec2 combo_field = (uv - combo_position) / vec2(0.18, 0.24);
	float combo_radius = length(combo_field);
	float combo_angle = atan(combo_field.y, combo_field.x);
	float flow_wave = sin(combo_angle * (5.0 + combo_tier) - TIME * (0.8 + combo_energy));
	float flow_ring = 1.0 - smoothstep(
		0.025,
		0.075,
		abs(combo_radius - (0.72 + combo_energy * 0.28 + flow_wave * 0.045)));
	float flow_arc = flow_ring * smoothstep(-0.9, 0.45, cos(combo_angle - 0.25));
	vec3 flow_color = mix(combo_primary.rgb, combo_secondary.rgb, 0.5 + flow_wave * 0.4);
	color += flow_color * flow_arc * (0.16 + combo_energy * 0.9);
	alpha += flow_arc * (0.06 + combo_energy * 0.34);

	// Poeira arcana em primeiro plano, lenta e esparsa.
	vec2 particle_grid = uv * vec2(22.0, 13.0);
	vec2 particle_id = floor(particle_grid);
	float particle_hash = hash21(particle_id + floor(TIME * 0.025));
	vec2 particle_cell = fract(particle_grid) - 0.5;
	particle_cell.x += sin(TIME * 0.22 + particle_hash * 18.0) * 0.18;
	particle_cell.y += cos(TIME * 0.16 + particle_hash * 11.0) * 0.16;
	float mote = (1.0 - smoothstep(0.025, 0.095, length(particle_cell))) *
		step(0.86, particle_hash);
	float mote_twinkle = 0.45 + 0.55 * sin(TIME * (0.7 + particle_hash) + particle_hash * 21.0);
	vec3 mote_color = mix(enemy_secondary.rgb, combo_primary.rgb, particle_hash);
	color += mote_color * mote * mote_twinkle * 0.32;
	alpha += mote * mote_twinkle * 0.16;

	color += mix(enemy_primary.rgb, vec3(1.0), 0.55) * stagger_flash * inner_aura * 0.52;
	alpha += stagger_flash * inner_aura * 0.2;
	COLOR = vec4(color, clamp(alpha, 0.0, 0.62));
}

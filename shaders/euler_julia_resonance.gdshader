shader_type canvas_item;

// =============================================================================
// EULER-JULIA RESONANCE SHADER  v2.1  (Godot 4 / canvas_item)
// Drop onto a ColorRect → Full Rect. Set aspect_ratio = viewport W / H.
// =============================================================================
//
// FIXES vs v1:
//   - PI and TAU removed (built-in in Godot 4 GLSL, can't redeclare)
//   - pan split into pan_x / pan_y (vec2 uniform can't carry hint_range)
//   - 11 themes via color_theme : hint_range(0, 10)
//   - Low-iteration mode (5-15) is visually rich via domain + orbit traps
//
// QUICK START DEFAULTS:
//   color_theme       0        (0-10, see list below)
//   time_scale        0.10     loop period ~62.8 s
//   zoom              1.90
//   c_radius          0.7885   Julia parameter circle radius
//   euler_twist       0.80
//   max_iter          8       try 5-15 for a painterly look
//   pan_x / pan_y     0.0
//   glow_strength     1.40
//   vignette_strength 0.80
//   shimmer_strength  0.40
//   aspect_ratio      1.7778   <- set to viewport_width / viewport_height
// =============================================================================

// --- Color theme -------------------------------------------------------------
uniform int color_theme : hint_range(0, 10) = 0;
// 0  Void Pulse      - deep indigo / electric violet / magenta
// 1  Abyssal Cyan    - near-black navy / electric cyan / ice
// 2  Ember Forge     - charcoal / deep red / molten orange
// 3  Toxic Circuit   - black / acid green / electric lime
// 4  Solar Eclipse   - black / deep amber / white corona
// 5  Neon Noir       - near-black / hot pink / electric blue
// 6  Obsidian Fire   - pure black / smoldering orange / ashy edge
// 7  Cosmic Dust     - deep space blue / gold / rose gold
// 8  Blood Moon      - near-black / deep crimson / rust / bone
// 9  Deep Sea        - void teal / bioluminescent aqua / pale coral
// 10 Inkwell         - pure black and white / fine ink gradients

// --- Animation ---------------------------------------------------------------
uniform float time_scale      : hint_range(0.01, 0.50) = 0.10;
uniform float color_speed     : hint_range(0.00, 2.00) = 0.35;
uniform float phase_offset    : hint_range(0.00, 6.2832) = 0.0;
uniform float color_offset    : hint_range(0.00, 6.2832) = 0.0;

// --- Fractal -----------------------------------------------------------------
uniform float zoom            : hint_range(0.50, 6.00) = 1.90;
uniform float c_radius        : hint_range(0.10, 1.50) = 0.7885;
// 0.40=soft blobs  0.7885=classic  0.90=dendrites  1.10=Cantor dust
uniform float euler_twist     : hint_range(0.00, 5.00) = 0.80;
// 0.0=standard Julia  0.5-1.5=galaxy arms  3+=heavy spirals
uniform int   max_iter        : hint_range(5, 300) = 8;
// 5-15=painterly domain  80-120=classic fractal  200+=deep detail

// --- Pan (split from vec2 to allow hint_range) -------------------------------
uniform float pan_x           : hint_range(-2.0, 2.0) = 0.0;
uniform float pan_y           : hint_range(-2.0, 2.0) = 0.0;

// --- Visuals -----------------------------------------------------------------
uniform float glow_strength      : hint_range(0.0, 3.0) = 1.40;
uniform float vignette_strength  : hint_range(0.0, 2.0) = 0.80;
uniform float shimmer_strength   : hint_range(0.0, 1.0) = 0.40;
uniform float aspect_ratio       : hint_range(0.50, 3.00) = 1.7778;
// 16:9=1.7778  4:3=1.3333  21:9=2.3333  1:1=1.0

// =============================================================================
// COMPLEX HELPERS
// =============================================================================

vec2 csq(vec2 z) {
    return vec2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y);
}

// Euler rotation: multiply z by e^(i*theta)
vec2 euler_rot(vec2 z, float theta) {
    float s = sin(theta);
    float c = cos(theta);
    return vec2(z.x * c - z.y * s, z.x * s + z.y * c);
}

// =============================================================================
// COSINE PALETTE  (Inigo Quilez technique)
// pal(t) = a + b*cos(TAU*(c*t+d))
// Period = 1.0, so color cycling never has discontinuities.
// =============================================================================
vec3 pal(float t, vec3 a, vec3 b, vec3 c, vec3 d) {
    return a + b * cos(TAU * (c * t + d));
}

// =============================================================================
// 11 THEME PALETTES
// Three palettes per theme: pA (escape), pB (trap), pC (stripe/domain)
// All dark themes keep 'a' values near black. Theme 10 outputs greyscale.
// =============================================================================
void get_palettes(int theme, float ta, float tb, float tc,
                  out vec3 pA, out vec3 pB, out vec3 pC) {

    if (theme == 0) {
        // Void Pulse - indigo core, violet flash, magenta edge
        pA = pal(ta, vec3(0.02, 0.00, 0.05), vec3(0.45, 0.10, 0.60), vec3(1.00, 0.70, 0.40), vec3(0.00, 0.15, 0.20));
        pB = pal(tb, vec3(0.00, 0.00, 0.08), vec3(0.20, 0.05, 0.55), vec3(2.00, 1.00, 0.50), vec3(0.50, 0.20, 0.25));
        pC = pal(tc, vec3(0.04, 0.00, 0.06), vec3(0.55, 0.00, 0.45), vec3(1.00, 1.20, 0.50), vec3(0.80, 0.90, 0.30));

    } else if (theme == 1) {
        // Abyssal Cyan - near-black navy, electric cyan, icy white
        pA = pal(ta, vec3(0.00, 0.02, 0.06), vec3(0.05, 0.40, 0.55), vec3(1.00, 0.60, 0.30), vec3(0.00, 0.10, 0.20));
        pB = pal(tb, vec3(0.00, 0.03, 0.05), vec3(0.00, 0.55, 0.50), vec3(1.50, 1.00, 0.50), vec3(0.30, 0.10, 0.50));
        pC = pal(tc, vec3(0.00, 0.04, 0.06), vec3(0.05, 0.50, 0.60), vec3(1.00, 0.80, 0.50), vec3(0.60, 0.20, 0.10));

    } else if (theme == 2) {
        // Ember Forge - charcoal, deep red, molten orange
        pA = pal(ta, vec3(0.04, 0.01, 0.00), vec3(0.50, 0.15, 0.02), vec3(0.80, 0.60, 1.20), vec3(0.00, 0.25, 0.50));
        pB = pal(tb, vec3(0.05, 0.01, 0.00), vec3(0.55, 0.20, 0.00), vec3(1.00, 0.50, 0.00), vec3(0.10, 0.30, 0.40));
        pC = pal(tc, vec3(0.06, 0.02, 0.00), vec3(0.60, 0.35, 0.05), vec3(1.00, 0.80, 0.50), vec3(0.00, 0.10, 0.20));

    } else if (theme == 3) {
        // Toxic Circuit - black, acid green, electric lime
        pA = pal(ta, vec3(0.00, 0.04, 0.00), vec3(0.05, 0.50, 0.05), vec3(1.20, 1.00, 2.00), vec3(0.00, 0.00, 0.50));
        pB = pal(tb, vec3(0.00, 0.05, 0.00), vec3(0.00, 0.55, 0.10), vec3(2.00, 1.00, 0.00), vec3(0.00, 0.25, 0.25));
        pC = pal(tc, vec3(0.01, 0.04, 0.01), vec3(0.10, 0.50, 0.10), vec3(1.00, 1.50, 1.00), vec3(0.50, 0.00, 0.50));

    } else if (theme == 4) {
        // Solar Eclipse - black, deep amber, white corona
        pA = pal(ta, vec3(0.03, 0.02, 0.00), vec3(0.50, 0.35, 0.00), vec3(0.80, 0.50, 1.00), vec3(0.00, 0.10, 0.40));
        pB = pal(tb, vec3(0.04, 0.03, 0.00), vec3(0.55, 0.45, 0.05), vec3(1.00, 0.70, 0.00), vec3(0.20, 0.00, 0.50));
        pC = pal(tc, vec3(0.05, 0.04, 0.01), vec3(0.60, 0.50, 0.30), vec3(1.00, 0.80, 0.40), vec3(0.00, 0.20, 0.30));

    } else if (theme == 5) {
        // Neon Noir - near-black, hot pink, electric blue
        pA = pal(ta, vec3(0.02, 0.00, 0.04), vec3(0.55, 0.00, 0.40), vec3(1.00, 0.50, 0.40), vec3(0.00, 0.50, 0.20));
        pB = pal(tb, vec3(0.00, 0.00, 0.05), vec3(0.10, 0.10, 0.55), vec3(1.00, 2.00, 1.00), vec3(0.50, 0.20, 0.25));
        pC = pal(tc, vec3(0.02, 0.00, 0.04), vec3(0.50, 0.00, 0.50), vec3(0.50, 1.00, 0.50), vec3(0.25, 0.50, 0.00));

    } else if (theme == 6) {
        // Obsidian Fire - pure black, smoldering orange, ashy white edge
        pA = pal(ta, vec3(0.02, 0.01, 0.00), vec3(0.55, 0.25, 0.00), vec3(0.70, 0.40, 1.50), vec3(0.00, 0.30, 0.60));
        pB = pal(tb, vec3(0.03, 0.01, 0.00), vec3(0.60, 0.30, 0.02), vec3(1.00, 0.60, 0.20), vec3(0.10, 0.20, 0.40));
        pC = pal(tc, vec3(0.04, 0.02, 0.01), vec3(0.55, 0.40, 0.25), vec3(1.20, 0.90, 0.50), vec3(0.00, 0.10, 0.20));

    } else if (theme == 7) {
        // Cosmic Dust - deep space blue, gold, rose gold shimmer
        pA = pal(ta, vec3(0.01, 0.00, 0.03), vec3(0.35, 0.25, 0.10), vec3(0.80, 0.60, 1.00), vec3(0.00, 0.15, 0.30));
        pB = pal(tb, vec3(0.02, 0.01, 0.02), vec3(0.40, 0.30, 0.15), vec3(1.00, 0.70, 0.50), vec3(0.30, 0.10, 0.20));
        pC = pal(tc, vec3(0.02, 0.01, 0.02), vec3(0.45, 0.20, 0.25), vec3(1.00, 1.00, 0.50), vec3(0.60, 0.30, 0.10));

    } else if (theme == 8) {
        // Blood Moon - near-black, deep crimson, rust, pale bone
        pA = pal(ta, vec3(0.04, 0.00, 0.00), vec3(0.50, 0.02, 0.02), vec3(0.80, 1.00, 1.20), vec3(0.00, 0.25, 0.50));
        pB = pal(tb, vec3(0.05, 0.01, 0.00), vec3(0.55, 0.05, 0.00), vec3(1.00, 0.50, 0.30), vec3(0.10, 0.40, 0.60));
        pC = pal(tc, vec3(0.05, 0.02, 0.00), vec3(0.55, 0.20, 0.05), vec3(1.00, 0.70, 0.50), vec3(0.00, 0.10, 0.20));

    } else if (theme == 9) {
        // Deep Sea - void teal, bioluminescent aqua, pale coral
        pA = pal(ta, vec3(0.00, 0.03, 0.04), vec3(0.00, 0.40, 0.45), vec3(1.20, 1.00, 0.50), vec3(0.00, 0.20, 0.30));
        pB = pal(tb, vec3(0.00, 0.04, 0.04), vec3(0.05, 0.45, 0.40), vec3(2.00, 1.00, 0.30), vec3(0.50, 0.10, 0.30));
        pC = pal(tc, vec3(0.02, 0.03, 0.03), vec3(0.20, 0.40, 0.35), vec3(1.00, 1.20, 0.70), vec3(0.70, 0.40, 0.10));

    } else {
        // theme 10: Inkwell - pure black and white
        // All three palettes output identical greyscale values (r=g=b).
        // pA: escape bands - soft grey waves from near-black to white
        // pB: orbit trap  - finer detail layer, slightly offset phase
        // pC: stripe/edge - sharpest contrast, used for crisp boundary lines
        pA = pal(ta, vec3(0.03), vec3(0.48), vec3(1.00), vec3(0.00));
        pB = pal(tb, vec3(0.02), vec3(0.45), vec3(1.00), vec3(0.25));
        pC = pal(tc, vec3(0.01), vec3(0.50), vec3(2.00), vec3(0.10));
    }
}

// =============================================================================
// FRAGMENT
// =============================================================================
void fragment() {
    vec2 uv = UV - 0.5;
    uv.x *= aspect_ratio;

    float anim_t  = TIME * time_scale + phase_offset;
    float color_t = TIME * color_speed + color_offset;

    // Euler circle parameter: c(t) = c_radius * e^(i*anim_t)
    vec2 c = c_radius * vec2(cos(anim_t), sin(anim_t));

    // Starting z with slow global rotation
    vec2 pan = vec2(pan_x, pan_y);
    vec2 z   = euler_rot(uv / zoom + pan, anim_t * 0.04);
    vec2 z0  = z;

    // =========================================================================
    // ITERATION LOOP
    // =========================================================================
    float smooth_n   = 0.0;
    float trap_circ  = 1e9;
    float trap_axis  = 1e9;
    float trap_diag  = 1e9;
    float trap_minr  = 1e9;
    float stripe_acc = 0.0;
    float angle_sum  = 0.0;
    bool  escaped    = false;

    for (int i = 0; i < 300; i++) {
        if (i >= max_iter) { break; }

        float r2 = dot(z, z);

        if (r2 > 256.0) {
            escaped = true;
            float log_r = log(r2) * 0.5;
            float nu    = log(log_r / log(2.0)) / log(2.0);
            smooth_n    = float(i) + 1.0 - nu;
            break;
        }

        float r = sqrt(r2);

        trap_circ = min(trap_circ, abs(r - 1.0));
        trap_axis = min(trap_axis, abs(z.x));
        trap_diag = min(trap_diag, abs(z.x - z.y) * 0.707);
        trap_minr = min(trap_minr, r);

        float arg_z  = atan(z.y, z.x);
        angle_sum   += arg_z;
        stripe_acc  += 0.5 * sin(arg_z * 5.0 + anim_t * 1.6) + 0.5;

        // Euler twist: z -> z^2 * e^(i*tau*|z|^2) + c
        float twist_angle = euler_twist * r2 * 0.12;
        z = euler_rot(csq(z), twist_angle) + c;
    }

    // =========================================================================
    // DERIVED SIGNAL VALUES
    // =========================================================================
    float iter_f = float(max_iter);
    float fn     = smooth_n / iter_f;

    float fc = clamp(trap_circ * 3.0, 0.0, 1.0);
    float fa = clamp(trap_axis * 4.0, 0.0, 1.0);
    float fd = clamp(trap_diag * 4.0, 0.0, 1.0);
    float fr = clamp(trap_minr * 1.5, 0.0, 1.0);

    float fs = stripe_acc / max(smooth_n + 1.0, 1.0);
    float fw = sin(angle_sum * 0.08 + anim_t) * 0.5 + 0.5;

    float final_arg = atan(z.y, z.x) / TAU + 0.5;

    // =========================================================================
    // LOW-ITERATION ENRICHMENT
    // =========================================================================
    float low_mix   = clamp(1.0 - (iter_f - 5.0) / 35.0, 0.0, 1.0);
    float domain_a  = atan(z0.y, z0.x) / TAU + 0.5;
    float domain_r  = clamp(length(z0) / 2.0, 0.0, 1.0);
    float potential = exp(-trap_minr * 2.5) * 0.8;

    float ta_in = mix(fn,                   domain_a + domain_r * 0.3, low_mix);
    float tb_in = mix(fc,                   1.0 - fr,                  low_mix);
    float tc_in = mix(fs + final_arg * 0.4, domain_a + fw * 0.3,       low_mix);

    // =========================================================================
    // PALETTE LOOKUP
    // =========================================================================
    vec3 pA, pB, pC;
    get_palettes(color_theme,
        ta_in + color_t * 0.10,
        tb_in + color_t * 0.07,
        tc_in + color_t * 0.12,
        pA, pB, pC);

    // =========================================================================
    // COLORING
    // =========================================================================
    vec3 col;

    if (escaped) {
        col = pA;
        col = mix(col, pB, smoothstep(0.0, 0.7, 1.0 - fc) * 0.50);
        col = mix(col, pC, (1.0 - fd) * 0.20);
        col = mix(col, pC, smoothstep(0.1, 0.6, fs) * 0.35);
        col = mix(col, pC * 1.1, final_arg * 0.15 * (1.0 - fn));
        col += potential * low_mix * pB * 0.6;
        float edge = exp(-fn * iter_f * 0.10);
        col += edge * glow_strength * mix(pB, pC, 0.4) * 1.2;

    } else {
        float int_sig = fc * 0.5 + fa * 0.3 + fw * 0.2 + (1.0 - fr) * 0.4;
        vec3 hi_col   = mix(vec3(0.0), pA, clamp(int_sig, 0.0, 1.0)) * 0.55;
        hi_col += fw * 0.07 * pC;

        float dom   = domain_a + domain_r * 0.4 + color_t * 0.08;
        vec3 lo_col = pal(dom, pA * 0.5, pB * 0.8, pC, vec3(0.0));
        lo_col *= 0.7 + potential * 0.6;
        lo_col += (1.0 - fr) * 0.3 * pC;

        col = mix(hi_col, lo_col, low_mix);
        col += fw * 0.09 * pB;
    }

    // =========================================================================
    // POST-PROCESSING
    // =========================================================================

    // Balatro shimmer
    float shx = sin(UV.x * 14.0 + anim_t * 2.3) * 0.5 + 0.5;
    float shy = sin(UV.y * 11.0 + anim_t * 1.8) * 0.5 + 0.5;
    col += shx * shy * shimmer_strength * 0.04 * pB;

    // Hue drift - skipped for B&W theme to keep it pure greyscale
    if (color_theme != 10) {
        float hue_drift = sin(anim_t * 0.38) * 0.06;
        col = mix(col, col.gbr, hue_drift);
    }

    // Radial vignette
    float dist2    = dot(uv, uv);
    float vignette = 1.0 - smoothstep(0.30, 1.30, dist2 * vignette_strength);
    col *= mix(0.15, 1.0, vignette);

    // Gaussian center boost
    col *= 1.0 + 0.28 * exp(-dist2 * 3.0);

    // Strip colour for B&W - convert to luminance after all processing
    // so every lighting / glow effect still runs, then desaturate
    if (color_theme == 10) {
        float luma = dot(col, vec3(0.299, 0.587, 0.114));
        col = vec3(luma);
    }

    // Filmic Reinhard tone map
    col = col / (col + vec3(0.75));

    // Gamma correction (gamma 2.2)
    col = pow(max(col, vec3(0.0)), vec3(0.4545));

    COLOR = vec4(col, 1.0);
}

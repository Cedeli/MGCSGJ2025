shader_type canvas_item;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

vec2 hash22(vec2 p) {
    float n = hash21(p);
    return vec2(n, hash21(p + n));
}

#define STAR_DENSITY 40.0
#define STAR_THRESHOLD 0.8
#define STAR_RADIUS 0.1
#define TWINKLE_MIN 0.2
#define TWINKLE_SPEED_MULT 4.0

void fragment() {
    vec2 uv = UV;
    vec2 viewport_res = vec2(1.0) / SCREEN_PIXEL_SIZE;
    uv.x *= viewport_res.x / viewport_res.y;
    vec2 scaled_uv = uv * STAR_DENSITY;
    vec2 cell_id = floor(scaled_uv);
    vec2 cell_uv = fract(scaled_uv);
    vec3 final_color = vec3(0.0);

    for (int i = -1; i <= 1; i++) {
        for (int j = -1; j <= 1; j++) {
            vec2 neighbour_cell_id = cell_id + vec2(float(i), float(j));
            float star_rand = hash21(neighbour_cell_id);

            if (star_rand > STAR_THRESHOLD) {
                vec2 star_pos_in_cell = hash22(neighbour_cell_id);
                vec2 star_pos_relative = star_pos_in_cell - vec2(float(i), float(j));
                float dist = distance(cell_uv, star_pos_relative);
                float star_shape = smoothstep(STAR_RADIUS, 0.0, dist);
                float base_brightness = hash21(neighbour_cell_id + 10.0);
                float twinkle_speed = (hash21(neighbour_cell_id + 20.0) * 0.5 + 0.5) * TWINKLE_SPEED_MULT;
                float twinkle_phase = hash21(neighbour_cell_id + 30.0) * 6.28318;
                float twinkle = (sin(TIME * twinkle_speed + twinkle_phase) + 1.0) * 0.5;
                twinkle = mix(TWINKLE_MIN, 1.0, twinkle);
                float brightness = star_shape * base_brightness * twinkle;
                vec3 star_color = vec3(1.0);
                float color_rand = hash21(neighbour_cell_id + 40.0);
                star_color = mix(vec3(0.8, 0.8, 1.0), vec3(1.0, 1.0, 0.8), color_rand);
                final_color += star_color * brightness;
            }
        }
    }

    vec3 bg_color = vec3(0.0, 0.0, 0.05) + pow((1.0 - UV.y), 2.0) * 0.02;
    final_color += bg_color;
    final_color = clamp(final_color, 0.0, 1.0);
    COLOR = vec4(final_color, 1.0);
}
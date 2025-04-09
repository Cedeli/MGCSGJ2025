shader_type sky;

uniform int vertical_sections = 21;
uniform float star_size = 0.02;
uniform float star_falloff = 4.0;
uniform float margin = 0.05;

vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec2 get_partition(vec2 sky_coords) {
    float y = sky_coords.y * 2.0 - 1.0;
    float section_y = ceil(abs(y) * float(vertical_sections)) / float(vertical_sections);
    float horizontal_sections = max(4.0, ceil(float(vertical_sections) * 4.0 * cos(section_y * PI / 2.0)));
    horizontal_sections = horizontal_sections > 0.0 ? horizontal_sections : 1.0;
    return vec2(sky_coords.x * horizontal_sections, y * float(vertical_sections));
}

vec2 rand_vec2(vec2 xy) {
    float rand1 = fract(sin(dot(xy, vec2(11.9131, 81.2317))) * 57183.77193);
    float rand2 = fract(sin(dot(xy, vec2(16.8131, 91.2327))) * 37113.66193);
    return vec2(rand1, rand2) * (1.0 - margin * 2.0) + margin;
}

vec3 star_color(vec2 sky_coords) {
    vec2 partitions = get_partition(sky_coords);
    vec2 partition_index = floor(partitions);
    vec2 inner_coord = fract(partitions);

    vec2 star_position = rand_vec2(partition_index);
    float dist = length(inner_coord - star_position);

    vec2 star_rand = rand_vec2(partition_index);
    float size_variation = mix(0.5, 1.5, star_rand.x);
    float effective_size = star_size * size_variation;

    float brightness = pow(effective_size / dist, star_falloff);

    vec2 star_rand2 = rand_vec2(partition_index + vec2(10.0));
    float hue_value = mix(0.55, 0.1, star_rand2.x);
    float hue_offset = mix(-0.05, 0.05, star_rand2.y);
    float hue = mod(hue_value + hue_offset, 1.0);

    vec3 hsv = vec3(hue, 0.3, clamp(brightness, 0.0, 1.0));
    return hsv2rgb(hsv);
}

void sky() {
    COLOR = star_color(SKY_COORDS);
}

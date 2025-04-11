shader_type spatial;

uniform float base_radius : hint_range(0.1, 100.0, 0.1) = 1.0;
uniform float min_altitude_factor : hint_range(0.8, 1.2, 0.001) = 0.98;
uniform float max_altitude_factor : hint_range(0.8, 1.2, 0.001) = 1.04;

uniform vec4 lowland_color : source_color = vec4(0.55, 0.45, 0.3, 1.0);
uniform vec4 midland_color : source_color = vec4(0.35, 0.4, 0.2, 1.0);
uniform vec4 highland_color : source_color = vec4(0.4, 0.38, 0.35, 1.0);

uniform float low_mid_transition : hint_range(0.9, 1.1, 0.001) = 1.0;
uniform float mid_high_transition : hint_range(0.9, 1.1, 0.001) = 1.075;

uniform float blend_range : hint_range(0.0, 0.1, 0.001) = 0.01;

uniform float texture_influence : hint_range(0.0, 1.0) = 0.8;

uniform float roughness : hint_range(0.0, 1.0) = 0.8;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

varying vec3 world_vertex;
varying vec3 world_normal;
varying float altitude;

void vertex() {
	altitude = length(VERTEX);

	world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);

	UV.x = (atan(NORMAL.z, NORMAL.x) / (2.0 * PI)) + 0.5;
	UV.y = (asin(NORMAL.y) / PI) + 0.5;
}

void fragment() {
	float low_mid_blend = smoothstep(low_mid_transition * base_radius - blend_range, low_mid_transition * base_radius + blend_range, altitude);
	float mid_high_blend = smoothstep(mid_high_transition * base_radius - blend_range, mid_high_transition * base_radius + blend_range, altitude);

	vec3 current_color = lowland_color.rgb;
	current_color = mix(current_color, midland_color.rgb, low_mid_blend);
	current_color = mix(current_color, highland_color.rgb, mid_high_blend);

	ALBEDO = current_color;

	METALLIC = metallic;
	ROUGHNESS = roughness;
}
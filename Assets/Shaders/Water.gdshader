shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;

uniform vec4 shallow_color : source_color = vec4(0.25, 0.75, 1.0, 0.7);
uniform vec4 deep_color : source_color = vec4(0.0, 0.15, 0.4, 1.0);
uniform float depth_distance : hint_range(0.1, 20.0, 0.1) = 5.0;

uniform vec4 foam_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float foam_depth_threshold : hint_range(0.0, 2.0, 0.01) = 0.2;
uniform float foam_softness : hint_range(0.01, 1.0) = 0.5;

uniform float roughness : hint_range(0.0, 1.0) = 0.05;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;

void fragment() {
	vec2 screen_uv = FRAGCOORD.xy / VIEWPORT_SIZE;
	float scene_depth = texture(DEPTH_TEXTURE, screen_uv).r;

	vec4 projected_pos = vec4(screen_uv * 2.0 - 1.0, scene_depth, 1.0);
	vec4 world_pos_behind = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * projected_pos;
	world_pos_behind /= world_pos_behind.w;

	vec3 surface_world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float depth_diff = distance(surface_world_pos, world_pos_behind.xyz);

	float depth_factor = clamp(depth_diff / depth_distance, 0.0, 1.0);
	vec4 water_color = mix(shallow_color, deep_color, depth_factor);

	float foam_amount = 1.0 - smoothstep(
		foam_depth_threshold - foam_softness * 0.5,
		foam_depth_threshold + foam_softness * 0.5,
		depth_diff
	);

	vec3 final_rgb = mix(water_color.rgb, foam_color.rgb, foam_amount * foam_color.a);
	float final_alpha = mix(water_color.a, 1.0, foam_amount * foam_color.a);

	vec3 background_color = texture(SCREEN_TEXTURE, screen_uv).rgb;
	final_rgb = mix(background_color, final_rgb, final_alpha);

	ALBEDO = final_rgb;
	ALPHA = final_alpha;
	METALLIC = metallic;
	ROUGHNESS = roughness;
}
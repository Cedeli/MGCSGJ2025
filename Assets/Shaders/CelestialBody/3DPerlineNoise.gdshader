shader_type spatial;
render_mode unshaded;

uniform float grid_scale = 1.0;

vec3 random3D(vec3 uvw) {
	uvw = vec3( dot(uvw, vec3(127.1, 311.7, 513.7)),
				dot(uvw, vec3(269.5, 183.3, 396.5)),
				dot(uvw, vec3(421.3, 314.1, 119.7))
				);
				
	return -1.0 + 2.0 * fract(sin(uvw) * 43758.5453123);
}

float noise3D (vec3 uvw) {
	uvw *= grid_scale;
	vec3 gridIndex = floor(uvw);
	vec3 gridFract = fract(uvw);
	// Smooth Step Function
	vec3 blur = smoothstep(0.0, 1.0, gridFract);

	// Grid
	vec3 blb = gridIndex + vec3(0.0, 0.0, 0.0);
	vec3 brb = gridIndex + vec3(1.0, 0.0, 0.0);
	vec3 tlb = gridIndex + vec3(0.0, 1.0, 0.0);
	vec3 trb = gridIndex + vec3(1.0, 1.0, 0.0);
	vec3 blf = gridIndex + vec3(0.0, 0.0, 1.0);
	vec3 brf = gridIndex + vec3(1.0, 0.0, 1.0);
	vec3 tlf = gridIndex + vec3(0.0, 1.0, 1.0);
	vec3 trf = gridIndex + vec3(1.0, 1.0, 1.0);

	// Random Gradient
	vec3 gradBLB = random3D(blb);
	vec3 gradBRB = random3D(brb);
	vec3 gradTLB = random3D(tlb);
	vec3 gradTRB = random3D(trb);
	vec3 gradBLF = random3D(blf);
	vec3 gradBRF = random3D(brf);
	vec3 gradTLF = random3D(tlf);
	vec3 gradTRF = random3D(trf);

	vec3 distanceToPixelFromBLB = gridFract - vec3(0.0, 0.0, 0.0);
	vec3 distanceToPixelFromBRB = gridFract - vec3(1.0, 0.0, 0.0);
	vec3 distanceToPixelFromTLB = gridFract - vec3(0.0, 1.0, 0.0);
	vec3 distanceToPixelFromTRB = gridFract - vec3(1.0, 1.0, 0.0);
	vec3 distanceToPixelFromBLF = gridFract - vec3(0.0, 0.0, 1.0);
	vec3 distanceToPixelFromBRF = gridFract - vec3(1.0, 0.0, 1.0);
	vec3 distanceToPixelFromTLF = gridFract - vec3(0.0, 1.0, 1.0);
	vec3 distanceToPixelFromTRF = gridFract - vec3(1.0, 1.0, 1.0);

	// Dot Product of Random Gradient and Distance corner
	float dotBLB = dot(gradBLB, distanceToPixelFromBLB);
	float dotBRB = dot(gradBRB, distanceToPixelFromBRB);
	float dotTLB = dot(gradTLB, distanceToPixelFromTLB);
	float dotTRB = dot(gradTRB, distanceToPixelFromTRB);
	float dotBLF = dot(gradBLF, distanceToPixelFromBLF);
	float dotBRF = dot(gradBRF, distanceToPixelFromBRF);
	float dotTLF = dot(gradTLF, distanceToPixelFromTLF);
	float dotTRF = dot(gradTRF, distanceToPixelFromTRF);

	// Tri-Linear Interpolation
	return mix(
		mix (
			mix(dotBLB, dotBRB, blur.x),
			mix(dotTLB, dotTLF, blur.x),
			blur.y
		),
		mix(
			mix(dotBLF, dotBRF, blur.x),
			mix(dotTLF, dotTRF, blur.x),
			blur.y
		), 
		blur.z
	);
}

void vertex() {
	COLOR.rgb = VERTEX + 0.5;
}

void fragment() {
	vec3 uvw = COLOR.rgb;
	ALBEDO = vec3(noise3D(uvw));
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}

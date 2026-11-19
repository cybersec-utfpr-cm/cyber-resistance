shader_type canvas_item;

uniform float animation_speed : hint_range(0.0, 4.0, 0.05) = 1.15;
uniform float shimmer_strength : hint_range(0.0, 0.35, 0.01) = 0.14;
uniform float distortion_strength : hint_range(0.0, 0.012, 0.0005) = 0.0035;

void fragment() {
	vec4 source = texture(TEXTURE, UV);

	// A máscara seleciona apenas os azuis/cianos da água. A pedra permanece intacta.
	float blue_dominance = source.b - max(source.r, source.g * 0.74);
	float water_mask = smoothstep(0.06, 0.24, blue_dominance)
		* smoothstep(0.28, 0.66, source.b)
		* source.a;

	float time = TIME * animation_speed;
	float falling_flow = sin(UV.y * 118.0 - time * 8.0 + sin(UV.x * 38.0 + time * 1.7));
	float basin_ripple = sin(distance(UV, vec2(0.5, 0.67)) * 126.0 - time * 6.0);
	float shimmer = 0.5 + 0.5 * (falling_flow * 0.66 + basin_ripple * 0.34);

	vec2 moving_uv = UV + vec2(
		sin(UV.y * 72.0 + time * 3.2),
		cos(UV.x * 58.0 - time * 2.6)
	) * distortion_strength * water_mask;
	vec4 moving_sample = texture(TEXTURE, clamp(moving_uv, vec2(0.0), vec2(1.0)));

	vec3 animated_color = mix(source.rgb, moving_sample.rgb, water_mask * 0.32);
	animated_color += vec3(0.01, 0.09, 0.18)
		* water_mask
		* shimmer
		* shimmer_strength;

	COLOR = vec4(animated_color, source.a) * COLOR;
}

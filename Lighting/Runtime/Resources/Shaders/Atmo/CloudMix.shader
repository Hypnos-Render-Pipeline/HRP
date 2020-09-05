Shader "Hidden/Custom/CloudMix"
{
	HLSLINCLUDE

		#define T T_TAB
		#define L L_0
		#define J_L J_L_0
		#define S_L S_L_SHADE
		
		#include "../Includes/PostCommon.hlsl"
		#include "../Includes/Atmo/Atmo.hlsl"
		#include "../Includes/Atmo/CloudMarching.hlsl"

		float _Exp;
		float _GapLightIntensity;
		float4 _WH;

		int _EnableAurora;
		float _CloudGIIntensity;

		sampler2D _Cloud;
		sampler2D _SpaceTexture;
		sampler2D _HighCloudTexture;
		
		const float3 J_L_Cloud(const float3 x, const float3 v, const float3 s) {
			float4 x_uv = xvs_2_u(x, v, s);
			return tex4D(J_table, S_resolution, x_uv);
		}

		float3 L_Cloud(const float3 x, const float3 s) {
			float3 x_0;
			if (X_0(x, s, x_0)) return 0;
			return T(x, x_0);
		}

		float3 nmzHash33(float3 q)
		{
			uint3 p = uint3(int3(q));
			p = p * uint3(374761393U, 1103515245U, 668265263U) + p.zxy + p.yzx;
			p = p.yzx*(p.zxy ^ (p >> 3U));
			return float3(p ^ (p >> 16U)) * (1.0 / float(0xffffffffU));
		}

		float3 stars(float3 p)
		{
			float3 c = 0;
			float res = _WH.x;

			for (float i = 0.; i<4.; i++)
			{
				float3 q = frac(p*(.15*res)) - 0.5;
				float3 id = floor(p*(.15*res));
				float2 rn = nmzHash33(id).xy;
				float c2 = 1. - smoothstep(0., .6, length(q));
				c2 *= step(rn.x, .0005 + i * i*0.001);
				c += c2 * (lerp(float3(1.0, 0.49, 0.1), float3(0.75, 0.9, 1.), rn.y)*0.1 + 0.9);
				p *= 1.3;
			}
			return c * c*.8;
		}

		float3 hash33(float3 p3)
		{
			p3 = frac(p3 * float3(.1031, .11369, .13787));
			p3 += dot(p3, p3.yxz + 19.19);
			return -1.0 + 2.0 * frac(float3((p3.x + p3.y)*p3.z, (p3.x + p3.z)*p3.y, (p3.y + p3.z)*p3.x));
		}

		float perlin_noise(float3 p)
		{
			float3 pp = floor(p);
			float3 pf = p - pp;

			float3 w = pf * pf * (3.0 - 2.0 * pf);

			return max(0, lerp(
				lerp(
					lerp(dot(pf - float3(0, 0, 0), hash33(pp + float3(0, 0, 0))),
						dot(pf - float3(1, 0, 0), hash33(pp + float3(1, 0, 0))),
						w.x),
					lerp(dot(pf - float3(0, 0, 1), hash33(pp + float3(0, 0, 1))),
						dot(pf - float3(1, 0, 1), hash33(pp + float3(1, 0, 1))),
						w.x),
					w.z),
				lerp(
					lerp(dot(pf - float3(0, 1, 0), hash33(pp + float3(0, 1, 0))),
						dot(pf - float3(1, 1, 0), hash33(pp + float3(1, 1, 0))),
						w.x),
					lerp(dot(pf - float3(0, 1, 1), hash33(pp + float3(0, 1, 1))),
						dot(pf - float3(1, 1, 1), hash33(pp + float3(1, 1, 1))),
						w.x),
					w.z),
				w.y));
		}
		
		float2x2 mm2(in float a) { float c = cos(a), s = sin(a); return float2x2(c, s, -s, c); }
		float tri(in float x) { return clamp(abs(frac(x) - .5), 0.01, 0.49); }
		float2 tri2(in float2 p) { return float2(tri(p.x) + tri(p.y), tri(p.y + tri(p.x))); }
		float triNoise2d(float2 p, float spd)
		{
			float2x2 m2 = float2x2(0.95534, 0.29552, -0.29552, 0.95534);
			float z = 1.8;
			float z2 = 2.5;
			float rz = 0.;
			p = mul(mm2(p.x*0.06), p);
			float2 bp = p;
			for (float i = 0.; i<5.; i++)
			{
				float2 dg = tri2(bp*1.85)*.75;
				dg = mul(mm2(_Time.y * spd), dg);
				p -= dg / z2;

				bp *= 1.3;
				z2 *= .45;
				z *= .42;
				p *= 1.21 + (rz - 1.0)*.02;

				rz += tri(p.x + tri(p.y))*z;
				p = mul(-m2, p);
			}
			return clamp(1. / pow(rz*29., 1.3), 0., .55);
		}
		float4 aurora(float3 p) {
			float3 norm_p = normalize(p);

			float norm_h = (length(p) - planet_radius - cloud_radi.y) / 600;
			float sa = remap(norm_h, 0.1, 0.2, 1, 0.3) * remap(norm_h, 0.1, 1, 1, 0.1);

			float3 color = float3(perlin_noise(float3(norm_p.xz * 4000 + 0.33, _Time.x)) * 0.4 + 0.2,
									perlin_noise(float3(norm_p.xz * 4000, _Time.x)) * 0.7 + 0.3,
									perlin_noise(float3(norm_p.xz * 4000 + 0.66, _Time.x)) * 0.7 + 0.3);

			float cov = perlin_noise(float3(norm_p.xz * 1000, _Time.x));
			cov = remap(cov, 0, 0.2, 0, 1);

			return float4(color, pow(triNoise2d(norm_p.xz * 2000, 0.2), 2) * sa * cov * 8);
		}
		float hash21(in float2 n) { return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453); }
		float3 aurora(float3 ro, float3 rd)
		{
			float3 st = ro + rd * IntersectSphere(ro, rd, float3(0, 0, 0), planet_radius + cloud_radi.y);

			float3 col = 0;
			float4 avgCol = 0;
			float bias = Rand();
			//return aurora(st);
			for (int i = 0.; i < 64; i++)
			{
				float t = (i + bias) / 64;
				float3 bpos = st + rd * t * 4096;
				float4 rzt = aurora(bpos);
				float4 col2 = float4(0, 0, 0, rzt.a);
				col += rzt.xyz * rzt.a;
			}

			return col;
		}

		float3 RotateAroundYInDegrees(float3 vertex, float degrees)
		{
			float alpha = degrees * 3.14159265359f / 180.0f;
			float sina, cosa;
			sincos(alpha, sina, cosa);
			float2x2 m = float2x2(cosa, -sina, sina, cosa);
			return float3(mul(m, vertex.xz), vertex.y).xzy;
		}

		inline float2 ToRadialCoords(float3 coords)
		{
			float3 normalizedCoords = normalize(coords);
			float latitude = acos(normalizedCoords.y);
			float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
			float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / 3.14159265359f, 1.0 / 3.14159265359f);
			return float2(0.5, 1.0) - sphereCoords;
		}

        float3 mix(v2f i) : SV_Target
		{ 
			RandSeed(i.vertex.xy);
			float3 camPos = _V_Inv._m03_m13_m23;
			float4 v_ = mul(_VP_Inv, float4(UV * 2 - 1, 0, 1)); v_ /= v_.w;
			float3 v = normalize(v_.xyz - camPos);
			float dotvc = dot(v, -_V._m02_m12_m22);
			float3 s = normalize(-_LightDir);
			float3 x = _WorldSpaceCameraPos;
			x.y = max(x.y, 95) + planet_radius;

			float3 star_v = -normalize(mul(unity_WorldToShadow[0], float4(v, 0)).xyz);
			float2 tc = ToRadialCoords(RotateAroundYInDegrees(star_v, 0));
			float3 space = 0.04 * tex2Dlod(_SpaceTexture, float4(tc, 0, 0));
			
			float depth = 1;// LinearEyeDepth(_CameraDepthTexture.SampleLevel(sampler_CameraDepthTexture, UV, 0));
			depth = depth /= dotvc;
			float max_depth = (1.0 / _ZBufferParams.w) * 0.998;
			float far_clip_fade = smoothstep(0.9, 1, depth / max_depth);

															// remove this when using TOD shader
			float3 scene_color = depth > max_depth ? 0 : SampleColor(UV) * saturate(s.y);

															// avoid float operation accuracy loss
			float planet_h_norm = Altitude(x) / (atmosphere_thickness - 10);
			if (planet_h_norm > 1) {
				float space_offset = IntersectSphere(x, v, float3(0, 0, 0), atmosphere_radius - 10);
				if (space_offset == 0) {
					return depth > max_depth ? (L(x, v, s) + L(x, v, -s) * 0.0001) * pow(10, _Exp) + stars(star_v) + space : scene_color;
				}
				x += v * space_offset;
			}

			float cloud_h = Cloud_H(x);
			float k;
			if (cloud_h < 0) k = IntersectSphere(x, v, float3(0, 0, 0), planet_radius + cloud_radi.x);
			else k = IntersectSphere(x, v, float3(0, 0, 0), planet_radius + cloud_radi.y);
			float3 x_1 = x + v * k;

			float prevent_leak = smoothstep(cos(1.581268302306967), 0, s.y);
			float prevent_leak2 = smoothstep(-cos(1.581268302306967), -cos(1.581268302306967) + 0.1, -s.y);

			float3 x_0;
			bool ground = X_0(x, v, x_0);
			float night_light = lerp(prevent_leak2, 1, planet_h_norm) * T(x, x_0);
			float3 star = 0, aurora_light = 0;

			[branch]
			if (!ground) {
				star = (stars(star_v) * lerp(nmzHash33(v * 100 + _Time.x).z, 1, saturate(planet_h_norm)) + space) * lerp(night_light, 1, saturate(planet_h_norm));
				[branch]
				if (_EnableAurora)
					aurora_light = aurora(x, v) * night_light;
			}
			
			float dis = distance(x, x_0);
			dis = lerp(min(dis, depth), dis, far_clip_fade);
			x_0 = dis * v + x;

			float3 trans = T(x, x_0); 
			dis = min(dis, distance(x, x_1));
			x_1 = dis * v + x;
			float3 cloud_t = cloud_h > 0 && cloud_h < 1 ? 1 : T(x, x_1);
			cloud_t = saturate(cloud_t.b * 1.5 - 0.3);

			float4 cloud = tex2Dlod(_Cloud, float4(UV, 0, 0));

			cloud = lerp(cloud, float4(0, 0, 1, 1), saturate(planet_h_norm * 2 - 1));
			cloud.g *= _CloudGIIntensity;
			float3 cloud_color=0;
			float3 atmo_scatter=0;
			[branch]
			if (s.y < -0.05) {
				cloud_color = min(max(prevent_leak2, prevent_leak), cloud.r) * L_Cloud(x, -s).bgr * 0.04 +
								3e3 * cloud.g * J_L_Cloud(x, v, -s) * pow(10, _Exp);
			}
			else {
				cloud_color = min(max(prevent_leak2, prevent_leak), cloud.r) * L_Cloud(x, s) +
								3e4 * cloud.g * J_L_Cloud(x, v, s) * pow(10, _Exp);
			}
			[branch]
			if (planet_h_norm > 1) {
				atmo_scatter = (S_L_Night(x, x_0, -s, 8) + S_L(x, x_0, s, 6)) * pow(10, _Exp);
			}
			else {
				[branch]
				if (s.y < -0.05) atmo_scatter = S_L_Night(x, x_0, -s, 6);
				else atmo_scatter = S_L(x, x_0, s, 8);
				atmo_scatter *= pow(10, _Exp);
				atmo_scatter = lerp(0, atmo_scatter, max(far_clip_fade, cloud.b)); // mask leak of atmo scatter
			}

			float4 high_cloud;
			{
				k = IntersectSphere(x, v, float3(0, 0, 0), planet_radius + cloud_radi.z);
				if (k <= 0) high_cloud = float4(0, 0, 0, 0);
				else {
					float3 high_x = k * v + x;
					bool lower_than_high_cloud = length(x) < planet_radius + cloud_radi.z;
					ground = (lower_than_high_cloud ? ground : false) || X_0(high_x, s, x_0);
					high_cloud.a = tex2D(_HighCloudTexture, high_x.xz / 50000 + _Time.x / 100).r * (lower_than_high_cloud ? (1 - max(0, v.y)) : abs(v.y));
					high_cloud.a = depth > max_depth ? high_cloud.a : 0;

					high_cloud.rgb = lerp(atmo_scatter, T(high_x, x_0) * ((GetPhase(0.65, dot(v, s)) + GetPhase(0.15, dot(v, s))) * 0.5 * 0.9 + 0.1), ground ? 0 : T(x, high_x));
				}
			}
			float3 sunLight = (depth > max_depth ? L(x, v, s) + L(x, v, -s) * 0.01 : 0) * 1e-2 * pow(10, _Exp) * cloud.a * cloud.a;

			float3 sky = atmo_scatter;
			sky += sunLight;

			atmo_scatter += sunLight + far_clip_fade * (star + aurora_light) * (1 - high_cloud.a);
			atmo_scatter = lerp(atmo_scatter, high_cloud.rgb, high_cloud.a);
			sky = lerp(sky, high_cloud.rgb, high_cloud.a * cloud.a);
			scene_color = scene_color * trans + atmo_scatter;
			scene_color *= cloud.w;

			float3 gap_light = cloud.b * saturate(1 - planet_h_norm) * L(x, s, s) * GetPhase(0.5, dot(v, s)) / 3.14159265359 * _GapLightIntensity;

			return gap_light + lerp(sky, cloud_color + cloud.a * scene_color, cloud_t);
		}

	ENDHLSL


	SubShader
	{
		Cull Off ZWrite Off ZTest Always
			
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment mix			
			ENDHLSL
		}
	}
}

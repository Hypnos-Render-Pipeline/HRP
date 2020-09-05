Shader "Hidden/DeferredLighting"
{
    Properties { }
    SubShader
    {
        Pass // normal light
        {
            ZWrite off
            Cull off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/Light.hlsl"

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;

            int _DebugTiledLight;

            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos; 
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }
                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    baseColor, roughness, metallic, normal, emission, gnormal, ao);

                float3 res = 0;

                BegineLocalLightsLoop(i.uv, pos, _VP_Inv);
                {
                    float NdotL = max(0, dot(normalize(light.dir), normal));
                    float diffuseAO = lerp(ao, 1, NdotL * 0.8 + 0.2);
                    float specAO = lerp(ao + (1 - ao) * roughness * 0.8, 1, saturate(dot(view, gnormal) * 1.5 - 0.5));

                    res += lerp(pow(max(0, dot(normalize(view + light.dir), normal)), lerp(2000, 10, roughness))* lerp(10, 0.2, roughness) * specAO, NdotL* diffuseAO / 2, 0.96 - metallic)* light.radiance* baseColor;
                    if (_DebugTiledLight) {
                        res += float3(0.05, 0, 0.05);
                    }
                }
                EndLocalLightsLoop;

                return float4(res + emission, 1);
            }
            ENDCG
        }


        Pass // quad lighjt
        {
            ZWrite off
            Cull off
            Blend DstAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    baseColor, roughness, metallic, normal, emission, gnormal, ao);

                float3 lp = _LightPos;
                float3 lx = _LightX;
                float3 ly = _LightY;
                float2 size = float2(_LightX.w, _LightY.w) / 2;
                lx *= size.x;
                ly *= size.y;
                float3 ln = normalize(cross(lx, ly));

                half3x3 basis;
                basis[0] = normalize(view - normal * dot(view, normal));
                basis[1] = normalize(cross(normal, basis[0]));
                basis[2] = normal;

                half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

                L = L - half4x3(pos, pos, pos, pos);
                L = mul(L, transpose(basis));

                half theta = acos(dot(view, normal));
                half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

                half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

                float3 result = 0;
                float3 specColor;
                baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);

                half4 diffuseTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Diffuse, AmpDiffAmpSpecFresnel.x);
                result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;

                half3 specularTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Specular, AmpDiffAmpSpecFresnel.y, true);
                half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
                result += lerp(ao + (1 - ao) * roughness * 0.8, 1, saturate(dot(view, gnormal) * 1.5 - 0.5)) * specularTerm * fresnelTerm * UNITY_PI;

                return result * _LightColor;
            }
            ENDCG
        }

        Pass // tube light
        {
            ZWrite off
            Cull off
            Blend DstAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    baseColor, roughness, metallic, normal, emission, gnormal, ao);

                float3 lp = _LightPos;
                float3 ly = normalize(cross(_LightPos - pos, _LightX));
                float3 lx = _LightX;
                float2 size = float2(_LightX.w, _LightY.w) / 2;
                lx *= size.x;
                ly *= size.y;
                float3 ln = normalize(cross(lx, ly));

                half3x3 basis;
                basis[0] = normalize(view - normal * dot(view, normal));
                basis[1] = normalize(cross(normal, basis[0]));
                basis[2] = normal;

                half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

                L = L - half4x3(pos, pos, pos, pos);
                L = mul(L, transpose(basis));

                half theta = acos(dot(view, normal));
                half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

                half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

                float3 result = 0;
                float3 specColor;
                baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);

                half4 diffuseTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Diffuse, AmpDiffAmpSpecFresnel.x);
                result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;

                half3 specularTerm = TransformedPolygonRadiance(L, uv, _TransformInv_Specular, AmpDiffAmpSpecFresnel.y, true);
                half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
                result += lerp(ao + (1 - ao) * roughness * 0.8, 1, saturate(dot(view, gnormal) * 1.5 - 0.5)) * specularTerm * fresnelTerm * UNITY_PI;

                return result * _LightColor;
            }
            ENDCG
        }

        Pass // disc light
        {
            ZWrite off
            Cull off
            Blend DstAlpha One
            //Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Includes/GBuffer.hlsl"
            #include "../Includes/LTCLight.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _DepthTex, _BaseColorTex, _NormalTex, _EmissionTex, _AOTex;
            SamplerState sampler_point_clamp;


            float4x4 _V, _V_Inv;
            float4x4 _VP_Inv;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                float d = _DepthTex.SampleLevel(sampler_point_clamp, i.uv, 0).x;
                if (d == 0) return 0;

                float3 camPos = _V_Inv._m03_m13_m23;
                float3 pos;
                {
                    float4 ndc = float4(i.uv * 2 - 1, d, 1);
                    float4 worldPos = mul(_VP_Inv, ndc);
                    pos = worldPos.xyz / worldPos.w;
                }

                float3 view = normalize(camPos - pos);

                float3 baseColor;
                float roughness;
                float metallic;
                float3 normal;
                float3 emission;
                float3 gnormal;
                float ao;

                DecodeGBuffer(_BaseColorTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _NormalTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _EmissionTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    _AOTex.SampleLevel(sampler_point_clamp, i.uv, 0),
                    baseColor, roughness, metallic, normal, emission, gnormal, ao);

                float3 lp = _LightPos;
                float3 lx = _LightX;
                float3 ly = _LightY;
                float3 ln = cross(lx, ly);
                float2 size = float2(_LightX.w, _LightY.w) / 2;
                lx *= size.x;
                ly *= size.y;

                half3x3 basis;
                basis[0] = normalize(view - normal * dot(view, normal));
                basis[1] = normalize(cross(normal, basis[0]));
                basis[2] = normal;

                half4x3 L = half4x3(half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly));

                half theta = acos(dot(view, normal));
                half2 uv = half2(lerp(0.09, 0.64, roughness), theta / 1.57);

                float3 points[4] = { half3(lp - lx - ly), half3(lp + lx - ly), half3(lp + lx + ly), half3(lp - lx + ly) };

                half3 AmpDiffAmpSpecFresnel = tex2D(_AmpDiffAmpSpecFresnel, uv).rgb;

                float3 result = 0;
                float3 specColor;
                baseColor = DiffuseAndSpecularFromMetallic(baseColor, metallic, /*out*/ specColor);

                half3x3 Minv = 0;
                Minv._m22 = 1;
                {
                    float3 l = normalize(_LightPos - pos);
                    if (dot(l, ln) > 0.98 && dot(l, normal) < 0.3 && distance(_LightPos, pos) < size.x / 2.2)
                        Minv._m00_m02_m11_m20 = tex2D(_TransformInv_Diffuse, uv);
                    else
                        Minv._m00_m02_m11_m20 = half4(1, 0, 1, 0);
                    half4 diffuseTerm = LTC_Evaluate(normal, view, pos, Minv, points);
                    result = lerp(ao, 1, diffuseTerm.a * 0.8 + 0.2) * diffuseTerm.rgb * baseColor;
                    if (any(isnan(result))) result = baseColor;
                }
                {
                    Minv._m00_m02_m11_m20 = tex2D(_TransformInv_Specular, uv);
                    half3 specularTerm = LTC_Evaluate(normal, view, pos, Minv, points, true);
                    specularTerm *= 1 - smoothstep(0, 0.1, dot(reflect(view, normal), ln)) * (1 - roughness); // prevent float precision lost
                    half3 fresnelTerm = specColor + (1.0 - specColor) * AmpDiffAmpSpecFresnel.z;
                    result += lerp(ao + (1 - ao) * roughness * 0.8, 1, saturate(dot(view, gnormal) * 1.5 - 0.5)) * specularTerm * fresnelTerm;
                }

                return result * _LightColor;
            }
            ENDCG
        }
    }
}

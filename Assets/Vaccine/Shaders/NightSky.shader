Shader "Vaccine/NightSky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.006, 0.015, 0.038, 1)
        _Horizon("Horizon", Color) = (0.055, 0.095, 0.145, 1)
        _CityGlow("Distant City Amber Glow", Color) = (0.22, 0.11, 0.045, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 vertex : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _CityGlow;
            CBUFFER_END

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.vertex.xyz);
                o.dir = i.vertex.xyz;
                return o;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise(float3 p)
            {
                float3 a = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash(a), hash(a + float3(1, 0, 0)), f.x), lerp(hash(a + float3(0, 1, 0)), hash(a + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash(a + float3(0, 0, 1)), hash(a + float3(1, 0, 1)), f.x), lerp(hash(a + float3(0, 1, 1)), hash(a + 1), f.x), f.y),
                    f.z);
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y);
                half3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(h, 0.42));

                // Subtle directional city sodium light pollution near horizon
                float cityAzimuth = saturate(dot(normalize(float3(d.x, 0, d.z)), normalize(float3(0.6, 0, 0.8))) * 0.5 + 0.5);
                col += _CityGlow.rgb * pow(1.0 - h, 4.5) * (0.45 + 0.55 * cityAzimuth);

                // Stars with subtle twinkle
                float3 cell = d * 440.0;
                float starSeed = hash(floor(cell));
                float twinkle = 0.75 + 0.25 * sin(_Time.y * 2.5 + starSeed * 62.8);
                float star = step(0.9973, starSeed) * (1.0 - smoothstep(0.03, 0.24, length(frac(cell) - 0.5))) * twinkle;

                // Drifting storm clouds
                float3 wind = float3(_Time.y * 0.012, 0, _Time.y * 0.007);
                float c1 = noise(d * 4.5 + float3(2, 0, 1) + wind);
                float c2 = noise(d * 11.5 - wind * 1.4);
                float c3 = noise(d * 27.0 + wind * 2.0);
                float clouds = c1 * 0.58 + c2 * 0.28 + c3 * 0.14;
                float cloud = smoothstep(0.44, 0.74, clouds) * smoothstep(-0.02, 0.22, d.y);

                col += star * 0.9 * smoothstep(0.02, 0.32, d.y) * (1.0 - cloud);

                // Moon disc + atmospheric halo + cloud silver lining
                float3 moonDir = normalize(float3(-0.45, 0.42, 0.78));
                float moonDot = dot(d, moonDir);
                float moonDisc = smoothstep(0.99952, 0.99972, moonDot);
                float moonHalo = pow(saturate(moonDot), 140.0) * 0.065 + pow(saturate(moonDot), 18.0) * 0.028;
                col += float3(0.72, 0.84, 0.98) * (moonDisc * 0.95 + moonHalo) * (1.0 - cloud * 0.82);

                half3 cloudLit = float3(0.03, 0.048, 0.075) + _CityGlow.rgb * (1.0 - h) * 0.35 + float3(0.25, 0.35, 0.5) * pow(saturate(moonDot), 12.0) * 0.25;
                col = lerp(col, cloudLit, cloud * 0.72);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

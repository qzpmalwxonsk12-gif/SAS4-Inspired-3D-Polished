Shader "Vaccine/TacticalInteractable"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.18, 0.45, 0.45, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0.1, 0.4, 0.35, 1)
        [HDR] _PulseColor("Beacon Rim & Scan Color", Color) = (0.25, 1.35, 1.1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.55
        _RimPower("Rim Sharpness", Range(1, 6)) = 2.4
        _PulseSpeed("Pulse Speed", Float) = 2.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half4 _PulseColor;
                half _Smoothness;
                half _RimPower;
                float _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lit = SampleSH(N) * albedo;
                lit += albedo * mainLight.color * (saturate(dot(N, mainLight.direction)) * mainLight.distanceAttenuation * mainLight.shadowAttenuation);

                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, input.positionWS, half4(1, 1, 1, 1));
                    half ndotl = saturate(dot(N, l.direction));
                    float3 H = SafeNormalize(l.direction + V);
                    half spec = pow(saturate(dot(N, H)), exp2(9.0 * _Smoothness + 1.0)) * _Smoothness;
                    lit += (albedo + spec * 0.35) * l.color * (ndotl * l.distanceAttenuation * l.shadowAttenuation);
                }
                #endif

                float pulse = 0.55 + 0.45 * sin(_Time.y * _PulseSpeed);
                float scan = smoothstep(0.75, 0.98, frac(input.positionWS.y * 2.2 - _Time.y * 0.95));
                half rim = pow(1.0 - saturate(dot(N, V)), _RimPower);

                half3 beacon = _PulseColor.rgb * (rim * (0.65 + 0.65 * pulse) + scan * 0.28 + 0.12 * pulse);
                half3 color = lit + _EmissionColor.rgb + beacon;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}

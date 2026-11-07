Shader "Universal Render Pipeline/InfectedCharacter"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2.5)) = 1.15
        _Smoothness("Smoothness", Range(0, 1)) = 0.38
        _Metallic("Metallic", Range(0, 1)) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [HDR] _SubsurfaceColor("Subsurface Rim Color", Color) = (0.42, 0.05, 0.04, 1)
        _PulseSpeed("Bio Pulse Speed", Float) = 2.8
        _PulseStrength("Bio Pulse Strength", Range(0, 1)) = 0.0
        _VeinStrength("Organic Vein & Pore Detail", Range(0, 1)) = 0.42
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 250

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                float3 posOS      : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half4 _SubsurfaceColor;
                half _BumpScale;
                half _Smoothness;
                half _Metallic;
                float _PulseSpeed;
                half _PulseStrength;
                half _VeinStrength;
            CBUFFER_END

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));
                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = normInputs.normalWS;
                o.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                o.posOS = input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 nWS = normalize(input.normalWS);
                float3 tWS = normalize(input.tangentWS.xyz);
                float3 bWS = normalize(cross(nWS, tWS) * input.tangentWS.w);
                if (dot(tWS, tWS) < 0.01)
                {
                    tWS = normalize(abs(nWS.y) > 0.7 ? float3(1, 0, 0) : float3(0, 1, 0));
                    bWS = normalize(cross(nWS, tWS));
                }

                half4 texSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = texSample.rgb * _BaseColor.rgb;

                // Procedural organic vein & necrotic mottling in character-local space
                float vNoise1 = valueNoise(input.posOS * 7.5);
                float vNoise2 = valueNoise(input.posOS * 19.0);
                float veinPattern = saturate(1.0 - abs(vNoise1 - 0.5) * 5.5) * vNoise2;
                albedo = lerp(albedo, albedo * half3(0.42, 0.14, 0.16), veinPattern * _VeinStrength);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 N = normalize(normalTS.x * tWS + normalTS.y * bWS + normalTS.z * nWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                half smoothness = saturate(_Smoothness + veinPattern * 0.16);
                float specPower = exp2(9.5 * smoothness + 1.0);
                half3 specTint = lerp(half3(0.24, 0.28, 0.25), albedo, _Metallic);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 ambient = SampleSH(N) * albedo;
                half3 directDiffuse = 0;
                half3 directSpec = 0;

                // Main light with organic subsurface wrap
                half rawNdotL = dot(N, mainLight.direction);
                half ndotl = saturate(rawNdotL);
                half wrapNdotL = saturate((rawNdotL + 0.35) / 1.35);
                half sss = saturate(wrapNdotL - ndotl) * (1.0 - _Metallic);
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                directDiffuse += (albedo * ndotl + _SubsurfaceColor.rgb * sss * 0.65) * mainLight.color * atten;
                float3 H = SafeNormalize(mainLight.direction + V);
                half spec = pow(saturate(dot(N, H)), specPower) * (smoothness * 1.45);
                directSpec += specTint * mainLight.color * (spec * ndotl * atten);

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light addLight = GetAdditionalLight(li, input.positionWS, half4(1, 1, 1, 1));
                    half addRaw = dot(N, addLight.direction);
                    half addNdotL = saturate(addRaw);
                    half addWrap = saturate((addRaw + 0.35) / 1.35);
                    half addSss = saturate(addWrap - addNdotL) * (1.0 - _Metallic);
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;

                    directDiffuse += (albedo * addNdotL + _SubsurfaceColor.rgb * addSss * 0.55) * addLight.color * addAtten;
                    float3 addH = SafeNormalize(addLight.direction + V);
                    half addSpec = pow(saturate(dot(N, addH)), specPower) * (smoothness * 1.35);
                    directSpec += specTint * addLight.color * (addSpec * addNdotL * addAtten);
                }
                #endif

                // Visceral rim light so silhouettes pop dramatically in dark tunnels and night missions
                half rim = pow(1.0 - saturate(dot(N, V)), 3.2);
                half3 rimLight = _SubsurfaceColor.rgb * rim * 0.65;

                // Bioluminescent pulse for Acid/Mutant organs
                float pulse = 1.0 + _PulseStrength * (0.45 * sin(_Time.y * _PulseSpeed + input.posOS.y * 4.0));
                half3 emission = _EmissionColor.rgb * pulse;

                half3 color = ambient * (1.0 - _Metallic * 0.45) + directDiffuse * (1.0 - _Metallic * 0.35) + directSpec + rimLight + emission;
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

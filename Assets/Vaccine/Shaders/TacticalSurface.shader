Shader "Vaccine/TacticalSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2.5)) = 1.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.25
        _Metallic("Metallic", Range(0, 1)) = 0.05
        _SpecColor("Specular Color", Color) = (0.2, 0.22, 0.24, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _GrimeStrength("Weathering & Grime", Range(0, 1)) = 0.22
        _Wetness("Surface Puddle Wetness", Range(0, 1)) = 0.0
        _TriplanarScale("World Triplanar Scale", Float) = 0.45
        _UseTriplanar("Use World Triplanar (0=UV, 1=Blend)", Range(0, 1)) = 0.35
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
                float3 posLocal   : TEXCOORD5;
                float3 normLocal  : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SpecColor;
                half4 _EmissionColor;
                half _BumpScale;
                half _Smoothness;
                half _Metallic;
                half _GrimeStrength;
                half _Wetness;
                float _TriplanarScale;
                half _UseTriplanar;
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
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                float3 objScale = float3(
                    length(UNITY_MATRIX_M._m00_m10_m20),
                    length(UNITY_MATRIX_M._m01_m11_m21),
                    length(UNITY_MATRIX_M._m02_m12_m22)
                );

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.posLocal = input.positionOS.xyz * objScale;
                output.normLocal = normalize(input.normalOS / max(objScale, 0.0001));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 nWS = normalize(input.normalWS);
                float3 nLocal = normalize(input.normLocal);
                float3 absN = abs(nLocal);
                float2 triUV = absN.y > 0.65 ? input.posLocal.xz : (absN.x > absN.z ? input.posLocal.zy : input.posLocal.xy);
                triUV *= _TriplanarScale;

                half4 uvSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 triSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, triUV);
                half4 texSample = lerp(uvSample, triSample, _UseTriplanar);
                half3 albedo = texSample.rgb * _BaseColor.rgb;

                // Procedural macro-weathering & grime variation locked to object-local meter coordinates
                float macroNoise = valueNoise(input.posLocal * 0.42) * 0.65 + valueNoise(input.posLocal * 1.85) * 0.35;
                float edgeCavities = saturate(1.0 - abs(frac(input.posLocal.y * 0.5) - 0.5) * 2.0);
                float grimeMask = saturate((1.0 - macroNoise) * _GrimeStrength + edgeCavities * _GrimeStrength * 0.35);
                albedo = lerp(albedo, albedo * half3(0.58, 0.60, 0.56), grimeMask);

                // Unpack tangent normal map + procedural micro-bump
                half3 normalUV = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normalTri = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, triUV), _BumpScale);
                half3 normalTS = normalize(lerp(normalUV, normalTri, _UseTriplanar));

                float3 tWS = normalize(input.tangentWS.xyz);
                float3 bWS = normalize(cross(nWS, tWS) * input.tangentWS.w);
                // Safe fallback basis if mesh lacks authored tangents
                if (dot(tWS, tWS) < 0.01 || _UseTriplanar > 0.5)
                {
                    float3 tOS = absN.y > 0.65 ? float3(1, 0, 0) : (absN.x > absN.z ? float3(0, 0, 1) : float3(1, 0, 0));
                    tWS = normalize(TransformObjectToWorldDir(tOS));
                    bWS = normalize(cross(nWS, tWS));
                }
                float3 perturbedNormal = normalize(normalTS.x * tWS + normalTS.y * bWS + normalTS.z * nWS);

                // Puddle / damp surface effect on upward-facing floors
                float puddle = 0.0;
                if (_Wetness > 0.01 && nWS.y > 0.85)
                {
                    float pNoise = valueNoise(input.posLocal * 0.28 + float3(3.1, 0, 7.4));
                    puddle = smoothstep(0.56, 0.72, pNoise) * _Wetness;
                    albedo *= lerp(1.0, 0.68, puddle);
                    perturbedNormal = normalize(lerp(perturbedNormal, nWS, puddle * 0.82));
                }

                half luma = dot(texSample.rgb, half3(0.299, 0.587, 0.114));
                half smoothness = saturate(_Smoothness + (luma - 0.5) * 0.14 - grimeMask * 0.15 + puddle * 0.58);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 ambient = SampleSH(perturbedNormal) * albedo;
                half3 directDiffuse = 0;
                half3 directSpec = 0;

                // Main light contribution
                half ndotl = saturate(dot(perturbedNormal, mainLight.direction));
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                directDiffuse += albedo * mainLight.color * (ndotl * atten);

                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                float specPower = exp2(10.0 * smoothness + 1.0);
                half spec = pow(saturate(dot(perturbedNormal, halfDir)), specPower) * (smoothness * 1.4);
                half3 specTint = lerp(_SpecColor.rgb, albedo, _Metallic);
                directSpec += specTint * mainLight.color * (spec * ndotl * atten);

                // Additional point/spot lights
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light addLight = GetAdditionalLight(li, input.positionWS, half4(1, 1, 1, 1));
                    half addNdotL = saturate(dot(perturbedNormal, addLight.direction));
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    directDiffuse += albedo * addLight.color * (addNdotL * addAtten);

                    float3 addHalf = SafeNormalize(addLight.direction + viewDirWS);
                    half addSpec = pow(saturate(dot(perturbedNormal, addHalf)), specPower) * (smoothness * 1.35);
                    directSpec += specTint * addLight.color * (addSpec * addNdotL * addAtten);
                }
                #endif

                // Subtle rim fresnel for 3D silhouette readability in dark corridors
                half fresnel = pow(1.0 - saturate(dot(perturbedNormal, viewDirWS)), 4.0);
                half3 rimEnv = SampleSH(reflect(-viewDirWS, perturbedNormal)) * fresnel * smoothness * 0.45;

                half3 color = ambient * (1.0 - _Metallic * 0.5) + directDiffuse * (1.0 - _Metallic * 0.35) + directSpec + rimEnv + _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}

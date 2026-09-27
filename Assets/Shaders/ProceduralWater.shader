Shader "TerrainGeneration/Procedural Water"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.08, 0.65, 0.72, 0.45)
        _DeepColor("Deep Color", Color) = (0.01, 0.10, 0.32, 0.88)
        _DepthDistance("Depth Distance", Range(0.1, 30)) = 8
        _Opacity("Opacity", Range(0, 1)) = 0.8
        _WaveHeight("Wave Height", Range(0, 2)) = 0.3
        _WaveScale("Wave Scale", Range(0.01, 2)) = 0.18
        _WaveSpeed("Wave Speed", Range(0, 5)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.9
        _FresnelStrength("Fresnel Strength", Range(0, 2)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthDistance;
                float _Opacity;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _Smoothness;
                float _FresnelStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Wave(float2 position, float2 direction, float frequency, float phase)
            {
                return sin(dot(position, direction) * frequency + phase);
            }

            void EvaluateWaves(float2 position, out float height, out float2 slope)
            {
                float time = _Time.y * _WaveSpeed;
                float frequency = _WaveScale;
                float2 directionA = normalize(float2(1.0, 0.45));
                float2 directionB = normalize(float2(-0.35, 1.0));
                float2 directionC = normalize(float2(0.75, -1.0));

                float phaseA = dot(position, directionA) * frequency + time;
                float phaseB = dot(position, directionB) * frequency * 1.7 - time * 1.35;
                float phaseC = dot(position, directionC) * frequency * 2.4 + time * 0.7;
                height = (sin(phaseA) + sin(phaseB) * 0.45 + sin(phaseC) * 0.2) * _WaveHeight;
                slope = cos(phaseA) * directionA * frequency;
                slope += cos(phaseB) * directionB * frequency * 1.7 * 0.45;
                slope += cos(phaseC) * directionC * frequency * 2.4 * 0.2;
                slope *= _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float waveHeight;
                float2 waveSlope;
                EvaluateWaves(positionWS.xz, waveHeight, waveSlope);
                positionWS.y += waveHeight;

                output.positionWS = positionWS;
                output.normalWS = normalize(float3(-waveSlope.x, 1.0, -waveSlope.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                float rawDepth = SampleSceneDepth(screenUv);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float waterDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float depth = max(0.0, sceneDepth - waterDepth);
                float depth01 = saturate(depth / max(_DepthDistance, 0.001));

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), 4.0);
                Light mainLight = GetMainLight();
                float diffuse = saturate(dot(normalWS, mainLight.direction)) * 0.25 + 0.75;
                float specular = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewDirection)),
                    lerp(16.0, 256.0, _Smoothness));

                half4 waterColor = lerp(_ShallowColor, _DeepColor, depth01);
                float3 color = waterColor.rgb * diffuse * mainLight.color;
                color += fresnel * _FresnelStrength;
                color += specular * mainLight.color * _Smoothness;
                color = MixFog(color, input.fogFactor);

                float alpha = saturate(waterColor.a * _Opacity * lerp(0.35, 1.0, depth01) + fresnel * 0.15);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

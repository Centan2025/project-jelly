Shader "ProjectJelly/JellySubsurface"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.14, 0.82, 1.0, 0.86)
        _CoreColor ("Inner Core Color", Color) = (0.45, 0.96, 1.0, 1.0)
        _RimColor ("Fresnel Rim Color", Color) = (0.85, 0.98, 1.0, 1.0)
        _RimPower ("Rim Power", Range(1.0, 8.0)) = 2.8
        _RimIntensity ("Rim Intensity", Range(0.0, 2.0)) = 0.85
        _Subsurface ("Subsurface Wrap", Range(0.0, 1.5)) = 0.65
        _SpecPower ("Specular Glossiness", Range(8.0, 256.0)) = 128.0
        _SpecIntensity ("Specular Intensity", Range(0.0, 3.0)) = 1.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _CoreColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _Subsurface;
                half _SpecPower;
                half _SpecIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);

                // Wrapped diffuse + Subsurface scattering
                float NdotL = dot(N, L);
                float wrapDiffuse = saturate((NdotL + _Subsurface) / (1.0 + _Subsurface));
                float backLight = pow(saturate(dot(V, -L) * 0.5 + 0.5), 2.5) * _Subsurface * 0.55;

                // Inner core vs outer translucent shell
                float NdotV = saturate(dot(N, V));
                float coreMask = pow(NdotV, 1.4);
                half3 jellyAlbedo = lerp(_BaseColor.rgb, _CoreColor.rgb, coreMask * 0.55);

                // Studio ambient + light (balanced so crown never blows out to flat white)
                half3 ambient = half3(0.28, 0.36, 0.42) * jellyAlbedo;
                half3 diffuse = jellyAlbedo * mainLight.color * wrapDiffuse * 0.88;
                half3 sssGlow = _CoreColor.rgb * backLight * 0.65;

                // Crisp wet specular highlight (compact studio glint, not giant white blob)
                float NdotH = saturate(dot(N, H));
                float specMain = pow(NdotH, _SpecPower * 1.35) * (_SpecIntensity * 0.65);
                float specSecondary = pow(saturate(dot(N, normalize(float3(-0.4, 0.8, 0.3) + V))), _SpecPower * 0.65) * (_SpecIntensity * 0.18);

                // Fresnel rim glow
                float fresnel = pow(1.0 - NdotV, _RimPower);
                half3 rim = _RimColor.rgb * fresnel * (_RimIntensity * 0.65);

                half3 finalRGB = ambient + diffuse + sssGlow + rim + (specMain + specSecondary);
                finalRGB = MixFog(finalRGB, IN.fogFactor);

                // Solid enough (0.95+) so road stripes underneath do not bleed through the jelly's forehead
                half alpha = saturate(max(_BaseColor.a, 0.95) + fresnel * 0.05);
                return half4(finalRGB, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _LightDirection));
                return OUT;
            }

            half4 ShadowPassFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}

Shader "ProjectJelly/GlassPickupOrb"
{
    Properties
    {
        _BaseColor ("Tint Color", Color) = (0.12, 0.94, 0.44, 0.28)
        _RimColor ("Fresnel Rim Color", Color) = (0.75, 1.0, 0.85, 0.90)
        _RimPower ("Rim Power", Range(1.0, 6.0)) = 2.4
        _SpecPower ("Specular Glossiness", Range(16.0, 256.0)) = 140.0
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
            Name "GlassShell"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _SpecPower;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = normInputs.normalWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);

                float NdotV = saturate(dot(N, V));
                float NdotL = saturate(dot(N, L) * 0.5 + 0.5);
                float fresnel = pow(1.0 - NdotV, _RimPower);
                float spec = pow(saturate(dot(N, H)), _SpecPower) * 1.55;
                float bottomRefract = pow(saturate(-N.y * 0.6 + 0.5), 2.0) * 0.35;

                // Rich jewel-glass sphere: deep saturated body, bright top highlight, dark refractive rim
                half3 deepGlass = _BaseColor.rgb * lerp(0.55, 1.18, NdotL);
                half3 col = lerp(deepGlass, _RimColor.rgb, fresnel * 0.55 + bottomRefract) + spec;
                half alpha = saturate(0.42 + fresnel * 0.52 + spec * 0.45);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

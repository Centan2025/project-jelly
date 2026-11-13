Shader "ProjectJelly/ProceduralSkyGradient"
{
    Properties
    {
        _TopColor ("Zenith Color", Color) = (0.16, 0.42, 0.92, 1.0)
        _HorizonColor ("Horizon Color", Color) = (0.62, 0.84, 1.0, 1.0)
        _BottomColor ("Ground Color", Color) = (0.36, 0.32, 0.72, 1.0)
        _HorizonSharpness ("Horizon Blend", Range(0.2, 4.0)) = 1.25
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _HorizonSharpness;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.viewDir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float h = normalize(IN.viewDir).y;
                float upperT = pow(saturate(h), _HorizonSharpness);
                float lowerT = pow(saturate(-h), _HorizonSharpness * 1.4);

                half3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, upperT);
                col = lerp(col, _BottomColor.rgb, lowerT);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

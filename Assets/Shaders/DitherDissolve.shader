Shader "Custom/DitherDissolve"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _DissolveAmount("Dissolve Amount", Range(0,1)) = 0
        _DotScale("Dot Pattern Scale", Float) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _DissolveAmount;
                float _DotScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.screenPos = ComputeScreenPos(vertexInput.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // 4x4 Bayer 디더 패턴 - 화면 픽셀 좌표 기준으로 점 찍힌 듯한 클립 패턴을 만든다.
            static const float Bayer4x4[16] = {
                0.0/16, 8.0/16, 2.0/16, 10.0/16,
                12.0/16, 4.0/16, 14.0/16, 6.0/16,
                3.0/16, 11.0/16, 1.0/16, 9.0/16,
                15.0/16, 7.0/16, 13.0/16, 5.0/16
            };

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 pixelCoord = screenUV * _ScreenParams.xy * _DotScale;
                int x = (int)fmod(pixelCoord.x, 4);
                int y = (int)fmod(pixelCoord.y, 4);
                float threshold = Bayer4x4[y * 4 + x];

                // 디졸브가 진행될수록 더 많은 점(픽셀)이 사라져서 건물이 흐릿한 점선처럼 보인다.
                clip(threshold - _DissolveAmount + 0.001);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                Light mainLight = GetMainLight();
                float3 normalWS = normalize(IN.normalWS);
                half ndotl = saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4;
                half3 color = albedo * mainLight.color * ndotl;

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

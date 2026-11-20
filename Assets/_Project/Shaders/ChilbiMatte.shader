Shader "Roadways/ChilbiMatte"
{
    Properties
    {
        _BaseColor ("Primary Color", Color) = (0.75, 0.22, 0.17, 1.0)
        _AccentColor ("Accent Color", Color) = (0.66, 0.17, 0.12, 1.0)
        _GradientPower ("Gradient Power", Range(0.5, 3.0)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AccentColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            CBUFFER_START(UnityPerMaterial)
                float _GradientPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 accentColor = UNITY_ACCESS_INSTANCED_PROP(Props, _AccentColor);

                // Subtle vertical gradient ramp from top highlight to base shadow
                // Object Y spans from -1.0 to +1.0
                float verticalRamp = saturate((input.positionOS.y + 0.8) / 1.6);
                verticalRamp = pow(verticalRamp, _GradientPower);

                // Soft matte ambient lighting from directional normal
                float3 lightDir = normalize(float3(0.5, 0.8, -0.3));
                float NdotL = saturate(dot(normalize(input.normalWS), lightDir) * 0.35 + 0.65);

                half3 finalRgb = lerp(accentColor.rgb, baseColor.rgb, verticalRamp) * NdotL;

                // STRICT ZERO SPECULAR HIGHLIGHTS
                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

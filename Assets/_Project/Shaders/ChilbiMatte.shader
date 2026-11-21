Shader "Roadways/ChilbiMatte"
{
    Properties
    {
        _BaseColor ("Primary Color", Color) = (0.75, 0.22, 0.17, 1.0)
        _AccentColor ("Accent Color", Color) = (0.66, 0.17, 0.12, 1.0)
        _GradientPower ("Gradient Power", Range(0.5, 3.0)) = 1.2
        _Glossiness ("Glossiness / Shine", Range(4.0, 64.0)) = 22.0
        _SpecularStrength ("Specular Strength", Range(0.0, 1.5)) = 0.55
        _CreviceDepth ("Crevice AO Strength", Range(0.0, 1.0)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 150

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float localY : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _AccentColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            CBUFFER_START(UnityPerMaterial)
                float _GradientPower;
                float _Glossiness;
                float _SpecularStrength;
                float _CreviceDepth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.color = input.color;
                output.localY = input.positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float4 accentColor = UNITY_ACCESS_INSTANCED_PROP(Props, _AccentColor);

                // Vertical tone ramp from bottom shaded to top lit
                float verticalRamp = saturate((input.localY + 0.8) / 1.6);
                verticalRamp = pow(verticalRamp, _GradientPower);

                half3 albedo = lerp(accentColor.rgb, baseColor.rgb, verticalRamp);

                // Lighting: Directional Key Sun + Sky Ambient
                float3 normal = normalize(input.normalWS);
                float3 lightDir = normalize(float3(0.35, 0.85, -0.40)); // Matching Directional Sun

                float NdotL = saturate(dot(normal, lightDir));
                half3 diffuse = albedo * (NdotL * 0.70 + 0.35); // Soft half-lambert fill

                // Crevice Ambient Occlusion (from baked vertex colors)
                // Darkens recesses between heads, neck creases, and touching shoulders
                float ao = lerp(1.0, input.color.r, _CreviceDepth);
                half3 shaded = diffuse * ao;

                // Glossy Candy Vinyl Specular Sheen (Blinn-Phong)
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfVec));
                float specular = pow(NdotH, _Glossiness) * _SpecularStrength;

                // Subtle rim light around figurine contour
                float rim = 1.0 - saturate(dot(viewDir, normal));
                rim = pow(rim, 3.5) * 0.25;

                half3 finalRgb = shaded + (specular * baseColor.rgb * 1.3) + (specular * 0.35) + (rim * albedo * 0.5);

                return half4(finalRgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}

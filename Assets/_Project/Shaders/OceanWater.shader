Shader "Roadways/OceanWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.15, 0.78, 0.85, 1.0)
        _DeepColor ("Deep Color", Color) = (0.01, 0.47, 0.71, 1.0)
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _ShorelineZ ("Shoreline Z", Float) = -7.5
        _WaveSpeed ("Wave Speed", Float) = 1.5
        _WaveScale ("Wave Scale", Float) = 0.5
        _WaveHeight ("Wave Height", Float) = 0.07
        _FoamDepth ("Foam Depth", Float) = 1.4
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float _ShorelineZ;
                float _WaveSpeed;
                float _WaveScale;
                float _WaveHeight;
                float _FoamDepth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // Subtle organic wave undulation
                float wave = sin(_Time.y * _WaveSpeed + posWS.x * _WaveScale + posWS.z * 0.35f);
                posWS.y += wave * _WaveHeight;

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Shoreline gradient from crystal turquoise to deep ocean blue
                float distFromShore = saturate((_ShorelineZ - input.positionWS.z) / 22.0f);
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, distFromShore);

                // Moving gentle ripple bands
                float ripple = sin((input.positionWS.z + _Time.y * 1.1f) * 1.4f + cos(input.positionWS.x * 0.45f) * 1.1f);
                float rippleShimmer = smoothstep(0.75f, 0.98f, ripple) * 0.22f;
                waterColor += rippleShimmer;

                // Rhythmic shoreline foam wave edge
                float waveFoamOsc = sin(_Time.y * 2.0f + input.positionWS.x * 0.75f) * 0.35f;
                float distToShoreEdge = abs(input.positionWS.z - (_ShorelineZ + waveFoamOsc));
                float foam = smoothstep(_FoamDepth, 0.05f, distToShoreEdge);
                waterColor = lerp(waterColor, _FoamColor.rgb, foam * _FoamColor.a);

                // Sunlight specular glint
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 lightDir = normalize(float3(-0.35f, 0.85f, -0.4f));
                float3 halfDir = normalize(viewDir + lightDir);
                float spec = pow(max(0.0f, dot(float3(0.0, 1.0, 0.0), halfDir)), 28.0f) * 0.38f;
                waterColor += spec;

                return float4(waterColor, 1.0f);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

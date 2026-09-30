// Additive soft light: halos, beams, shockwave rings, sparks, tokens and dust.
// Bloom without post processing: the glow is drawn, not computed from the frame.
// Unlit, no lights, no shadows. Works with Single Pass Instanced stereo.
Shader "OCS/Glow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 1, 1, 1)
        _Shape ("Shape (0 blob, 1 beam across V, 2 ring)", Float) = 0
        _Falloff ("Falloff", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Shape;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2.0 - 1.0;
                half blob = saturate(1.0 - length(p));
                half beam = saturate(1.0 - abs(p.y));
                half ring = saturate(1.0 - abs(length(p) - 0.8) * 6.0);
                half shape = _Shape < 0.5 ? blob : (_Shape < 1.5 ? beam : ring);

                half4 c = _Color * input.color;
                c.a *= pow(shape, _Falloff);
                // Additive light fades out in fog rather than turning fog coloured.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    c.a *= ComputeFogIntensity(input.fogFactor);
                #endif
                return c;
            }
            ENDHLSL
        }
    }
}

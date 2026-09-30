// The hall floor: a glowing grid drawn in the shader, no texture, so it stays sharp at
// any distance. It also shows the room reacting to the rig, from values WorldPulseDriver
// sets globally each frame:
//   _OCSCentre        the rig, where waves start
//   _OCSWaveRadius    the power on shockwave's distance from the rig
//   _OCSWaveStrength  how bright that wave still is
//   _OCSWake          everything inside this radius is awake and fully lit
//   _OCSRipple        0 to 1, rings rolling out from the rig while it generates
// Unlit and opaque. Works with Single Pass Instanced stereo.
Shader "OCS/Grid Floor"
{
    Properties
    {
        _BaseColor ("Floor", Color) = (0.018, 0.02, 0.026, 1)
        [HDR] _GridColor ("Grid", Color) = (0.12, 0.45, 0.7, 1)
        [HDR] _WaveColor ("Wave", Color) = (0.6, 1.8, 2.6, 1)
        [HDR] _RippleColor ("Ripple", Color) = (1.6, 0.7, 0.18, 1)
        _GridSize ("Cell size, metres", Float) = 1
        _MajorEvery ("Major line every", Float) = 5
        _Standby ("Brightness before power on", Range(0, 1)) = 0.3
        _FadeRadius ("Grid fades out by, metres", Float) = 24
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Name "GridFloor"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GridColor;
                half4 _WaveColor;
                half4 _RippleColor;
                float _GridSize;
                float _MajorEvery;
                half _Standby;
                float _FadeRadius;
            CBUFFER_END

            float4 _OCSCentre;
            float _OCSWaveRadius;
            float _OCSWaveStrength;
            float _OCSWake;
            float _OCSRipple;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // One pixel wide, antialiased lines every `size` metres.
            float GridLines(float2 p, float size, float width)
            {
                float2 g = p / size;
                float2 w = fwidth(g);
                float2 d = abs(frac(g - 0.5) - 0.5) / max(w * width, 1e-5);
                float mask = 1.0 - min(min(d.x, d.y), 1.0);
                // Far away the cells shrink below a pixel and shimmer. Fade them first.
                return mask * saturate(1.5 - max(w.x, w.y) * 3.0);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.positionWS.xz;
                float r = length(p - _OCSCentre.xz);

                float minor = GridLines(p, _GridSize, 1.0);
                float major = GridLines(p, _GridSize * _MajorEvery, 1.6);
                float lines = max(minor * 0.35, major);
                float fade = saturate(1.0 - r / _FadeRadius);

                float awake = saturate((_OCSWake - r) * 0.5 + 0.5);
                half3 col = _BaseColor.rgb;
                col += _GridColor.rgb * lines * fade * lerp(_Standby, 1.0, awake);

                // The power on shockwave: a bright band on the lines and a faint fill.
                float band = (r - _OCSWaveRadius) / 0.9;
                float wave = exp(-band * band) * _OCSWaveStrength;
                col += _WaveColor.rgb * wave * (0.2 + lines * 2.5);

                // While generating: amber rings rolling out from the rig, fading with distance.
                float phase = frac((r - _Time.y * 1.6) / 1.4);
                float ripple = pow(saturate(1.0 - abs(phase - 0.5) * 2.0), 10.0);
                col += _RippleColor.rgb * ripple * _OCSRipple * saturate(1.0 - r / 9.0) * (0.25 + lines * 1.5);

                col = MixFog(col, input.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

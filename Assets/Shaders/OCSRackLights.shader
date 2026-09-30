// The front of a server rack: fourteen servers, each with a row of status lights that
// blink on their own clocks and a drive activity bar. All of it drawn from the quad's
// UVs, so a whole rack face is one quad and a hall of them batches into a draw call.
// Lights sit at standby until the power on wave (see Grid Floor) reaches the rack.
// Unlit and opaque. Works with Single Pass Instanced stereo.
Shader "OCS/Rack Lights"
{
    Properties
    {
        _PanelColor ("Panel", Color) = (0.03, 0.033, 0.04, 1)
        [HDR] _LedA ("Light A", Color) = (0.3, 1.1, 1.8, 1)
        [HDR] _LedB ("Light B", Color) = (0.3, 1.6, 0.6, 1)
        [HDR] _LedC ("Light C", Color) = (1.8, 0.8, 0.2, 1)
        _Slots ("Servers per rack", Float) = 14
        _Standby ("Brightness before power on", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Name "RackLights"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _PanelColor;
                half4 _LedA;
                half4 _LedB;
                half4 _LedC;
                float _Slots;
                half _Standby;
            CBUFFER_END

            float4 _OCSCentre;
            float _OCSWaveRadius;
            float _OCSWaveStrength;
            float _OCSWake;
            float _OCSRipple;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float Hash(float3 p)
            {
                return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                float slotF = uv.y * _Slots;
                float slot = floor(slotF);
                float sy = frac(slotF);

                // Server faceplates: a thin dark gap between each one.
                half3 col = _PanelColor.rgb * (0.55 + 0.45 * step(0.08, sy) * step(sy, 0.92));

                // Which rack this is, from where it stands, so neighbours don't blink together.
                float rack = floor(input.positionWS.x * 1.7) * 13.0 + floor(input.positionWS.z * 1.6);

                // Six status lights on the right of each server.
                float lx = (uv.x - 0.62) / 0.33 * 6.0;
                float led = floor(lx);
                float inRow = step(0.0, lx) * step(lx, 6.0);
                float2 q = float2(frac(lx) - 0.5, (sy - 0.5) * 2.4);
                float dotMask = saturate(1.0 - length(q) * 3.0) * inRow;
                float h = Hash(float3(led, slot, rack));
                float rate = lerp(0.4, 7.0, Hash(float3(led + 17.1, slot, rack)));
                // Busier while the rig generates: the whole hall looks like it's working.
                float blink = step(0.35, frac(_Time.y * rate * (1.0 + _OCSRipple * 1.5) + h));
                blink = max(blink, step(0.8, h));                    // some stay lit
                half3 ledCol = h < 0.55 ? _LedA.rgb : (h < 0.85 ? _LedB.rgb : _LedC.rgb);

                // A drive activity bar on the left that flickers.
                float bar = step(0.06, uv.x) * step(uv.x, 0.42) * step(0.44, sy) * step(sy, 0.56);
                float barOn = step(0.55, Hash(float3(slot, rack, floor(_Time.y * 3.0 + h * 7.0))));

                float r = length(input.positionWS.xz - _OCSCentre.xz);
                float awake = saturate((_OCSWake - r) * 0.8 + 0.5);
                float level = lerp(_Standby, 1.0, awake);
                col += ledCol * (dotMask * blink * 2.0 + bar * barOn * 0.35) * level;

                // The rack flashes as the wave front passes it.
                float band = (r - _OCSWaveRadius) / 0.7;
                col += _LedA.rgb * exp(-band * band) * _OCSWaveStrength * 0.6;

                col = MixFog(col, input.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}

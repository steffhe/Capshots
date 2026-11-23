// An energy barrier for the edges of a map: a faint glowing grid that's brightest at the ground,
// fades out with height, shimmers upward, lights up around the player as they get close, and
// ripples where it's shot.
// Drawn additively (it only adds light), from both sides. Used by BoundaryField, which builds
// the wall meshes: UV x is metres along the wall and UV y is metres above the ground.
Shader "Capshots/Force Field"
{
    Properties
    {
        _Color ("Color", Color) = (0.0, 0.61, 1.0, 1.0)
        _BaseOpacity ("Base Opacity", Range(0, 1)) = 0.05
        _GridSize ("Grid Size (m)", Float) = 1.5
        _LineWidth ("Line Width (cells)", Range(0.005, 0.25)) = 0.04
        _FadeHeight ("Fade Height (m)", Float) = 12
        _GroundGlow ("Ground Glow Height (m)", Float) = 0.8
        _RevealRadius ("Reveal Radius (m)", Float) = 7
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.35
        _RippleSpeed ("Ripple Speed (m/s)", Float) = 6
        _RippleLife ("Ripple Life (s)", Float) = 0.7
        _RippleStrength ("Ripple Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForceField"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _BaseOpacity;
                float _GridSize;
                half _LineWidth;
                float _FadeHeight;
                float _GroundGlow;
                float _RevealRadius;
                float _ShimmerSpeed;
                float _RippleSpeed;
                float _RippleLife;
                half _RippleStrength;
            CBUFFER_END

            // Set every frame by BoundaryField: the local camera's position
            float4 _CapshotsPlayerPos;
            // Set by BoundaryField.Ripple: where shots hit the field (xyz) and when (w, scene time)
            #define MAX_RIPPLES 8
            float4 _CapshotsRipples[MAX_RIPPLES];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = max(input.uv.y, 0.0);

                // Grid lines, anti-aliased by their on-screen width so they don't shimmer far away
                float2 cell = input.uv / _GridSize;
                float2 toLine = 0.5 - abs(frac(cell) - 0.5);
                float2 aa = max(fwidth(cell), 1e-4);
                float2 lines = 1.0 - saturate((toLine - _LineWidth * 0.5) / aa);
                // Far away the lines get thinner than a pixel; blend them into an even tint instead
                float far = saturate(max(aa.x, aa.y) * 4.0 - 0.5);
                float grid = lerp(max(lines.x, lines.y), _LineWidth * 2.0, far);

                // Bands of light drifting upward
                float shimmer = frac(height * 0.12 - _Time.y * _ShimmerSpeed);
                shimmer = shimmer * shimmer * shimmer * shimmer * 0.5;

                float heightFade = 1.0 - saturate(height / _FadeHeight);
                float ground = exp(-height / max(_GroundGlow, 0.01));

                float dist = distance(input.positionWS, _CapshotsPlayerPos.xyz);
                float reveal = 1.0 - smoothstep(_RevealRadius * 0.25, _RevealRadius, dist);

                // Rings spreading out from where shots hit, lighting up the grid as they pass, with a
                // quick flash at the impact
                float ripple = 0.0;
                [unroll]
                for (int i = 0; i < MAX_RIPPLES; i++)
                {
                    float4 hit = _CapshotsRipples[i];
                    float age = _Time.y - hit.w;
                    if (age < 0.0 || age > _RippleLife) continue;
                    float d = distance(input.positionWS, hit.xyz);
                    float ringOffset = (d - age * _RippleSpeed) / 0.35;
                    float ring = exp(-ringOffset * ringOffset);
                    float flash = exp(-d * d * 4.0) * saturate(1.0 - age * 6.0);
                    float life = 1.0 - age / _RippleLife;
                    ripple += (ring * (0.25 + 0.75 * grid) + flash) * life * life;
                }

                float opacity = _BaseOpacity * (0.4 + 2.5 * grid + shimmer) * (0.2 + 0.8 * heightFade)
                              + ground * 0.25
                              + reveal * (0.06 + 0.7 * grid)
                              + ripple * _RippleStrength;
                return half4(_Color.rgb, saturate(opacity) * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

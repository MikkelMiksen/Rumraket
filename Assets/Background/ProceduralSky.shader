// Procedural sky for the Built-in Render Pipeline: a gradient sky, a sun, and stars + a faint
// nebula band that fade in as the sky gets dark. SpaceBackdrop.cs drives the colours.
Shader "Rumraket/ProceduralSky"
{
    Properties
    {
        _ZenithColor ("Zenith colour", Color) = (0.22, 0.46, 0.88, 1)
        _HorizonColor ("Horizon colour", Color) = (0.72, 0.84, 0.96, 1)
        _GroundColor ("Below-horizon colour", Color) = (0.45, 0.52, 0.58, 1)
        _Falloff ("Horizon falloff", Range(0.1, 2)) = 0.45

        _SunDir ("Sun direction (towards the sun)", Vector) = (0.4, 0.45, 0.8, 0)
        _SunColor ("Sun colour", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun size", Range(0.0002, 0.01)) = 0.001

        _StarVisibility ("Star visibility", Range(0, 1)) = 0
        _StarDensity ("Star density", Range(0, 1)) = 0.5
        _StarSize ("Star size", Range(0.03, 0.3)) = 0.1

        _NebulaStrength ("Nebula strength", Range(0, 1)) = 0.35
        _NebulaColorA ("Nebula colour A", Color) = (0.25, 0.2, 0.55, 1)
        _NebulaColorB ("Nebula colour B", Color) = (0.1, 0.35, 0.6, 1)
        _BandDir ("Milky way plane normal", Vector) = (0.3, 0.9, 0.3, 0)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "RumraketNoise.cginc"

            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float _Falloff;
            float4 _SunDir;
            float4 _SunColor;
            float _SunSize;
            float _StarVisibility;
            float _StarDensity;
            float _StarSize;
            float _NebulaStrength;
            float4 _NebulaColorA;
            float4 _NebulaColorB;
            float4 _BandDir;

            struct appdata { float4 vertex : POSITION; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;      // on a skybox mesh the vertex position is the view direction
                return o;
            }

            // One layer of stars: a grid of cells on the sky, some of which hold a star.
            float3 starLayer(float3 dir, float scale, float density, float size, float seed)
            {
                float3 p = dir * scale + seed;
                float3 id = floor(p);
                float3 f = frac(p);
                float3 r = rk_hash33(id);

                float exists = step(1.0 - density, rk_hash13(id + 19.19));
                float d = length(f - (0.15 + 0.7 * r));
                float spot = 1.0 - smoothstep(0.0, size, d);
                float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.0 + 4.0 * r.z) + r.x * 40.0);
                float3 tint = lerp(float3(0.7, 0.8, 1.0), float3(1.0, 0.85, 0.6), r.y);

                return tint * spot * exists * twinkle * (0.35 + 0.65 * r.x);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                // Sky gradient: horizon up to the zenith, or down to the ground colour
                float up = pow(saturate(dir.y), _Falloff);
                float down = saturate(-dir.y * 3.0);
                float3 col = (dir.y >= 0.0)
                    ? lerp(_HorizonColor.rgb, _ZenithColor.rgb, up)
                    : lerp(_HorizonColor.rgb, _GroundColor.rgb, down);

                // Sun disc and glow
                float3 sunDir = normalize(_SunDir.xyz);
                float sd = saturate(dot(dir, sunDir));
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sd);
                float glow = pow(sd, 48.0) * 0.45 + pow(sd, 6.0) * 0.12;
                col += _SunColor.rgb * (disc * 3.0 + glow);

                // Stars and nebula: only show where the sky is dark
                float luminance = dot(col, float3(0.333, 0.333, 0.333));
                float dark = saturate(1.0 - luminance * 2.5) * _StarVisibility;

                if (dark > 0.001)
                {
                    float3 stars = starLayer(dir, 55.0, _StarDensity, _StarSize, 0.0)
                                 + starLayer(dir, 110.0, _StarDensity * 0.8, _StarSize * 1.2, 7.0) * 0.8;

                    float3 bandNormal = normalize(_BandDir.xyz);
                    float bandDist = dot(dir, bandNormal) * 2.5;
                    float band = exp(-bandDist * bandDist);

                    stars += starLayer(dir, 220.0, 0.6, _StarSize * 1.4, 3.0) * band * 0.9;

                    float neb = smoothstep(0.35, 0.85, rk_fbm(dir * 2.5 + 3.1)) * band;
                    float3 nebColour = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, rk_fbm(dir * 4.0 + 9.0));

                    col += (stars * 1.6 + nebColour * neb * _NebulaStrength) * dark;
                }

                return half4(col, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}

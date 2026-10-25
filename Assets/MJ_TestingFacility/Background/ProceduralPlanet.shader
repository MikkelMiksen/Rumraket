// Procedural planet for the Built-in Render Pipeline. Put it on a sphere.
// Terrain (ocean / land / peaks), clouds, optional gas-giant bands, lit by the sun
// direction SpaceBackdrop.cs sets globally, with a glowing atmosphere rim.
Shader "Rumraket/ProceduralPlanet"
{
    Properties
    {
        _ColorA ("Ocean / low colour", Color) = (0.08, 0.25, 0.6, 1)
        _ColorB ("Land / mid colour", Color) = (0.2, 0.5, 0.25, 1)
        _ColorC ("Peaks / high colour", Color) = (0.85, 0.85, 0.8, 1)
        _CloudColor ("Cloud colour", Color) = (1, 1, 1, 1)
        _AtmoColor ("Atmosphere colour", Color) = (0.4, 0.7, 1, 1)
        _AtmoPower ("Atmosphere thickness", Range(0.5, 8)) = 3
        _Seed ("Seed", Float) = 0
        _Scale ("Feature scale", Float) = 3
        _SeaLevel ("Sea level", Range(0, 1)) = 0.5
        _CloudAmount ("Cloud amount", Range(0, 1)) = 0.4
        _Bands ("Gas giant bands", Range(0, 1)) = 0
        _BandFrequency ("Band frequency", Float) = 12
        _Ambient ("Night side brightness", Range(0, 1)) = 0.06
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "RumraketNoise.cginc"

            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorC;
            float4 _CloudColor;
            float4 _AtmoColor;
            float _AtmoPower;
            float _Seed;
            float _Scale;
            float _SeaLevel;
            float _CloudAmount;
            float _Bands;
            float _BandFrequency;
            float _Ambient;
            float _Fade;

            // Set once per frame by SpaceBackdrop.cs
            float4 _RumraketSunDir;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.objPos = v.vertex.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = UnityWorldSpaceViewDir(worldPos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 pn = normalize(i.objPos);
                float3 p = pn * _Scale + _Seed * 13.37;

                // Terrain: ocean below sea level, land and peaks above it
                float h = rk_fbm(p);
                float land = smoothstep(_SeaLevel - 0.015, _SeaLevel + 0.015, h);
                float3 ocean = _ColorA.rgb * lerp(0.65, 1.0, saturate(h / max(_SeaLevel, 0.01)));
                float3 landColour = lerp(_ColorB.rgb, _ColorC.rgb,
                                         saturate((h - _SeaLevel) / max(1.0 - _SeaLevel, 0.01) * 1.8));
                float3 terrain = lerp(ocean, landColour, land);

                // Gas giant: stripes by latitude with some swirl
                float stripe = 0.5 + 0.5 * sin(pn.y * _BandFrequency + rk_fbm(p * 0.8) * 4.0);
                float3 gas = lerp(_ColorB.rgb, _ColorC.rgb, stripe);
                gas = lerp(gas, _ColorA.rgb, 0.35 * smoothstep(0.45, 0.7, rk_fbm(p * float3(0.6, 3.0, 0.6))));

                float3 surface = lerp(terrain, gas, _Bands);

                // Slowly drifting clouds (not on gas giants)
                float threshold = lerp(0.8, 0.3, _CloudAmount);
                float clouds = smoothstep(threshold, threshold + 0.15,
                                          rk_fbm(p * 1.7 + 11.3 + float3(_Time.y * 0.01, 0.0, 0.0)));
                surface = lerp(surface, _CloudColor.rgb, clouds * 0.9 * (1.0 - _Bands));

                // Sun lighting with a soft day/night edge
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float3 sun = normalize(_RumraketSunDir.xyz);
                float ndl = dot(n, sun);
                float lit = smoothstep(-0.08, 0.45, ndl);
                float3 col = surface * (_Ambient + (1.0 - _Ambient) * lit);

                // Atmosphere glow along the rim, strongest on the lit side
                float rim = pow(1.0 - saturate(dot(n, v)), _AtmoPower);
                col += _AtmoColor.rgb * rim * (0.25 + 0.75 * saturate(ndl + 0.35));

                return half4(col, _Fade);
            }
            ENDCG
        }
    }

    Fallback Off
}

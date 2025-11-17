Shader "Custom/Terrain/Wireframe"
{
    Properties
    {
        _WireColor ("Wireframe Color", Color) = (1,1,1,1)
        _DensityTint ("Density Tint Color", Color) = (1,0,0,1)  // Red by default
        _WireThickness ("Wire Thickness", Range(0.1, 10)) = 1.5
        _DensityStrength ("Density → Red Strength", Range(0, 20)) = 2.0
        _DensityThreshold ("Density Threshold", Range(0.001, 1000)) = 2.0
        _BackgroundColor ("Background", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // No culling → we want wires visible from every angle

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #include "UnityCG.cginc"

            float4 _WireColor;
            float4 _DensityTint;
            float _WireThickness;
            float _DensityStrength;
            float _DensityThreshold;
            float4 _BackgroundColor;

            struct v2g {
                float4 pos : SV_POSITION;
            };

            struct g2f {
                float4 pos : SV_POSITION;
                float3 bary : TEXCOORD0;
                float density : TEXCOORD1;   // How dense this triangle is
            };

            v2g vert(appdata_base v)
            {
                v2g o;
                o.pos = v.vertex;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                float3 p0 = IN[0].pos.xyz;
                float3 p1 = IN[1].pos.xyz;
                float3 p2 = IN[2].pos.xyz;

                // Average edge length in world space (smaller = denser mesh)
                float edgeLength = (length(p1 - p2) + length(p2 - p0) + length(p0 - p1)) / 3.0;

                // Inverse → smaller edges = higher density value
                float density = 1.0 / max(edgeLength, 0.001);

                g2f o;
                o.density = density;

                o.pos = UnityObjectToClipPos(IN[0].pos);
                o.bary = float3(1,0,0);
                triStream.Append(o);

                o.pos = UnityObjectToClipPos(IN[1].pos);
                o.bary = float3(0,1,0);
                triStream.Append(o);

                o.pos = UnityObjectToClipPos(IN[2].pos);
                o.bary = float3(0,0,1);
                triStream.Append(o);

                triStream.RestartStrip();
            }

            fixed4 frag(g2f i) : SV_Target
            {
                // Wire calculation (same as before)
                float3 deltas = fwidth(i.bary);
                float3 smoothing = deltas * _WireThickness;
                float3 thickness = deltas * (_WireThickness - 0.5);
                float3 wires = smoothstep(thickness, thickness + smoothing, i.bary);
                float wire = 1.0 - min(min(wires.x, wires.y), wires.z); // 1 on edges, 0 inside

                // Density-based red tint
                float densityFactor = saturate((i.density - _DensityThreshold) * _DensityStrength);
                float4 finalColor = lerp(_WireColor, _DensityTint, densityFactor);

                // Combine
                return lerp(_BackgroundColor, finalColor, wire);
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
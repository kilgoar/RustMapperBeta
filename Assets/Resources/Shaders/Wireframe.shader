Shader "Custom/Wireframe"
{
    Properties
    {
        _WireColor ("Wireframe Color", Color) = (0, 0.5, 1, 1)
        _InteriorColor ("Interior Color", Color) = (0, 0.5, 1, 0.3)
        _WireThickness ("Wireframe Thickness", Range(0, 10)) = 5
        _Transparency ("Transparency", Range(0, 1)) = 0.02 // Controls interior transparency
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off // Disable Z-write to allow back faces to show through
            ZTest LEqual // Keep depth test but allow overlap
            Cull Off // Render both front and back faces

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2g
            {
                float4 vertex : POSITION;
            };

            struct g2f
            {
                float4 vertex : SV_POSITION;
                float3 bary : TEXCOORD0;
            };

            v2g vert (appdata v)
            {
                v2g o;
                o.vertex = v.vertex;
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                // Vertex 1
                o.vertex = UnityObjectToClipPos(IN[0].vertex);
                o.bary = float3(1, 0, 0);
                triStream.Append(o);
                // Vertex 2
                o.vertex = UnityObjectToClipPos(IN[1].vertex);
                o.bary = float3(0, 1, 0);
                triStream.Append(o);
                // Vertex 3
                o.vertex = UnityObjectToClipPos(IN[2].vertex);
                o.bary = float3(0, 0, 1);
                triStream.Append(o);

                triStream.RestartStrip();
            }

            float _WireThickness;
            fixed4 _WireColor;
            fixed4 _InteriorColor;
            float _Transparency;

            fixed4 frag (g2f i) : SV_Target
            {
                float3 barys = i.bary;
                float3 deltas = fwidth(barys); // Screen-space derivatives
                float3 thickness = deltas * _WireThickness * 2.0; // Scale thickness
                barys = smoothstep(float3(0, 0, 0), thickness, barys);
                float minBary = min(barys.x, min(barys.y, barys.z));

                // Adjust interior color with transparency
                fixed4 interior = _InteriorColor;
                interior.a = _Transparency;

                // Draw wireframe if close to an edge, else shaded interior
                return lerp(interior, _WireColor, 1.0 - smoothstep(0.0, 0.2, minBary));
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
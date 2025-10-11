Shader "Custom/Wireframe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WireColor ("Wireframe Color", Color) = (0, 0.5, 1, 1)
        _SliceLeft ("Slice Left", Range(0, 0.5)) = 0.1
        _SliceRight ("Slice Right", Range(0, 0.5)) = 0.1
        _SliceTop ("Slice Top", Range(0, 0.5)) = 0.1
        _SliceBottom ("Slice Bottom", Range(0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _WireColor;
            float _SliceLeft;
            float _SliceRight;
            float _SliceTop;
            float _SliceBottom;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = v.normal; // Pass normal to determine face
                return o;
            }

            // Adjust UVs based on cube face (determined by normal)
            float2 AdjustUVForFace(float2 uv, float3 normal)
            {
                float2 adjustedUV = uv;
                float3 absNormal = abs(normal);
                
                // Determine dominant normal axis to identify face
                if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
                {
                    // X-face (+X or -X)
                    adjustedUV = normal.x > 0 ? float2(uv.x, uv.y) : float2(1.0 - uv.x, uv.y);
                }
                else if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
                {
                    // Y-face (+Y or -Y)
                    adjustedUV = normal.y > 0 ? float2(uv.x, uv.y) : float2(uv.x, 1.0 - uv.y);
                }
                else
                {
                    // Z-face (+Z or -Z)
                    adjustedUV = normal.z > 0 ? float2(uv.x, uv.y) : float2(1.0 - uv.x, uv.y);
                }
                
                return adjustedUV;
            }

            // 9-slice UV remapping function
            float2 SliceUV(float2 uv, float sliceLeft, float sliceRight, float sliceTop, float sliceBottom)
            {
                float2 slicedUV = uv;
                float u = uv.x;
                float v = uv.y;

                // Define texture regions
                float leftBorder = sliceLeft;
                float rightBorder = 1.0 - sliceRight;
                float bottomBorder = sliceBottom;
                float topBorder = 1.0 - sliceTop;

                // Map UVs to texture regions
                // Corners (a, c, g, i) - no scaling
                if (u < leftBorder)
                {
                    slicedUV.x = u; // Left side (a, g)
                }
                else if (u > rightBorder)
                {
                    slicedUV.x = u; // Right side (c, i)
                }
                else
                {
                    // Middle (b, e, h) - scale horizontally
                    slicedUV.x = sliceLeft + (u - leftBorder) / (rightBorder - leftBorder) * (1.0 - sliceLeft - sliceRight);
                }

                if (v < bottomBorder)
                {
                    slicedUV.y = v; // Bottom side (g, h, i)
                }
                else if (v > topBorder)
                {
                    slicedUV.y = v; // Top side (a, b, c)
                }
                else
                {
                    // Middle (d, e, f) - scale vertically
                    slicedUV.y = sliceBottom + (v - bottomBorder) / (topBorder - bottomBorder) * (1.0 - sliceTop - sliceBottom);
                }

                return slicedUV;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Adjust UVs based on cube face
                float2 faceUV = AdjustUVForFace(i.uv, normalize(i.normal));

                // Apply 9-slice UV transformation
                float2 slicedUV = SliceUV(faceUV, _SliceLeft, _SliceRight, _SliceTop, _SliceBottom);

                // Sample the texture
                fixed4 texColor = tex2D(_MainTex, slicedUV);

                // Apply wire color with texture's alpha
                return texColor * _WireColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
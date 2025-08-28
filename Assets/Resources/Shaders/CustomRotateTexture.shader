Shader "Custom/RotateTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Rotation ("Rotation (radians)", Float) = 0
        _DefaultColor ("Default Color", Color) = (0, 0, 0, 0) // Default for out-of-bounds UVs
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha // Enable alpha blending
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Rotation;
            float4 _DefaultColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 pivot = float2(0.5, 0.5);
                float cosA = cos(_Rotation);
                float sinA = sin(_Rotation);
                float2x2 rotationMatrix = float2x2(cosA, -sinA, sinA, cosA);
                o.uv = mul(rotationMatrix, v.uv - pivot) + pivot;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Check if UVs are within [0, 1]
                if (i.uv.x < 0.0 || i.uv.x > 1.0 || i.uv.y < 0.0 || i.uv.y > 1.0)
                {
                    return _DefaultColor; // Return transparent or default value
                }
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
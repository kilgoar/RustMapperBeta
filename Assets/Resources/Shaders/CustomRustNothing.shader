Shader "Custom/Rust/Nothing"
{
    Properties
    {
		[Toggle(_SELECTION_ON)] _SelectionOn("Selection Indicator", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // No rendering pass needed, but we include an empty pass to avoid errors
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Discard all pixels to render nothing
                discard;
                return fixed4(0,0,0,0); // Fallback, though discard makes this unreachable
            }
            ENDCG
        }
    }
}
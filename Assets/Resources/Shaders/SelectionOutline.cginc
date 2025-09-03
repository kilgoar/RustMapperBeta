#ifndef SELECTION_OUTLINE_INCLUDED
#define SELECTION_OUTLINE_INCLUDED

// Shared properties for selection indicator
//float _SelectionOn;
//fixed4 _SelectionColor;
//float _OutlineThickness;

Pass
{
    Name "OUTLINE"
    Tags { "LightMode" = "Always" }
    Cull Front
    ZWrite On
    ZTest LEqual

    Stencil
    {
        Ref 1
        Comp Always
        Pass Replace
    }

    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
    #pragma shader_feature _SELECTION_ON
    #include "UnityCG.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
    };

    v2f vert(appdata v)
    {
        v2f o;
        #ifdef _SELECTION_ON
            v.vertex.xyz += v.normal * _OutlineThickness;
            o.pos = UnityObjectToClipPos(v.vertex);
        #else
            o.pos = float4(0,0,0,0);
        #endif
        return o;
    }

    fixed4 frag(v2f i) : SV_Target
    {
        #ifdef _SELECTION_ON
            return _SelectionColor;
        #else
            discard;
        #endif
    }
    ENDCG
}

#endif
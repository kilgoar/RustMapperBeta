Shader "Custom/VolumesStandard"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,0.5) // Default to semi-transparent
        [Toggle(_SELECTION_ON)] _SelectionOn("Selection Indicator", Float) = 0
        _SelectionColor("Selection Color", Color) = (1,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // Enable transparency
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard alpha:blend
        #pragma shader_feature_local _SELECTION_ON

        #include "UnityCG.cginc"
        #include "Lighting.cginc"
		#include "Outline.cginc"



        fixed4 _Color;
        float _SelectionOn;
        fixed4 _SelectionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
            float3 worldPos;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = _Color;

            // Apply outline
            ApplyOutline(albedo.rgb, WorldNormalVector(IN, o.Normal), IN.uv_MainTex, _SelectionOn, _SelectionColor);

            o.Albedo = albedo.rgb;
            o.Alpha = albedo.a;
            o.Metallic = 0.0;
            o.Smoothness = 0.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
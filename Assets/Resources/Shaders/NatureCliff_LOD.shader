Shader "Custom/NatureCliff_LOD"
{
    Properties
    {
        [Header(BIOME TINT)] [Toggle(_BIOMELAYER_ON)] _BiomeLayer ("Enabled", Float) = 0
        _BiomeLayer_TintMask ("Mask", 2D) = "white" { }
        [Enum(Dirt,0,Sand,2,Rock,3,Grass,4,Forest,5,Stones,6,Gravel,7)] _BiomeLayer_TintSplatIndex ("Tint Splat Index", Float) = -1
        _Normal ("Normal", 2D) = "bump" { }
        _TerrainBlendFactor ("Terrain Blend Factor", Range(0, 16)) = 8
        _TerrainBlendOffset ("Terrain Blend Offset", Range(0.001, 4)) = 1
        _TerrainMask ("Terrain Mask", 2D) = "white" { }
        _Albedo ("Albedo", 2D) = "white" { }
        [Toggle] _SelectionOn ("Selection Outline", Float) = 0
        _SelectionColor ("Selection Color", Color) = (1,1,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #pragma multi_compile_local _ _BIOMELAYER_ON

        #include "UnityCG.cginc"
        #include "UnityPBSLighting.cginc"
        #include "Outline.cginc"

        struct Input
        {
            float2 uv_Albedo;
            float2 uv_Normal;
            float2 uv_BiomeLayer_TintMask;
            float3 worldNormal;
            INTERNAL_DATA
        };

        sampler2D _Albedo;
        sampler2D _Normal;
        sampler2D _BiomeLayer_TintMask;
        float _BiomeLayer;
        float _BiomeLayer_TintSplatIndex;
        float _SelectionOn;
        fixed4 _SelectionColor;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_Albedo = v.texcoord.xy;
            o.uv_Normal = v.texcoord.xy;
            o.uv_BiomeLayer_TintMask = v.texcoord.xy;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo
            fixed4 albedo = tex2D(_Albedo, IN.uv_Albedo);

            // Apply outline effect
            ApplyOutline(albedo.rgb, IN.worldNormal, IN.uv_Albedo, _SelectionOn, _SelectionColor);

            // Output
            o.Albedo = albedo.rgb;

            // Normal
            float3 normal = UnpackNormal(tex2D(_Normal, IN.uv_Normal));
            o.Normal = normal;

            // Default PBR properties for LOD (simplified)
            o.Metallic = 0.0; // Rocks are typically non-metallic
            o.Smoothness = 0.2; // Slightly rough surface

            // No ambient occlusion for LOD to keep it lightweight
            o.Occlusion = 1.0;

            // Alpha (opaque)
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
Shader "Custom/NatureCliff_LOD" {
    Properties {
        [Header(BIOME TINT)] [Toggle(_BIOMELAYER_ON)] _BiomeLayer ("Enabled", Float) = 0
        _BiomeLayer_TintMask ("Mask", 2D) = "white" { }
        [Enum(Dirt,0,Sand,2,Rock,3,Grass,4,Forest,5,Stones,6,Gravel,7)] _BiomeLayer_TintSplatIndex ("Tint Splat Index", Float) = -1
        _Normal ("Normal", 2D) = "bump" { }
        _TerrainBlendFactor ("Terrain Blend Factor", Range(0, 16)) = 8
        _TerrainBlendOffset ("Terrain Blend Offset", Range(0.001, 4)) = 1
        _TerrainMask ("Terrain Mask", 2D) = "white" { }
        _Albedo ("Albedo", 2D) = "white" { }
    }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "UnityCG.cginc"
        #include "UnityPBSLighting.cginc"

        struct Input {
            float2 uv_Albedo;
            float2 uv_Normal;
            float2 uv_BiomeLayer_TintMask;
        };

        sampler2D _Albedo;
        sampler2D _Normal;
        sampler2D _BiomeLayer_TintMask;
        float _BiomeLayer;
        float _BiomeLayer_TintSplatIndex;

        void surf (Input IN, inout SurfaceOutputStandard o) {
            // Albedo
            fixed4 albedo = tex2D(_Albedo, IN.uv_Albedo);

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
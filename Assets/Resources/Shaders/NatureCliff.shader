Shader "Custom/NatureCliff"
{
    Properties
    {
        _CavityTinting ("Cavity Tinting", Color) = (0.8666667,0.8666667,0.8627452,0)
        [Header(BIOME TINT)] [Toggle(_BIOMELAYER_ON)] _BiomeLayer ("Enabled", Float) = 0
        _BiomeLayer_TintMask ("Mask", 2D) = "white" { }
        [Enum(Dirt,0,Sand,2,Rock,3,Grass,4,Forest,5,Stones,6,Gravel,7)] _BiomeLayer_TintSplatIndex ("Tint Splat Index", Float) = -1
        _GlobalNormal ("Global Normal", 2D) = "bump" { }
        _PeaksBrightness ("Peaks Brightness", Float) = 2
        _CavityMaskBrightness ("Cavity Mask Brightness", Range(1, 16)) = 0
        _CavityMaskPower ("Cavity Mask Power", Range(1, 16)) = 0
        _PeaksMaskPower ("Peaks Mask Power", Range(1, 16)) = 0
        _PeaksMaskBrightness ("Peaks Mask Brightness", Range(1, 16)) = 0
        _TerrainBlendFactor ("Terrain Blend Factor", Range(0, 16)) = 8
        _TerrainBlendOffset ("Terrain Blend Offset", Range(0.001, 4)) = 1
        [Header(SHORE WETNESS)] [Toggle(_SHOREWETNESSLAYER_ON)] _ShoreWetnessLayer ("Enable", Float) = 0
        _ShoreWetnessLayer_Range ("Range", Float) = 2
        [PowerSlider(4)] _ShoreWetnessLayer_BlendFactor ("Blend Factor", Range(0, 128)) = 2
        [PowerSlider(4)] _ShoreWetnessLayer_BlendFalloff ("Blend Falloff", Range(0.001, 128)) = 2
        _ShoreWetnessLayer_WetAlbedoScale ("Wet Albedo Scale", Range(0, 1)) = 0.5
        _ShoreWetnessLayer_WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.85
        _AOIntensity ("AO Intensity", Range(0, 1)) = 1
        _CliffDataMap ("Cliff Data Map", 2D) = "white" { }
        _RockAlbedo ("Rock Albedo", 2D) = "white" { }
        _RockNormal ("Rock Normal", 2D) = "bump" { }
        _RockPackedSpec ("Rock Packed Spec", 2D) = "white" { }
        [Toggle(_SELECTION_ON)] _SelectionOn ("Selection Outline", Float) = 0
        _SelectionColor ("Selection Color", Color) = (1,1,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #pragma multi_compile_local _ _BIOMELAYER_ON
        #pragma multi_compile_local _ _SHOREWETNESSLAYER_ON
        #pragma multi_compile_local _ _SELECTION_ON

        #include "UnityCG.cginc"
        #include "UnityPBSLighting.cginc"
        #include "Outline.cginc"

        struct Input
        {
            float2 uv_RockAlbedo;
            float2 uv_RockNormal;
            float2 uv_RockPackedSpec;
            float2 uv_CliffDataMap;
            float3 worldNormal;
            INTERNAL_DATA
        };

        sampler2D _RockAlbedo;
        sampler2D _RockNormal;
        sampler2D _RockPackedSpec;
        sampler2D _CliffDataMap;
        float _AOIntensity;
        float _SelectionOn;
        fixed4 _SelectionColor;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_RockAlbedo = v.texcoord.xy;
            o.uv_RockNormal = v.texcoord.xy;
            o.uv_RockPackedSpec = v.texcoord.xy;
            o.uv_CliffDataMap = v.texcoord.xy;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Sample textures
            fixed4 albedo = tex2D(_RockAlbedo, IN.uv_RockAlbedo);
            float3 normal = UnpackNormal(tex2D(_RockNormal, IN.uv_RockNormal));
            fixed4 packedSpec = tex2D(_RockPackedSpec, IN.uv_RockPackedSpec);
            fixed cavity = tex2D(_CliffDataMap, IN.uv_CliffDataMap).r; // Red channel for shadow/darkening

            // Apply cavity darkening to albedo
            float cavityEffect = lerp(1.0, cavity, _AOIntensity); // Use _AOIntensity to control darkening strength
            albedo.rgb *= cavityEffect; // Darken albedo in cavity areas

            ApplyOutline(albedo.rgb, IN.worldNormal, IN.uv_RockAlbedo, _SelectionOn, _SelectionColor);


            // Output
            o.Albedo = albedo.rgb;
            o.Normal = normal;
            //o.Metallic = packedSpec.g; // Assuming green channel for metallic
            //o.Smoothness = packedSpec.r; // Assuming red channel for smoothness
            o.Occlusion = cavityEffect; // Reuse for AO
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
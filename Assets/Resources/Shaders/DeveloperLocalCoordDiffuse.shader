Shader "Developer/LocalCoordDiffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.0
        _BaseOffset ("Base Offset", Vector) = (0,0,0,0)
        _Tiling ("Tiling", Float) = 1.0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _SpecColor ("Specular", Color) = (0.2,0.2,0.2,1)
        _SpecGlossMap ("Specular (RGB) Occlusion (G)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Scale", Float) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        [Toggle] _EmissionFresnel ("Emission Fresnel", Float) = 0
        _EmissionFresnelPower ("Power", Range(0, 16)) = 1
        [Toggle] _EmissionFresnelInvert ("Invert", Float) = 0
        [Toggle] _BlendLayer1 ("Blend Layer 1 Enabled", Float) = 0
        [Toggle(_SELECTION_ON)] _SelectionOn ("Selection Outline", Float) = 0
        _SelectionColor ("Selection Color", Color) = (1,1,0,1)

        [Enum(Off,0,Front,1,Back,2)] _Cull ("Cull", Float) = 0
        [Enum(Opaque,0,Cutout,1,Fade,2,Transparent,3)] _Mode ("Blend Mode", Float) = 1
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "PerformanceChecks"="False" }
        LOD 300
        Cull [_Cull]
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf StandardSpecular fullforwardshadows vertex:vert
        #pragma multi_compile_local _ _BLENDLAYER1
        #pragma multi_compile_local _ _BLENDLAYER2
        #pragma multi_compile_local _ _BLENDLAYER3
        #pragma multi_compile_local _ _EMISSIONFRESNEL
        #pragma multi_compile_local _ _SELECTION_ON

        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"
        #include "Outline.cginc"

        sampler2D _MainTex;
        sampler2D _SpecGlossMap;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;

        fixed4 _Color;
        float _Glossiness;
        float _BumpScale;
        float _OcclusionStrength;
        fixed4 _EmissionColor;
        float _EmissionFresnelPower;
        float _EmissionFresnelInvert;
        float4 _BaseOffset;
        float _Tiling;
        float _SelectionOn;
        fixed4 _SelectionColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 localPos;
            float3 localNormal;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.localPos = v.vertex.xyz;
            o.localNormal = v.normal;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
        }

        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            // Compute tiling in world space for consistent global scale
            float3 worldPos = IN.worldPos;
            float3 scaledWorldPos = (worldPos + _BaseOffset.xyz) * _Tiling;

            // Extract the object's scale from the world-to-object matrix
            float3 objectScale = float3(
                length(float3(unity_WorldToObject[0].x, unity_WorldToObject[1].x, unity_WorldToObject[2].x)),
                length(float3(unity_WorldToObject[0].y, unity_WorldToObject[1].y, unity_WorldToObject[2].y)),
                length(float3(unity_WorldToObject[0].z, unity_WorldToObject[1].z, unity_WorldToObject[2].z))
            );

            // Transform world position to local space, but normalize out the scale
            float3 localPosForOrientation = IN.localPos;
            float3 scaleNormalizedLocalPos = float3(
                localPosForOrientation.x * objectScale.x,
                localPosForOrientation.y * objectScale.y,
                localPosForOrientation.z * objectScale.z
            );
            scaleNormalizedLocalPos = mul(unity_WorldToObject, float4(scaledWorldPos, 1.0)).xyz / objectScale;

            // Apply the global tiling scale to the normalized local position
            float3 tiledLocalPos = scaleNormalizedLocalPos * _Tiling;

            // Use local normal for triplanar mapping to respect local face orientation
            float3 absNormal = abs(normalize(IN.localNormal));
            float2 uv;
            if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
            {
                // X-facing face (±X), use YZ plane in local space
                uv = frac(tiledLocalPos.yz);
            }
            else if (absNormal.y > absNormal.z)
            {
                // Y-facing face (±Y), use XZ plane in local space
                uv = frac(tiledLocalPos.xz);
            }
            else
            {
                // Z-facing face (±Z), use XY plane in local space
                uv = frac(tiledLocalPos.xy);
            }

            // Sample albedo
            fixed4 albedo = tex2D(_MainTex, uv) * _Color;

                ApplyOutline(albedo.rgb, IN.worldNormal, uv, _SelectionOn, _SelectionColor);


            // Output
            o.Albedo = albedo.rgb;
            o.Alpha = albedo.a;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
}
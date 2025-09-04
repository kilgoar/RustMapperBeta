#ifndef OUTLINE_CGINC
#define OUTLINE_CGINC

#pragma shader_feature_local _SELECTION_ON

void ApplyOutline(inout fixed3 albedo, float3 normal, float2 uv, float selectionOn, fixed4 selectionColor)
{
	
    if (selectionOn > 0.5)
    {
        // Normalize inputs
        float3 normalDir = normalize(normal);
        float3 viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz);

        // Enhanced edge detection
        float edge = abs(dot(normalDir, viewDir));
        // Soften and control outline thickness with smoothstep
        float outlineFactor = smoothstep(0.1, 0.4, 1.0 - edge); // Adjustable edge range
        // Add a subtle glow-like modulation
        float pulse = 0.5 + 0.5 * sin(_Time.y * 3.0); // Optional pulsing effect
        float outlineStrength = outlineFactor * lerp(0.4, .6, pulse); // Dynamic strength

        // Blend albedo with selection color, preserving some original color
        albedo = lerp(albedo * 0.3, selectionColor.rgb, outlineStrength * selectionColor.a);
    }
}

#endif
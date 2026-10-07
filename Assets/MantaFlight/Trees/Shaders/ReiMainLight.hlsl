#ifndef REI_MAIN_LIGHT_INCLUDED
#define REI_MAIN_LIGHT_INCLUDED
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

// The only custom node: obtain URP's real directional light and realtime shadows.
// Color mixing, cutout selection and stylized lighting remain editable graph nodes.
void ReiMainLight_float(float3 Position, out float3 Direction, out float3 Color, out float Shadow)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = normalize(float3(0.4,0.8,0.3)); Color = 1; Shadow = 1;
#else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(Position));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(Position);
    #endif
    Light light = GetMainLight(shadowCoord);
    Direction = light.direction;
    Color = light.color;
    Shadow = light.shadowAttenuation * light.distanceAttenuation;
#endif
}
void ReiMainLight_half(half3 Position, out half3 Direction, out half3 Color, out half Shadow)
{
    float3 d,c; float s; ReiMainLight_float(Position,d,c,s); Direction=d; Color=c; Shadow=s;
}
#endif

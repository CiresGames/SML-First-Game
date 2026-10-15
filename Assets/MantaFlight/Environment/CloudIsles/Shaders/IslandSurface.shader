Shader "MantaFlight/Cloud Isles/Weathered Stone and Meadow"
{
 Properties { _BaseColor("Stone / meadow tint",Color)=(.43,.49,.48,1) _Meadow("Meadow",Float)=0 _BaseMap("Compatibility",2D)="white"{} _Cutoff("Cutoff",Float)=.5 _Cull("Cull",Float)=2 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;float _Meadow;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;};
 V vert(A i){V o;o.p=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.p);o.n=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
 half4 frag(V i):SV_Target {
 float3 n=normalize(i.n);float broad=noise(i.p*.035),fine=noise(i.p*.42);
 float strata=pow(.5+.5*sin(i.p.y*.37+broad*3.8),14);
 float seam=smoothstep(.70,.90,noise(float3(i.p.x*.25,i.p.y*.013,i.p.z*.25)));
 float3 stone=_BaseColor.rgb*lerp(.60,1.32,broad)*lerp(.89,1.09,fine)*(1-strata*.18-seam*.2);
 float moss=smoothstep(.38,.86,n.y)*smoothstep(.32,.67,broad)*.65;
 stone=lerp(stone,float3(.18,.26,.09),moss);
 float3 meadow=_BaseColor.rgb*lerp(.64,1.35,broad)*lerp(.88,1.12,fine);
 float3 albedo=lerp(stone,meadow,_Meadow);
 Light l=GetMainLight(TransformWorldToShadowCoord(i.p));
 float3 lit=SampleSH(n)*.85+l.color*(saturate(dot(n,l.direction))*.86+.08)*lerp(.26,1,l.shadowAttenuation);
 float3 c=albedo*max(lit,float3(.21,.25,.30));return half4(MixFog(c,i.fog),1);
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}

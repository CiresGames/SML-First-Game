Shader "MantaFlight/Cloud Isles/Soft Spray"
{
 Properties { _BaseColor("Mist",Color)=(.72,.85,.89,.12) }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
 Pass {
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;
 CBUFFER_END
 struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float fog:TEXCOORD1;};
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target {float2 p=(i.uv-.5)*2;float a=pow(saturate(1-dot(p,p)),2);return half4(MixFog(_BaseColor.rgb*i.color.rgb,i.fog),a*_BaseColor.a*i.color.a);}
 ENDHLSL
 }
 }
}

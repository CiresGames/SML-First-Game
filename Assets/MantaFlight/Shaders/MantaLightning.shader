Shader "Manta/Lightning"
{
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 p:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 struct V {float4 p:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.color=i.color;o.uv=i.uv;return o;}
 half4 Frag(V i):SV_Target {return half4(i.color.rgb,i.color.a*pow(saturate(1-abs(i.uv.y*2-1)),.6));}
 ENDHLSL
 }}
}

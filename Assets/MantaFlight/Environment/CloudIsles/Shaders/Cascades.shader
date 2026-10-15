Shader "MantaFlight/Cloud Isles/Cascades"
{
 Properties { _BaseColor("Water",Color)=(.24,.64,.69,1) _Foam("Foam",Color)=(.80,.96,1,1) _Flow("Flow speed",Float)=.22 _River("Horizontal water",Float)=0 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_Foam;float _Flow,_River;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 p:TEXCOORD1;float fog:TEXCOORD2;};
 V vert(A i){V o;o.p=TransformObjectToWorld(i.positionOS.xyz);o.uv=i.uv;o.positionCS=TransformWorldToHClip(o.p);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
 float hash2(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise2(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash2(a),hash2(a+float2(1,0)),f.x),lerp(hash2(a+float2(0,1)),hash2(a+1),f.x),f.y);}
 half4 frag(V i):SV_Target {
 float t=_Time.y*_Flow;float u=i.uv.x,v=i.uv.y;
 float strands=noise2(float2(u*36,v*5-t*10));
 float flow=noise2(float2(u*79,v*12-t*23));
 float wisps=smoothstep(.28,.82,strands*.7+flow*.3);
 float edge=smoothstep(0,.07,u)*smoothstep(0,.07,1-u);
 float end=lerp(1-smoothstep(.72,1,v),1,_River);
 float foam=lerp(.24+wisps*.7,.06+pow(flow*strands,3)*.4,_River);
 float3 col=lerp(_BaseColor.rgb,_Foam.rgb,foam);
 Light sun=GetMainLight();col*=.55+sun.color*.48;
 float alpha=edge*end*lerp(.52+wisps*.45,.86,_River);
 return half4(MixFog(col,i.fog),alpha);
 }
 ENDHLSL
 }
 }
}

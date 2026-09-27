Shader "Manta/Rain streak"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0;};
            struct V {float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0;};
            V Vert(A i) { V o; o.position=TransformObjectToHClip(i.vertex.xyz); o.color=i.color; o.uv=i.uv; return o; }
            half4 Frag(V i):SV_Target
            {
                float width=pow(saturate(1-abs(i.uv.x*2-1)),2);
                float ends=saturate(min(i.uv.y,1-i.uv.y)*8);
                return half4(i.color.rgb,i.color.a*width*ends);
            }
            ENDHLSL
        }
    }
}

Shader "Manta/Wind streak"
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
            struct Attributes { float4 position : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.position = TransformObjectToHClip(i.position.xyz); o.color = i.color; o.uv = i.uv; return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half width = pow(saturate(1 - abs(i.uv.x * 2 - 1)), 2);
                half ends = saturate(min(i.uv.y, 1 - i.uv.y) * 8);
                return half4(i.color.rgb, i.color.a * width * ends);
            }
            ENDHLSL
        }
    }
}

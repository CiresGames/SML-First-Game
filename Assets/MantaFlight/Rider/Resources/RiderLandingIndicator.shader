Shader "Manta/Rider Landing Indicator"
{
    Properties { _Color ("Color", Color) = (0.1,0.9,0.75,0.32) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(input.normalOS); o.positionWS = TransformObjectToWorld(input.positionOS.xyz); return o; }
            half4 Frag(Varyings input) : SV_Target { float rim = pow(1 - saturate(abs(dot(normalize(input.normalWS), normalize(GetWorldSpaceViewDir(input.positionWS))))), 2); return half4(_Color.rgb * (0.8 + rim * 0.4), _Color.a * (0.3 + rim * 0.7)); }
            ENDHLSL
        }
    }
}

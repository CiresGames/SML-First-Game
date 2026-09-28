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
            float4 _RainViewerVelocity;
            float _RainExposure,_RainLength;
            // UV + UV2 occupy TEXCOORD0; Center + AgePercent occupy TEXCOORD1.
            struct A {float4 vertex:POSITION; float4 color:COLOR; float4 uv:TEXCOORD0; float4 center:TEXCOORD1; float3 velocity:TEXCOORD2;};
            struct V {float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0;};
            V Vert(A i)
            {
                V o;
                float3 world=TransformObjectToWorld(i.vertex.xyz);
                float3 center=i.center.xyz; // World-space particle simulation.
                float size=length(world-center)*1.41421356;
                float3 view=normalize(GetCameraPositionWS()-center);
                float3 relative=i.velocity-_RainViewerVelocity.xyz;
                float3 projected=relative-view*dot(relative,view);
                float speed=length(projected);
                float3 down=-UNITY_MATRIX_I_V._m01_m11_m21;
                float3 axis=speed>.01 ? projected/max(speed,.001) : down;
                float3 across=normalize(cross(view,axis));
                float streakLength=max(size,size*_RainLength+speed*_RainExposure);
                world=center+axis*((i.uv.y-.5)*streakLength)+across*((i.uv.x-.5)*size);
                o.position=TransformWorldToHClip(world);o.color=i.color;o.uv=i.uv.xy;return o;
            }
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

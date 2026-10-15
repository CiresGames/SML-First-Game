Shader "Hidden/Manta/GPU Cloud Composite"
{
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off ZTest Always
 Pass { HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
 TEXTURE2D(_Clouds);TEXTURE2D(_CloudDepth);TEXTURE2D(_SceneDepth);
 float4 _CloudTexel;float4x4 _InvVP;float4 _CloudCamera;
 half4 Frag(Varyings i):SV_Target
 {
   float2 uv=i.texcoord;float raw=SAMPLE_TEXTURE2D_LOD(_SceneDepth,sampler_PointClamp,uv,0).r;
   float scene=abs(raw-UNITY_RAW_FAR_CLIP_VALUE)<.00001 ? _CloudCamera.w : distance(ComputeWorldSpacePosition(uv,raw,_InvVP),_CloudCamera.xyz);
   float2 grid=uv*_CloudTexel.zw-.5;float2 baseUV=(floor(grid)+.5)*_CloudTexel.xy;float2 f=frac(grid);
   float4 c=0;float total=0;
   [unroll]for(int y=0;y<2;y++)[unroll]for(int x=0;x<2;x++){
     float2 q=baseUV+float2(x,y)*_CloudTexel.xy;
     float d=SAMPLE_TEXTURE2D_LOD(_CloudDepth,sampler_PointClamp,q,0).g;
     float weight=(x ? f.x : 1-f.x)*(y ? f.y : 1-f.y)*exp(-abs(d-scene)/max(5,scene*.02));
     c+=SAMPLE_TEXTURE2D_LOD(_Clouds,sampler_PointClamp,q,0)*weight;total+=weight;
   }
   c=total>.0001 ? c/total : float4(0,0,0,1);
   return half4(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_LinearClamp,uv,0).rgb*c.a+c.rgb,1);
 }
 ENDHLSL }
 }
}

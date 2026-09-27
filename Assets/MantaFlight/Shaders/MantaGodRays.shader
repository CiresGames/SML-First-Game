Shader "Hidden/Manta/God Rays"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        TEXTURE2D_X_FLOAT(_SceneDepth);
        TEXTURE2D_X(_Atmosphere);
        TEXTURE2D(_CloudShadowCache);
        float4 _CacheOrigin,_CacheRight,_CacheUp;
        float _CacheSpan;
        float _UseCloudCache;
        float4 _AirSettings,_ScatterSettings,_SolarRadiance,_SunDirection,_AirTexel;
        float4 _CloudCenters[192],_CloudRadii[192];
        int _CloudCount;
        float Depth(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_SceneDepth,sampler_PointClamp,uv,0).r; }
        float CloudTransmission(float3 p,float3 sun)
        {
            float optical=0;
            [loop] for(int j=0;j<_CloudCount;j++)
            {
                float3 q=(p-_CloudCenters[j].xyz)/_CloudRadii[j].xyz;
                float3 d=sun/_CloudRadii[j].xyz;
                float a=dot(d,d), b=dot(q,d), c=dot(q,q)-1;
                float disc=b*b-a*c;
                if(disc>0)
                {
                    float root=sqrt(disc);
                    float entry=max(0,(-b-root)/a), exit=max(0,(-b+root)/a);
                    optical+=max(0,exit-entry)*_CloudCenters[j].w;
                    if(optical>7) return 0; // Negligible direct sunlight; skip deeper occluders.
                }
            }
            return exp(-optical);
        }
        half4 BuildCloudCache(Varyings input):SV_Target
        {
            float tile=floor(input.texcoord.x*8);
            float2 cell=float2(frac(input.texcoord.x*8),input.texcoord.y);
            float3 p=_CacheOrigin.xyz+_CacheSpan*((cell.x-.5)*_CacheRight.xyz+
                (cell.y-.5)*_CacheUp.xyz+(tile/7-.5)*_SunDirection.xyz);
            return CloudTransmission(p,_SunDirection.xyz).xxxx;
        }
        float CachedCloudTransmission(float3 p)
        {
            float3 d=(p-_CacheOrigin.xyz)/_CacheSpan;
            float2 cell=saturate(float2(dot(d,_CacheRight.xyz),dot(d,_CacheUp.xyz))+.5);
            // Keep bilinear filtering inside each atlas slice.
            cell=clamp(cell,.5/128,1-.5/128);
            float z=saturate(dot(d,_SunDirection.xyz)+.5)*7;
            float lo=floor(z),hi=min(7,lo+1);
            float a=SAMPLE_TEXTURE2D_LOD(_CloudShadowCache,sampler_LinearClamp,float2((lo+cell.x)/8,cell.y),0).r;
            float b=SAMPLE_TEXTURE2D_LOD(_CloudShadowCache,sampler_LinearClamp,float2((hi+cell.x)/8,cell.y),0).r;
            return lerp(a,b,frac(z));
        }
        half4 Scatter(Varyings input):SV_Target
        {
            float2 uv=input.texcoord;
            float raw=Depth(uv);
            #if !UNITY_REVERSED_Z
            raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
            #endif
            float3 origin=GetCameraPositionWS();
            float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
            float3 delta=end-origin; float lengthRay=min(length(delta),_AirSettings.z);
            float3 direction=normalize(delta);
            int count=(int)_AirSettings.w;
            float jitter=frac(52.9829189*frac(dot(input.positionCS.xy,float2(.06711056,.00583715))));
            float g=clamp(_ScatterSettings.x,0,.85);
            float cosine=dot(direction,normalize(_SunDirection.xyz));
            // Henyey-Greenstein phase function, normalized over solid angle.
            float phase=(1-g*g)/(4*PI*pow(max(.001,1+g*g-2*g*cosine),1.5));
            float transmission=1; float3 radiance=0;
            [loop] for(int i=0;i<count;i++)
            {
                // Quadratic spacing resolves nearby shadow boundaries without raising the step count.
                float nearT=(float)i/count,farT=(float)(i+1)/count;
                float start=nearT*nearT*lengthRay;
                float stride=(farT*farT-nearT*nearT)*lengthRay;
                float3 p=origin+direction*(start+jitter*stride);
                float extinction=_AirSettings.x*exp(-max(0,p.y)/max(1,_AirSettings.y));
                float stepT=exp(-extinction*stride);
                float shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                shadow=lerp(shadow,1,GetMainLightShadowFade(p));
                // Analytic optical depth through an exponential atmosphere toward the sun.
                float solarAir=exp(-extinction*_AirSettings.y/max(.08,_SunDirection.y));
                float lightT=solarAir*(_UseCloudCache>.5 ? CachedCloudTransmission(p) : CloudTransmission(p,_SunDirection.xyz));
                radiance+=transmission*(1-stepT)*_ScatterSettings.z*phase*_SolarRadiance.rgb*shadow*lightT;
                transmission*=stepT;
            }
            return half4(radiance*_ScatterSettings.y,transmission);
        }
        half4 Composite(Varyings input):SV_Target
        {
            float2 uv=input.texcoord;
            half4 scene=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,uv,0);
            float center=LinearEyeDepth(Depth(uv),_ZBufferParams);
            float2 grid=uv*_AirTexel.zw-.5;
            float2 baseUV=(floor(grid)+.5)*_AirTexel.xy;
            float2 f=frac(grid);
            float4 air=0; float sum=0;
            [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
            {
                float2 tap=baseUV+float2(x,y)*_AirTexel.xy;
                float sampleDepth=LinearEyeDepth(Depth(tap),_ZBufferParams);
                float weight=(x ? f.x : 1-f.x)*(y ? f.y : 1-f.y);
                weight*=exp(-abs(sampleDepth-center)/max(1,center*.02))+.0001;
                air+=SAMPLE_TEXTURE2D_X_LOD(_Atmosphere,sampler_PointClamp,tap,0)*weight;
                sum+=weight;
            }
            air/=max(sum,.00001);
            return half4(scene.rgb*air.a+air.rgb,scene.a);
        }
        ENDHLSL
        Pass
        {
            Name "Shadowed single scattering"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Scatter
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "Depth aware composite"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass
        {
            Name "Sun aligned cloud shadow cache"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment BuildCloudCache
            ENDHLSL
        }
    }
}

#ifndef MANTA_GPU_FIELD
#define MANTA_GPU_FIELD
TEXTURE2D(_GpuWeather); SAMPLER(sampler_GpuWeather);
TEXTURE3D(_GpuShape); SAMPLER(sampler_GpuShape);
TEXTURE3D(_GpuDetail); SAMPLER(sampler_GpuDetail);
float4 _Region, _Layers, _Weather, _FieldShape;
float CoverageFromNoise(float n)
{
    float threshold=1-lerp(.12,.92,saturate(_Weather.x));
    return smoothstep(threshold-.16,threshold+.16,n);
}
float RegionalCoverage(float2 xz)
{
    return CoverageFromNoise(SAMPLE_TEXTURE2D_LOD(_GpuWeather,sampler_GpuWeather,(xz-_Region.yz)*_Region.x,0).r);
}
float FieldDensity(float3 p,float detail)
{
    float h=(p.y-_Layers.x)/(_Layers.y*_FieldShape.z);
    float high=(p.y-_Layers.z)/_Layers.w;
    if((h<=0 || h>=1) && (high<=0 || high>=1))return 0;
    float2 region=SAMPLE_TEXTURE2D_LOD(_GpuWeather,sampler_GpuWeather,(p.xz-_Region.yz)*_Region.x,0).rg;
    float coverage=CoverageFromNoise(region.r);
    if(coverage<.001)return 0;
    float3 uv=(p-float3(_Region.y,0,_Region.z))/max(100,_FieldShape.x);
    uv+=float3(0,_Region.w*.0006,0);
    float4 n=SAMPLE_TEXTURE3D_LOD(_GpuShape,sampler_GpuShape,uv,0);
    float d=0;
    if(h>0 && h<1)
    {
        // Regional convection varies the cloud top without moving its base.
        // This breaks up the uniform deck while retaining conservative layer bounds.
        h/=lerp(.55,1,smoothstep(.15,.85,region.g));
        // A flatter condensation base and rounded, variable-height convective tops.
        float profile=smoothstep(0,.08,h)*(1-smoothstep(.52,1,h));
        // Threshold the low-frequency 3D field: this creates separate rounded
        // towers and deep valleys instead of a flat, solid slab.
        float threshold=lerp(.85,lerp(.43,.28,saturate(_Weather.z)),coverage);
        d=saturate((n.r*profile-threshold)*3.2);
    }
    if(high>0 && high<1)
        d=max(d,saturate((n.r-.35)*3)*smoothstep(0,.2,high)*(1-smoothstep(.5,1,high))*coverage*.2);
    if(d<=0)return 0;
    if(detail>0){float erosion=SAMPLE_TEXTURE3D_LOD(_GpuDetail,sampler_GpuDetail,uv*7.3,0).r;d=max(0,d-(1-erosion)*.25*_FieldShape.w*(1-d)*detail);}
    return d*_FieldShape.y*_Weather.y;
}
#endif

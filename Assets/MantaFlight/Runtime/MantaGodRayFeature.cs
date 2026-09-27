using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace MantaFlight
{
    public sealed class MantaGodRayFeature : ScriptableRendererFeature
    {
        public Material material;
        public bool cacheCloudShadows=true;
        [Range(2,4)] public int resolutionDivisor=3;
        AtmospherePass pass;
        public override void Create() => pass=new AtmospherePass();
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
        {
            var sky=MantaDayNightCycle.Active;
            if(!material || !sky || !sky.isActiveAndEnabled || sky.RayStrength<=.001f) return;
            if(renderingData.cameraData.cameraType!=CameraType.Game && renderingData.cameraData.cameraType!=CameraType.SceneView) return;
            if(renderingData.cameraData.renderType!=CameraRenderType.Base) return;
            pass.material=material; pass.cached=cacheCloudShadows; pass.divisor=Mathf.Clamp(resolutionDivisor,2,4); renderer.EnqueuePass(pass);
        }
        sealed class AtmospherePass : ScriptableRenderPass
        {
            public Material material;
            public bool cached;
            public int divisor;
            readonly Vector4[] centers=new Vector4[192],radii=new Vector4[192];
            readonly List<MantaCloudLayerVolume> banks=new List<MantaCloudLayerVolume>(64);
            sealed class Data
            {
                public TextureHandle source,depth,air,shadow,cache;
                public Material material;
                public MaterialPropertyBlock block;
                public int index;
            }
            public AtmospherePass()
            {
                // Composite air behind transparent cloud volumes.
                renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture=true;
            }
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var sky=MantaDayNightCycle.Active;
                if(!sky || !material) return;
                var r=frame.Get<UniversalResourceData>();
                if(r.isActiveTargetBackBuffer || !r.cameraDepthTexture.IsValid()) return;
                var desc=r.activeColorTexture.GetDescriptor(graph);
                desc.msaaSamples=MSAASamples.None; desc.depthBufferBits=DepthBits.None; desc.clearBuffer=false;
                var full=desc;
                desc.width=Mathf.Max(1,desc.width/divisor); desc.height=Mathf.Max(1,desc.height/divisor);
                desc.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                desc.name="Manta atmospheric scattering";
                var air=graph.CreateTexture(desc);
                var cacheDesc=desc;
                cacheDesc.width=1024; cacheDesc.height=128;
                cacheDesc.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
                cacheDesc.name="Manta sun aligned cloud shadow cache";
                var cache=graph.CreateTexture(cacheDesc);
                full.name="Manta atmosphere composite"; var output=graph.CreateTexture(full);
                var block=new MaterialPropertyBlock();
                float fade=Mathf.Clamp01(sky.RayStrength/Mathf.Max(.001f,sky.rayIntensity));
                block.SetVector("_AirSettings",new Vector4(sky.airExtinction*fade,sky.airHeight,sky.rayDistance,Mathf.Clamp(sky.raySamples,16,64)));
                block.SetVector("_SunDirection",sky.SunDirection);
                block.SetVector("_ScatterSettings",new Vector4(sky.raySpread,sky.RayStrength,.92f,0));
                block.SetColor("_SolarRadiance",sky.RayColor);
                block.SetFloat("_UseCloudCache",cached ? 1 : 0);
                var camera=frame.Get<UniversalCameraData>().camera;
                var sun=sky.SunDirection.normalized;
                var right=Vector3.Cross(Mathf.Abs(sun.y)>.95f ? Vector3.forward : Vector3.up,sun).normalized;
                var up=Vector3.Cross(sun,right);
                block.SetVector("_CacheOrigin",camera.transform.position);
                block.SetVector("_CacheRight",right); block.SetVector("_CacheUp",up);
                block.SetFloat("_CacheSpan",sky.rayDistance*2.1f+64);
                // Three lobes per formation leave narrower, irregular sunlit gaps.
                // Still analytic: no nested cloud-noise ray march for each air sample.
                int count=0;
                banks.Clear();
                if(sky.clouds) sky.clouds.GetComponentsInChildren(false,banks);
                if(sky.clouds)
                foreach(var bank in banks)
                {
                    if(count>=192) break;
                    var t=bank.transform; var size=t.lossyScale;
                    for(int l=0;l<3;l++)
                    {
                        var center=bank.lobeCenters[l]; var radius=bank.lobeRadii[l];
                        var position=t.TransformPoint(new Vector3(center.x*sky.clouds.footprint,center.y*sky.clouds.thickness,center.z*sky.clouds.footprint));
                        centers[count]=new Vector4(position.x,position.y,position.z,
                            sky.clouds.density*bank.density*bank.edgeFade*Mathf.Lerp(0,.012f,sky.clouds.coverage));
                        radii[count]=new Vector4(Mathf.Max(1,size.x*radius.x*sky.clouds.footprint),
                            Mathf.Max(1,size.y*radius.y*sky.clouds.thickness),Mathf.Max(1,size.z*radius.z*sky.clouds.footprint),0);
                        count++;
                    }
                }
                block.SetVectorArray("_CloudCenters",centers); block.SetVectorArray("_CloudRadii",radii);
                block.SetInt("_CloudCount",count);
                Add(graph,r.activeColorTexture,r.cameraDepthTexture,air,r.mainShadowsTexture,cache,cache,2,block);
                Add(graph,r.activeColorTexture,r.cameraDepthTexture,air,r.mainShadowsTexture,cache,air,0,block);
                Add(graph,r.activeColorTexture,r.cameraDepthTexture,air,r.mainShadowsTexture,cache,output,1,new MaterialPropertyBlock());
                r.cameraColor=output;
            }
            void Add(RenderGraph graph,TextureHandle source,TextureHandle depth,TextureHandle air,TextureHandle shadow,
                TextureHandle cache,TextureHandle target,int index,MaterialPropertyBlock block)
            {
                using(var builder=graph.AddRasterRenderPass<Data>(index==2 ? "Manta cloud shadow cache" : index==0 ? "Manta single scattering" : "Manta depth-aware atmosphere",out var d))
                {
                    d.source=source; d.depth=depth; d.air=air; d.shadow=shadow; d.cache=cache; d.index=index; d.material=material; d.block=block;
                    if(index!=2) builder.UseTexture(depth,AccessFlags.Read);
                    if(index==0) builder.UseTexture(cache,AccessFlags.Read);
                    if(index==0 && shadow.IsValid()) builder.UseTexture(shadow,AccessFlags.Read);
                    if(index==1) { builder.UseTexture(source,AccessFlags.Read); builder.UseTexture(air,AccessFlags.Read); }
                    builder.SetRenderAttachment(target,0,AccessFlags.Write);
                    builder.SetRenderFunc(static (Data d,RasterGraphContext context)=>
                    {
                        if(d.index!=2) d.block.SetTexture("_SceneDepth",(Texture)d.depth);
                        if(d.index==0) d.block.SetTexture("_CloudShadowCache",(Texture)d.cache);
                        if(d.index==0 && d.shadow.IsValid()) d.block.SetTexture("_MainLightShadowmapTexture",(Texture)d.shadow);
                        if(d.index==1)
                        {
                            d.block.SetTexture("_BlitTexture",(Texture)d.source);
                            d.block.SetTexture("_Atmosphere",(Texture)d.air);
                            var texture=(Texture)d.air;
                            d.block.SetVector("_AirTexel",new Vector4(1f/texture.width,1f/texture.height,texture.width,texture.height));
                        }
                        d.block.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));
                        context.cmd.DrawProcedural(Matrix4x4.identity,d.material,d.index,MeshTopology.Triangles,3,1,d.block);
                    });
                }
            }
        }
    }
}

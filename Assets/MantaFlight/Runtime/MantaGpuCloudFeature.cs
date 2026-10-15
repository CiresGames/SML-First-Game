using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace MantaFlight
{
    public sealed class MantaGpuCloudFeature : ScriptableRendererFeature
    {
        public Material compositeMaterial;
        static MantaGpuCloudFeature instance;
        public static bool Available => instance && instance.isActive && instance.compositeMaterial && instance.compositeMaterial.shader.isSupported;
        CloudPass pass;
        public override void Create(){pass?.Dispose();pass=new CloudPass();instance=this;}
        protected override void Dispose(bool disposing){pass?.Dispose();if(instance==this)instance=null;}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            var field=MantaGpuWeather.Active;
            if(!Available || !field || !field.Ready || data.cameraData.renderType!=CameraRenderType.Base || data.cameraData.xr.enabled)return;
            if(!MantaGpuWeather.SupportsCamera(data.cameraData.camera))return;
            if(data.cameraData.cameraType!=CameraType.Game && data.cameraData.cameraType!=CameraType.SceneView)return;
            field.EnsureMap();pass.material=compositeMaterial;renderer.EnqueuePass(pass);
        }
        sealed class History
        {
            public RTHandle[] color=new RTHandle[2],depth=new RTHandle[2];public int width,height,index,frame=-100;
            public Matrix4x4 vp;public Vector3 camera,forward,wind;public float coverage;public int seed;public float time,flash;public Color lighting;
            public void Dispose(){for(int i=0;i<2;i++){color[i]?.Release();depth[i]?.Release();color[i]=depth[i]=null;}}
            public void Ensure(int w,int h)
            {
                if(w==width && h==height && color[0]!=null)return;Dispose();width=w;height=h;frame=-100;
                for(int i=0;i<2;i++){
                    color[i]=RTHandles.Alloc(w,h,colorFormat:GraphicsFormat.R16G16B16A16_SFloat,enableRandomWrite:true,filterMode:FilterMode.Bilinear,name:"Manta cloud history "+i);
                    depth[i]=RTHandles.Alloc(w,h,colorFormat:GraphicsFormat.R32G32_SFloat,enableRandomWrite:true,filterMode:FilterMode.Point,name:"Manta cloud depth "+i);
                }
            }
        }
        sealed class CloudPass : ScriptableRenderPass
        {
            public Material material;
            readonly Dictionary<Camera,History> histories=new Dictionary<Camera,History>();
            readonly List<Camera> stale=new List<Camera>();
            public CloudPass(){renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents+1);ConfigureInput(ScriptableRenderPassInput.Depth);requiresIntermediateTexture=true;}
            public void Dispose(){foreach(var h in histories.Values)h.Dispose();histories.Clear();}
            sealed class ComputeData
            {
                public ComputeShader shader;public int kernel,width,height;
                public TextureHandle scene,current,meta,history,historyDepth,output,outputDepth,map,shape,detail,lightCache;
                public Matrix4x4 inverse,previous;public Vector4 camera,size,region,layers,weather,fieldShape,sun,sunColor,ambient,trace,historySettings,wind,lightning,lightOrigin,lightExtent,detailLod;
                public Vector4[] receiverPlanes;
            }
            sealed class CompositeData{public Material material;public TextureHandle source,clouds,meta,depth;public Matrix4x4 inverse;public Vector4 camera,texel;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var field=MantaGpuWeather.Active;if(!field || !field.Ready)return;
                var resources=frame.Get<UniversalResourceData>();var cameraData=frame.Get<UniversalCameraData>();var camera=cameraData.camera;
                if(resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())return;
                // Reclaim cameras destroyed by previews or scene changes.
                stale.Clear();foreach(var pair in histories)if(!pair.Key)stale.Add(pair.Key);
                foreach(var staleCamera in stale){histories[staleCamera].Dispose();histories.Remove(staleCamera);}
                if(!histories.TryGetValue(camera,out var history)){history=new History();histories.Add(camera,history);}
                var full=resources.activeColorTexture.GetDescriptor(graph);var low=full;
                int divisor=Mathf.Clamp(field.resolutionDivisor,1,4);
                int w=Mathf.Max(1,full.width/divisor),h=Mathf.Max(1,full.height/divisor);history.Ensure(w,h);
                low.width=w;low.height=h;low.msaaSamples=MSAASamples.None;low.depthBufferBits=DepthBits.None;low.enableRandomWrite=true;low.clearBuffer=false;low.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;low.name="Manta cloud radiance";
                var current=graph.CreateTexture(low);low.colorFormat=GraphicsFormat.R32G32_SFloat;low.name="Manta cloud distances";var meta=graph.CreateTexture(low);
                int next=1-history.index;var oldColor=graph.ImportTexture(history.color[history.index]);var oldDepth=graph.ImportTexture(history.depth[history.index]);var resolved=graph.ImportTexture(history.color[next]);var resolvedDepth=graph.ImportTexture(history.depth[next]);
                // External weather/noise textures are explicitly imported for graph dependencies.
                var map=graph.ImportTexture(field.WeatherHandle);
                // Wrappers belong to the weather component and outlive graph execution.
                var shape=graph.ImportTexture(field.ShapeHandle);var detail=graph.ImportTexture(field.DetailHandle);
                var vp=GL.GetGPUProjectionMatrix(camera.projectionMatrix,true)*camera.worldToCameraMatrix;var inverse=vp.inverse;
                var sky=MantaDayNightCycle.Active;float daylight=sky ? sky.Daylight : 1;
                Vector3 sun=sky ? (daylight>.05f ? sky.SunDirection : -sky.SunDirection) : Vector3.up;
                Color key=sky ? Color.Lerp(sky.moonlightColor*.18f,sky.sun.color*1.25f,daylight) : Color.white;
                key=Color.Lerp(key,key*.4f,field.Clouds.overcast);
                Color ambient=Color.Lerp(new Color(.025f,.04f,.075f),new Color(.25f,.32f,.43f),daylight);
                Vector4 lightning=MantaStormLightning.CloudFlash;
                bool valid=history.frame>=0 && history.seed==field.Seed && Vector3.Distance(history.camera,camera.transform.position)<150 && Vector3.Distance(history.wind,field.WindOffset)<300 && Vector3.Dot(history.forward,camera.transform.forward)>.85f && Mathf.Abs(history.coverage-field.Clouds.coverage)<.08f && lightning.w<=0 && history.flash<=0 && Vector4.Distance(history.lighting,key)<.1f && Time.realtimeSinceStartup-history.time<.5f;
                var lightDesc=low;lightDesc.width=64;lightDesc.height=32;lightDesc.slices=64;lightDesc.dimension=TextureDimension.Tex3D;lightDesc.colorFormat=GraphicsFormat.R16_SFloat;lightDesc.name="Manta volumetric sunlight cache";
                var lightCache=graph.CreateTexture(lightDesc);
                using(var builder=graph.AddComputePass<ComputeData>("Manta shared volumetric sunlight",out var d))
                {
                    Fill(d,field,w,h,inverse,vp,camera,sun,key,ambient,lightning);d.kernel=field.cloudCompute.FindKernel("BuildLightCache");d.lightCache=lightCache;d.map=map;d.shape=shape;d.detail=detail;
                    var planes=GeometryUtility.CalculateFrustumPlanes(camera);d.receiverPlanes=new Vector4[6];
                    // Sky volumes deliberately extend beyond the opaque camera
                    // far clip; their lighting must use the same cloud reach.
                    planes[5]=new Plane(-camera.transform.forward,camera.transform.position+camera.transform.forward*field.viewDistance);
                    float halo=new Vector3(d.lightExtent.x/64,d.lightExtent.y/32,d.lightExtent.z/64).magnitude+Mathf.Max(0,field.Clouds.cullingBias);
                    for(int i=0;i<6;i++)d.receiverPlanes[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance+halo);
                    builder.UseTexture(map);builder.UseTexture(shape);builder.UseTexture(detail);builder.UseTexture(lightCache,AccessFlags.Write);
                    builder.SetRenderFunc(static(ComputeData d,ComputeGraphContext ctx)=>{
                        Bind(d,ctx);ctx.cmd.SetComputeVectorArrayParam(d.shader,"_ReceiverPlanes",d.receiverPlanes);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuWeather",d.map);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuShape",d.shape);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuDetail",d.detail);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_LightWrite",d.lightCache);ctx.cmd.DispatchCompute(d.shader,d.kernel,16,8,16);
                    });
                }
                using(var builder=graph.AddComputePass<ComputeData>("Manta GPU cloud ray march",out var d))
                {
                    Fill(d,field,w,h,inverse,vp,camera,sun,key,ambient,lightning);d.kernel=field.cloudCompute.FindKernel("TraceClouds");d.scene=resources.cameraDepthTexture;d.output=current;d.outputDepth=meta;d.map=map;d.shape=shape;d.detail=detail;
                    d.lightCache=lightCache;builder.UseTexture(lightCache);builder.UseTexture(d.scene);builder.UseTexture(map);builder.UseTexture(shape);builder.UseTexture(detail);builder.UseTexture(current,AccessFlags.Write);builder.UseTexture(meta,AccessFlags.Write);
                    builder.SetRenderFunc(static(ComputeData d,ComputeGraphContext ctx)=>{
                        Bind(d,ctx);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_LightCache",d.lightCache);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_SceneDepth",d.scene);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuWeather",d.map);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuShape",d.shape);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_GpuDetail",d.detail);Dispatch(d,ctx);
                    });
                }
                using(var builder=graph.AddComputePass<ComputeData>("Manta cloud temporal reconstruction",out var d))
                {
                    Fill(d,field,w,h,inverse,history.vp,camera,sun,key,ambient,lightning);d.kernel=field.cloudCompute.FindKernel("ResolveHistory");d.current=current;d.meta=meta;d.history=oldColor;d.historyDepth=oldDepth;d.output=resolved;d.outputDepth=resolvedDepth;d.historySettings=new Vector4(valid ? field.historyWeight : 0,0,0,0);d.wind=field.WindOffset-history.wind;
                    builder.UseTexture(current);builder.UseTexture(meta);builder.UseTexture(oldColor);builder.UseTexture(oldDepth);builder.UseTexture(resolved,AccessFlags.Write);builder.UseTexture(resolvedDepth,AccessFlags.Write);
                    builder.SetRenderFunc(static(ComputeData d,ComputeGraphContext ctx)=>{
                        Bind(d,ctx);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_Current",d.current);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_CurrentDepth",d.meta);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_History",d.history);ctx.cmd.SetComputeTextureParam(d.shader,d.kernel,"_HistoryDepth",d.historyDepth);Dispatch(d,ctx);
                    });
                }
                full.msaaSamples=MSAASamples.None;full.depthBufferBits=DepthBits.None;full.name="Manta cloud composite";full.clearBuffer=false;var output=graph.CreateTexture(full);
                using(var builder=graph.AddRasterRenderPass<CompositeData>("Manta cloud depth aware upscale",out var d))
                {
                    d.material=material;d.source=resources.activeColorTexture;d.clouds=resolved;d.meta=resolvedDepth;d.depth=resources.cameraDepthTexture;d.inverse=inverse;d.camera=new Vector4(camera.transform.position.x,camera.transform.position.y,camera.transform.position.z,field.viewDistance);d.texel=new Vector4(1f/w,1f/h,w,h);
                    builder.UseTexture(d.source);builder.UseTexture(d.clouds);builder.UseTexture(d.meta);builder.UseTexture(d.depth);builder.SetRenderAttachment(output,0,AccessFlags.Write);
                    builder.SetRenderFunc(static(CompositeData d,RasterGraphContext ctx)=>{
                        var block=new MaterialPropertyBlock();block.SetTexture("_BlitTexture",(Texture)d.source);block.SetTexture("_Clouds",(Texture)d.clouds);block.SetTexture("_CloudDepth",(Texture)d.meta);block.SetTexture("_SceneDepth",(Texture)d.depth);block.SetMatrix("_InvVP",d.inverse);block.SetVector("_CloudCamera",d.camera);block.SetVector("_CloudTexel",d.texel);block.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));ctx.cmd.DrawProcedural(Matrix4x4.identity,d.material,0,MeshTopology.Triangles,3,1,block);
                    });
                }
                resources.cameraColor=output;history.index=next;history.vp=vp;history.camera=camera.transform.position;history.forward=camera.transform.forward;history.wind=field.WindOffset;history.coverage=field.Clouds.coverage;history.seed=field.Seed;history.frame=Time.frameCount;history.time=Time.realtimeSinceStartup;history.flash=lightning.w;history.lighting=key;
            }


            static void Fill(ComputeData d,MantaGpuWeather f,int w,int h,Matrix4x4 inverse,Matrix4x4 previous,Camera cam,Vector3 sun,Color key,Color ambient,Vector4 flash)
            {
                d.shader=f.cloudCompute;d.width=w;d.height=h;d.inverse=inverse;d.previous=previous;d.camera=cam.transform.position;d.size=new Vector4(w,h,0,0);d.region=f.Region;d.layers=f.Layers;d.weather=f.Weather;d.fieldShape=f.Shape;d.sun=sun;d.sunColor=key;d.ambient=ambient;d.trace=new Vector4(f.viewDistance,f.samples,Time.frameCount%1024,0);d.historySettings=Vector4.zero;d.wind=Vector4.zero;d.lightning=flash;
                d.detailLod=new Vector4(Mathf.Max(0,f.detailFadeStart),Mathf.Max(f.detailFadeStart+1,f.detailFadeEnd),0,0);
                float span=f.viewDistance*2;d.lightOrigin=new Vector4(cam.transform.position.x-span*.5f,f.mainLayer.x,cam.transform.position.z-span*.5f,0);d.lightExtent=new Vector4(span,Mathf.Max(f.mainLayer.x+f.mainLayer.y,f.highLayer.x+f.highLayer.y)-f.mainLayer.x,span,0);
            }
            static void Bind(ComputeData d,ComputeGraphContext c)
            {
                var s=d.shader;c.cmd.SetComputeMatrixParam(s,"_InvVP",d.inverse);c.cmd.SetComputeMatrixParam(s,"_PreviousVP",d.previous);
                c.cmd.SetComputeVectorParam(s,"_DetailLod",d.detailLod);
                c.cmd.SetComputeVectorParam(s,"_LightOrigin",d.lightOrigin);c.cmd.SetComputeVectorParam(s,"_LightExtent",d.lightExtent);
                c.cmd.SetComputeVectorParam(s,"_Camera",d.camera);c.cmd.SetComputeVectorParam(s,"_Size",d.size);c.cmd.SetComputeVectorParam(s,"_Region",d.region);c.cmd.SetComputeVectorParam(s,"_Layers",d.layers);c.cmd.SetComputeVectorParam(s,"_Weather",d.weather);c.cmd.SetComputeVectorParam(s,"_FieldShape",d.fieldShape);c.cmd.SetComputeVectorParam(s,"_Sun",d.sun);c.cmd.SetComputeVectorParam(s,"_SunColor",d.sunColor);c.cmd.SetComputeVectorParam(s,"_Ambient",d.ambient);c.cmd.SetComputeVectorParam(s,"_Trace",d.trace);c.cmd.SetComputeVectorParam(s,"_HistorySettings",d.historySettings);c.cmd.SetComputeVectorParam(s,"_WindDelta",d.wind);c.cmd.SetComputeVectorParam(s,"_Lightning",d.lightning);
            }
            static void Dispatch(ComputeData d,ComputeGraphContext c){c.cmd.SetComputeTextureParam(d.shader,d.kernel,"_CloudWrite",d.output);c.cmd.SetComputeTextureParam(d.shader,d.kernel,"_DepthWrite",d.outputDepth);c.cmd.DispatchCompute(d.shader,d.kernel,(d.width+7)/8,(d.height+7)/8,1);}
        }
    }
}



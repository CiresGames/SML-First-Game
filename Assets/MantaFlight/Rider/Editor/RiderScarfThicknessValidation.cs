using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    public static class RiderScarfThicknessValidation
    {
        public static string Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required.");
            var root=PrefabUtility.LoadPrefabContents("Assets/MantaFlight/Rider/Prefabs/RiderCharacter.prefab");
            var checks=new List<string>();
            void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add("PASS "+name);}
            try
            {
                var scarf=root.GetComponent<RiderScarf>();scarf.Initialize();
                var mesh=scarf.fabric.sharedMesh;
                int count=(RiderScarf.RenderRows+1)*(RiderScarf.Columns+1);
                Check(mesh.vertexCount==count*2,"Two complete fabric surfaces");
                var edges=new Dictionary<(int,int),int>();var tris=mesh.triangles;
                for(int i=0;i<tris.Length;i+=3)for(int j=0;j<3;j++)
                {
                    int a=tris[i+j],b=tris[i+(j+1)%3];var key=(Mathf.Min(a,b),Mathf.Max(a,b));
                    edges.TryGetValue(key,out int n);edges[key]=n+1;
                }
                foreach(var n in edges.Values)CheckEdge(n);
                checks.Add("PASS Closed fabric edges, no open borders");
                float error=0;
                foreach(var wind in new[]{Vector3.zero,Vector3.right*15,Vector3.left*15})
                {
                    for(int frame=0;frame<300;frame++)scarf.Simulate(1f/90,wind,Vector3.zero);
                    var v=mesh.vertices;
                    for(int i=0;i<count;i++)
                    {
                        float d=Vector3.Distance(scarf.fabric.transform.TransformPoint(v[i]),scarf.fabric.transform.TransformPoint(v[i+count]));
                        error=Mathf.Max(error,Mathf.Abs(d-scarf.thickness));
                    }
                    Check(float.IsFinite(mesh.bounds.size.sqrMagnitude) && mesh.bounds.size.sqrMagnitude<10,"Stable cloth under wind "+wind);
                }
                Check(error<.0001f,"Thickness stays constant during wind deformation");
                scarf.ResetCloth();
                for(int frame=0;frame<900;frame++)scarf.Simulate(1f/90,Vector3.right*15,Vector3.zero);
                Vector3 tip=scarf.Tip,previousTip=tip;float motion=0,maxSpeed=0;
                for(int frame=0;frame<450;frame++)
                {
                    scarf.Simulate(1f/90,Vector3.right*15,Vector3.zero);
                    motion=Mathf.Max(motion,Vector3.Distance(tip,scarf.Tip));
                    maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(previousTip,scarf.Tip)*90);previousTip=scarf.Tip;
                }
                Check(motion<.06f && maxSpeed<.2f,"Steady wind produces slow, restrained motion: "+motion.ToString("F3")+" m, "+maxSpeed.ToString("F3")+" m/s");
                Check((scarf.Tip-scarf.Anchor).x>.5f,"Steady scarf still indicates wind direction");
                for(int frame=0;frame<900;frame++)scarf.Simulate(1f/90,Vector3.left*15,Vector3.zero);
                Check((scarf.Tip-scarf.Anchor).x<-.5f,"Damped scarf follows wind reversal");
                Vector3 RenderTip()
                {
                    var v=mesh.vertices;int i=RiderScarf.RenderRows*(RiderScarf.Columns+1);
                    return scarf.fabric.transform.TransformPoint((v[i]+v[i+1]+v[i+count]+v[i+count+1])*.25f);
                }
                var start=root.transform.position;
                foreach(int fps in new[]{24,60,144})
                {
                    root.transform.position=start;scarf.ResetCloth();
                    Vector3 last=Vector3.zero;float maxStep=0;
                    for(int frame=0;frame<fps*5;frame++)
                    {
                        root.transform.position+=Vector3.forward*(25f/fps);
                        scarf.Simulate(1f/fps,Vector3.right*5,Vector3.forward*25);
                        var relative=RenderTip()-scarf.Anchor;
                        if(frame>fps*3)maxStep=Mathf.Max(maxStep,Vector3.Distance(relative,last));
                        last=relative;
                    }
                    Check(last.z<-.5f && last.magnitude<scarf.length*1.1f,"Rendered scarf follows fast flight at "+fps+" FPS: "+last);
                    Check(maxStep<.04f,"No visible tip jumps at "+fps+" FPS: "+maxStep.ToString("F4")+" m/frame");
                }
                root.transform.position=start;scarf.Simulate(1f/60,Vector3.zero,Vector3.zero);
                Check(Vector3.Distance(RenderTip(),scarf.Anchor)<scarf.length*1.1f,"Rendered history resets on teleport");
                var rotation=root.transform.rotation;
                foreach(var axis in new[]{Vector3.forward,Vector3.right})
                {
                    root.transform.SetPositionAndRotation(start,rotation);scarf.ResetCloth();
                    for(int frame=0;frame<180;frame++)scarf.Simulate(1f/60,Vector3.zero,Vector3.forward*25);
                    float response=0,extent=0;var baseline=RenderTip()-scarf.Anchor;
                    for(int frame=1;frame<=120;frame++)
                    {
                        root.transform.rotation=rotation*Quaternion.AngleAxis(frame*3,axis);
                        root.transform.position+=Vector3.forward*(25f/60);
                        scarf.Simulate(1f/60,Vector3.zero,Vector3.forward*25);
                        var offset=RenderTip()-scarf.Anchor;
                        response=Mathf.Max(response,Vector3.Distance(offset,baseline));
                        extent=Mathf.Max(extent,offset.magnitude);
                    }
                    Check(float.IsFinite(extent) && extent<scarf.length*1.2f && response>.03f,
                        "Controlled response to full "+(axis==Vector3.forward?"roll":"loop")+": "+response.ToString("F3")+" m");
                }
                root.transform.SetPositionAndRotation(start,rotation);scarf.ResetCloth();
                Vector3 glideVelocity=Vector3.zero;
                for(int frame=0;frame<300;frame++)
                {
                    float heading=Mathf.Min(90,frame*.5f);
                    glideVelocity=Quaternion.Euler(0,heading,0)*new Vector3(0,-7,25);
                    root.transform.rotation=rotation*Quaternion.Euler(0,heading,-20*Mathf.Sin(frame*.02f));
                    root.transform.position+=glideVelocity/60;
                    scarf.Simulate(1f/60,Vector3.zero,glideVelocity);
                }
                Check(Vector3.Dot((RenderTip()-scarf.Anchor).normalized,-glideVelocity.normalized)>.8f,
                    "Scarf follows descending glide through a banked turn");
                root.transform.SetPositionAndRotation(start,rotation);
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/MantaFlight/Rider/Models/ScarfCloth.asset");
                var thick=RiderScarf.CreateFabricMesh(scarf.length,scarf.width,scarf.thickness);
                EditorUtility.CopySerialized(thick,asset);Object.DestroyImmediate(thick);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
                checks.Add("PASS Updated shared fabric mesh for prefab and scene");
                return string.Join("\n",checks);
            }
            finally
            {
                // The prefab owns a source settings asset, not a runtime clone.
                root.GetComponent<RiderController>().settings=null;
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        static void CheckEdge(int count){if(count!=2)throw new Exception("Open or non-manifold scarf border");}
    }
}



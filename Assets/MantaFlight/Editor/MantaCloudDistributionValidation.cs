using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaCloudDistributionValidation
    {
        [MenuItem("Manta/Environment/Validate procedural cloud distribution")]
        static void RunMenu()=>Debug.Log(Run());
        public static string Run()
        {
            var source=UnityEngine.Object.FindFirstObjectByType<MantaCloudDistribution>();
            if(!source) throw new Exception("Install procedural cloud distribution first.");
            var go=new GameObject("Cloud distribution validation"){hideFlags=HideFlags.HideAndDontSave};
            var report=new List<string>();
            try
            {
                var test=go.AddComponent<MantaCloudDistribution>();
                EditorUtility.CopySerialized(source,test);
                var randomState=JsonUtility.ToJson(UnityEngine.Random.state);
                var first=test.CreateLayout(); var second=test.CreateLayout();
                int requested=test.cloudCount;
                if(test.altitudeLayers!=null && test.altitudeLayers.Length>0)
                {
                    requested=0;
                    foreach(var layer in test.altitudeLayers) if(layer!=null && layer.enabled) requested+=Mathf.Clamp(layer.count,0,32);
                }
                Check(first.Count==requested,"Requested cloud count fits the scene",report);
                bool same=first.Count==second.Count;
                for(int i=0;i<first.Count && same;i++) same=first[i].position==second[i].position && first[i].size==second[i].size;
                Check(same,"Same seed reproduces positions and sizes exactly",report);
                Check(randomState==JsonUtility.ToJson(UnityEngine.Random.state),"Generation preserves gameplay random state",report);
                test.seed++;
                var different=test.CreateLayout();
                Check(different.Count>0 && different[0].position!=first[0].position,"Changing seed produces a different distribution",report);
                foreach(var p in first)
                {
                    var layer=p.layer<0 ? null : test.altitudeLayers[p.layer];
                    Vector3 local=p.position-(layer==null ? test.regionCenter : new Vector3(test.regionCenter.x,0,test.regionCenter.z));
                    var heights=layer==null ? test.altitudeRange : layer.altitude;
                    float cloudBase=layer==null ? test.minimumCloudBase : 0;
                    Check(Mathf.Abs(local.x)+p.size.x*.5f<=test.regionSize.x*.5f+.01f &&
                        Mathf.Abs(local.z)+p.size.z*.5f<=test.regionSize.y*.5f+.01f,"Cloud fits region",null);
                    Check(local.y>=heights.x && local.y<=heights.y && local.y-p.size.y*.5f>=cloudBase-.01f,"Cloud respects altitude and base clearance",null);
                    Check(local.y-p.size.y*.5f>=test.corridorClearance || Mathf.Abs(local.x)>=test.clearCorridor.x+p.size.x*.5f ||
                        Mathf.Abs(local.z)>=test.clearCorridor.y+p.size.z*.5f,"Cloud respects flight corridor",null);
                }
                Check(true,"All generated clouds respect region, altitude and flight clearance",report);
                for(int i=0;i<first.Count;i++) for(int j=0;j<i;j++)
                {
                    var a=first[i]; var b=first[j];
                    if(a.layer!=b.layer) continue;
                    float radius=(Mathf.Max(a.size.x,a.size.z)+Mathf.Max(b.size.x,b.size.z))*.38f+test.minimumSpacing;
                    var d=a.position-b.position;
                    Check(d.x*d.x+d.z*d.z>=radius*radius-.01f,"Cloud spacing",null);
                }
                Check(true,"All cloud pairs respect spacing",report);
                test.Regenerate(); test.Regenerate();
                Check(test.GetComponentsInChildren<MeshRenderer>().Length==test.GeneratedCount &&
                    test.GetComponentsInChildren<MantaGeneratedCloudRoot>().Length==1,"Regeneration does not duplicate geometry",report);
                Check(test.GetComponentsInChildren<Collider>().Length==0,"Generated clouds have no colliders",report);
                foreach(var marker in test.GetComponentsInChildren<MantaGeneratedCloudRoot>())
                    Check((marker.gameObject.hideFlags&HideFlags.DontSave)==HideFlags.DontSave,"Transient geometry is excluded from serialization",report);
                test.altitudeLayers=Array.Empty<MantaCloudDistribution.CloudLayer>();
                test.cloudCount=0; test.Regenerate();
                Check(test.GeneratedCount==0,"Zero count produces an empty sky",report);
                test.cloudCount=64; test.regionSize=Vector2.one;
                Check(test.CreateLayout().Count==0,"Impossible density settings terminate safely",report);
                test.enabled=false;
                Check(test.transform.childCount==0,"Disabling generator cleans up its geometry",report);
                return string.Join("\n",report);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        static void Check(bool value,string message,List<string> report)
        {
            if(!value) throw new Exception("CLOUD DISTRIBUTION: "+message);
            report?.Add("PASS "+message);
        }
    }
}

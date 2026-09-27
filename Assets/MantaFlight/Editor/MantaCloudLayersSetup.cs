using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaCloudLayersSetup
    {
        [MenuItem("Manta/Environment/Add altitude cloud layers")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var d=Object.FindFirstObjectByType<MantaCloudDistribution>();
            if(!d) throw new System.InvalidOperationException("Add the cloudscape first.");
            Undo.RecordObject(d,"Add altitude cloud layers");
            if(d.altitudeLayers==null || d.altitudeLayers.Length==0)
                d.altitudeLayers=new[] {
                    Layer("Ground mist",8,35,85,new Vector3(450,50,450),new Vector3(950,110,950),.22f,.45f),
                    Layer("Low drifting banks",10,200,380,new Vector3(550,180,500),new Vector3(1150,340,1050),.65f,.8f),
                    Layer("Mid sky billows",10,720,1100,new Vector3(700,350,650),new Vector3(1450,650,1300),1.1f,1.15f),
                    Layer("High sky veils",8,1550,1950,new Vector3(1000,100,800),new Vector3(1900,230,1500),.4f,1.7f)
                };
            d.Regenerate(); EditorUtility.SetDirty(d);
            EditorSceneManager.MarkSceneDirty(d.gameObject.scene); EditorSceneManager.SaveScene(d.gameObject.scene);
            Selection.activeGameObject=d.gameObject;
        }
        static MantaCloudDistribution.CloudLayer Layer(string name,int count,float low,float high,Vector3 min,Vector3 max,float density,float speed)
            => new MantaCloudDistribution.CloudLayer {name=name,count=count,altitude=new Vector2(low,high),minimumSize=min,maximumSize=max,density=density,windSpeed=speed};
    }
}

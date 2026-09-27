using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaCloudWeatherSetup
    {
        public const string Folder="Assets/MantaFlight/Settings/Cloud Weather";
        [MenuItem("Manta/Environment/Set up cloud weather presets")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=MantaPrototypeBuilder.ScenePath) throw new System.InvalidOperationException("Open MantaFlight first.");
            var clouds=Object.FindFirstObjectByType<MantaCloudscape>();
            if(!clouds) throw new System.InvalidOperationException("Add the cloudscape first.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var big=Create("Big clouds","Towering warm cumulus, deep interiors and broad silhouettes.",.67f,1,1,.8f,.55f,0,0,.95f,new Vector3(2.3f,.08f,.65f),.28f);
            var thin=Create("Thin clouds","Translucent high veils with soft, wind-stretched detail.",.49f,1,.13f,.72f,.28f,0,0,.22f,new Vector3(5.5f,.02f,1.5f),.16f);
            var lens=Create("Lenticular clouds","Smooth stacked lenses with quiet internal motion.",.7f,.82f,.28f,.65f,.06f,1,.85f,.7f,new Vector3(.7f,0,.25f),.04f);
            var heavy=Create("Heavy clouds","Broad dense banks with cool, stormy undersides.",.93f,1,.88f,.65f,.25f,0,0,1.5f,new Vector3(4.4f,.06f,1.8f),.52f,true);
            var scattered=Create("Scattered clouds","Small broken cotton clouds and generous blue-sky gaps.",.32f,.68f,.53f,1.9f,.85f,0,0,.65f,new Vector3(3.3f,.1f,.95f),.4f);
            var weather=clouds.GetComponent<MantaCloudWeather>();
            if(!weather)
            {
                weather=Undo.AddComponent<MantaCloudWeather>(clouds.gameObject);
                weather.startingPreset=big;
                weather.sequence=new[] {
                    Stage(big,100,55), Stage(scattered,80,45), Stage(thin,80,50),
                    Stage(lens,100,60), Stage(heavy,90,65)
                };
                Undo.RecordObject(clouds,"Apply starting weather");
                weather.PreviewStartingPreset();
            }
            EditorUtility.SetDirty(weather);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject=clouds.gameObject;
        }
        static CloudWeatherStage Stage(MantaCloudPreset preset,float hold,float transition) =>
            new CloudWeatherStage {preset=preset,holdSeconds=hold,transitionSeconds=transition};
        static MantaCloudPreset Create(string name,string description,float coverage,float spread,float thickness,float scale,
            float erosion,float lenticular,float layers,float density,Vector3 wind,float evolution,bool storm=false)
        {
            string path=Folder+"/"+name+".asset";
            var preset=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(path);
            if(preset) return preset; // User-authored tuning survives repeated setup.
            preset=ScriptableObject.CreateInstance<MantaCloudPreset>(); preset.name=name; preset.description=description;
            preset.appearance=new CloudAppearance {
                coverage=coverage,footprint=spread,thickness=thickness,noiseScale=scale,erosion=erosion,
                lenticular=lenticular,layers=layers,density=density,wind=wind,evolution=evolution,
                sunlight=storm?new Color(.76f,.81f,.88f):new Color(1,.89f,.73f),
                shadow=storm?new Color(.2f,.27f,.37f):new Color(.36f,.48f,.61f)
            };
            AssetDatabase.CreateAsset(preset,path); return preset;
        }
        public static void Preview(string name)
        {
            var preset=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(Folder+"/"+name+".asset");
            var clouds=Object.FindFirstObjectByType<MantaCloudscape>();
            if(!preset || !clouds) throw new System.InvalidOperationException("Set up cloud weather presets first.");
            if(Application.isPlaying) { clouds.GetComponent<MantaCloudWeather>().TransitionTo(preset,8); return; }
            Undo.RecordObject(clouds,"Preview "+name); clouds.SetAppearance(preset.appearance);
            EditorSceneManager.MarkSceneDirty(clouds.gameObject.scene); Selection.activeGameObject=clouds.gameObject;
        }
        [MenuItem("Manta/Environment/Preview clouds/Big clouds")] static void Big()=>Preview("Big clouds");
        [MenuItem("Manta/Environment/Preview clouds/Thin clouds")] static void Thin()=>Preview("Thin clouds");
        [MenuItem("Manta/Environment/Preview clouds/Lenticular clouds")] static void Lenticular()=>Preview("Lenticular clouds");
        [MenuItem("Manta/Environment/Preview clouds/Heavy clouds")] static void Heavy()=>Preview("Heavy clouds");
        [MenuItem("Manta/Environment/Preview clouds/Scattered clouds")] static void Scattered()=>Preview("Scattered clouds");
    }
}

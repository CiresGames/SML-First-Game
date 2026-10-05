using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MantaFlight.Rider.Editor
{
    [InitializeOnLoad]
    public static class RiderScarfValidation
    {
        const string Key="Rider.Scarf.Validation";
        const string Report="Logs/MantaFlight/validation-scarf.txt";
        static double deadline;
        static RiderScarfValidation()
        {
            if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Run;}
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/MantaFlight/Scenes/MantaFlight.unity");SessionState.SetBool(Key,true);
            deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update-=Run;EditorApplication.update+=Run;EditorApplication.EnterPlaymode();
        }
        static void Check(bool ok,string message)
        {
            File.AppendAllText(Report,(ok?"PASS ":"FAIL ")+message+"\n");if(!ok)throw new Exception(message);
        }
        static void Run()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(1);return;}
            if(!EditorApplication.isPlaying || Time.time<.2f)return;
            EditorApplication.update-=Run;
            try
            {
                Directory.CreateDirectory("Logs/MantaFlight");File.WriteAllText(Report,"");
                var rider=Object.FindFirstObjectByType<RiderController>();var scarf=rider.GetComponent<RiderScarf>();var visual=rider.GetComponent<RiderVisuals>();
                Check(scarf!=null && scarf.fabric!=null && scarf.collar!=null,"Scarf installed and referenced in gameplay scene");
                rider.mantaInput.SetMenu(false);rider.enabled=false;rider.mount.enabled=false;rider.mount.manta.enabled=false;
                rider.PlaceForTest(new Vector3(-900,1000,-900),Vector3.zero,RiderState.Grounded);
                visual.enabled=false;var wings=rider.GetComponent<RiderWingsuit>();wings.enabled=false;
                foreach(var mesh in new[]{wings.leftWing,wings.rightWing,wings.legWing})mesh.GetComponent<Renderer>().enabled=false;
                visual.animator.Play("Locomotion",0,0);visual.animator.Update(0);visual.animator.speed=0;
                scarf.Initialize();scarf.enabled=false;
                Vector3 Settle(Vector3 wind,int fps=90,float seconds=5)
                {
                    scarf.ResetCloth();for(int i=0;i<fps*seconds;i++)scarf.Simulate(1f/fps,wind,Vector3.zero);
                    return scarf.Tip-scarf.Anchor;
                }
                var calm=Settle(Vector3.zero);
                Check(calm.y<-.5f,"Still air lets the cloth hang under gravity: "+calm);
                var weak=Settle(Vector3.right*2);
                var strong=Settle(Vector3.right*18);
                Check(strong.x>.5f,"Scarf aligns with positive crosswind: "+strong);
                Check(strong.magnitude<scarf.length*1.04f,"Strong wind does not over-stretch the fabric");
                Check(strong.x>weak.x+.15f,"Stronger wind lifts/extends scarf more than a light breeze");
                for(int i=0;i<450;i++)scarf.Simulate(1f/90,Vector3.left*18,Vector3.zero);
                Check((scarf.Tip-scarf.Anchor).x<-.5f,"Cloth follows a wind direction reversal");
                var lowFPS=Settle(new Vector3(12,0,-5),24);var highFPS=Settle(new Vector3(12,0,-5),144);
                Check(Vector3.Distance(lowFPS,highFPS)<.08f,"Fixed-step solver agrees at 24 and 144 FPS");
                var start=rider.transform.position;scarf.ResetCloth();
                for(int i=0;i<300;i++){rider.transform.position+=Vector3.forward*(20f/90);scarf.Simulate(1f/90,Vector3.zero,Vector3.forward*20);}
                Check((scarf.Tip-scarf.Anchor).z<-.5f,"Motion through still air streams scarf behind the Rider");
                rider.transform.position=start;scarf.Simulate(.02f,Vector3.zero,Vector3.zero);
                Check(Vector3.Distance(scarf.Tip,scarf.Anchor)<scarf.length*1.12f,"Teleport resets cloth without stretching or explosion");
                Vector3 pausedTip=scarf.Tip;rider.mantaInput.SetMenu(true);scarf.SendMessage("LateUpdate");
                Check(scarf.Tip==pausedTip,"Menu pause freezes cloth simulation");rider.mantaInput.SetMenu(false);
                var windManager=WindManager.Instance;windManager.backgroundWind=new Vector3(12,0,-5);
                scarf.SendMessage("LateUpdate");
                Check(Vector3.Distance(scarf.RelativeWind,WindManager.GetWindAt(scarf.Anchor)-rider.Motor.Velocity)<.01f,"Runtime reads the game's wind service");
                Settle(new Vector3(12,0,-5));
                var initialTip=scarf.Tip;float motion=0;
                for(int i=0;i<180;i++){scarf.Simulate(1f/90,new Vector3(12,0,-5),Vector3.zero);motion=Mathf.Max(motion,Vector3.Distance(initialTip,scarf.Tip));}
                Check(motion>.003f && motion<.09f,"Sustained wind produces restrained cloth motion: "+motion.ToString("F3")+" m");
                Capture(scarf,visual);
                var uv=scarf.fabric.sharedMesh.uv;var vertices=scarf.fabric.sharedMesh.vertices;
                for(int i=0;i<=RiderScarf.Columns;i++)
                    Check(Vector3.Distance(scarf.fabric.transform.TransformPoint(vertices[i]),scarf.Anchor)<scarf.width*.51f,"Pinned neck seam vertex "+i);
                Check(scarf.fabric.GetComponent<Renderer>().bounds.size.sqrMagnitude<5,"Cloth bounds remain finite");
                Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Capture(RiderScarf scarf,RiderVisuals visual)
        {
            var center=visual.animator.GetBoneTransform(HumanBodyBones.Chest).position+Vector3.down*.2f+Vector3.right*.3f;
            var go=new GameObject("Scarf validation camera");var camera=go.AddComponent<Camera>();
            camera.transform.position=center+new Vector3(1.5f,1,3);camera.transform.LookAt(center);camera.orthographic=true;camera.orthographicSize=1.25f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.09f);camera.nearClipPlane=.01f;
            bool fog=RenderSettings.fog;RenderSettings.fog=false;
            var fill=new GameObject("Scarf validation light");var light=fill.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;fill.transform.rotation=camera.transform.rotation;
            var texture=new RenderTexture(1100,1000,24);camera.targetTexture=texture;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=texture;
            var pixels=new Texture2D(1100,1000,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1100,1000),0,0);pixels.Apply();
            Directory.CreateDirectory("ArtSource/Rider");File.WriteAllBytes("ArtSource/Rider/RiderScarf-preview.png",pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(pixels);Object.DestroyImmediate(texture);Object.DestroyImmediate(go);Object.DestroyImmediate(fill);RenderSettings.fog=fog;
        }
        static void Finish(int code)
        {SessionState.SetBool(Key,false);EditorApplication.update-=Run;EditorApplication.Exit(code);}
    }
}

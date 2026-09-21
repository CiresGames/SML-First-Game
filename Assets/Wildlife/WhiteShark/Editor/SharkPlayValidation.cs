using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Sharks.Editor
{
    [InitializeOnLoad]
    public static class SharkPlayValidation
    {
        const string Key="SharkPlayValidation.Pending";
        static double started;
        static SharkPlayValidation()
        {
            if(SessionState.GetBool(Key,false)) { started=EditorApplication.timeSinceStartup; EditorApplication.update+=Run; }
        }
        public static void Begin()
        {
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string message) { if(!valid) throw new Exception(message); }
        static void Run()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time<.2f) return;
            EditorApplication.update-=Run; SessionState.SetBool(Key,false);
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Wildlife/WhiteShark/Prefabs/GreatWhite.prefab");
                var victim=new GameObject("Shark test target"); victim.transform.position=new Vector3(0,10000,0);
                var health=victim.AddComponent<SharkPlayerHealth>();
                var instance=UnityEngine.Object.Instantiate(prefab); var shark=instance.GetComponent<FlyingShark>();
                shark.enabled=false; shark.target=health; shark.patrolCenter=victim.transform.position;
                instance.transform.SetPositionAndRotation(victim.transform.position+new Vector3(0,.6f,-3),Quaternion.identity);
                for(int i=0;i<12;i++) shark.Tick(.05f);
                Check(health.HitsTaken==1 && Mathf.Approximately(health.Health,75),"In-range bite must cause exactly 25 damage.");
                for(int i=0;i<16;i++) shark.Tick(.05f);
                Check(health.HitsTaken==1,"Bite recovery must not cause repeated damage.");
                health.enabled=false;
                for(int i=0;i<60;i++) shark.Tick(.05f);
                Check(shark.State!=FlyingShark.Behaviour.Chase && shark.State!=FlyingShark.Behaviour.Bite,"Disabled target must not be chased.");
                UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(victim);
                victim=new GameObject("Occluded test target"); victim.transform.position=new Vector3(0,10000,0); health=victim.AddComponent<SharkPlayerHealth>();
                instance=UnityEngine.Object.Instantiate(prefab); shark=instance.GetComponent<FlyingShark>(); shark.enabled=false; shark.target=health;
                shark.patrolCenter=victim.transform.position+Vector3.back*20;
                instance.transform.SetPositionAndRotation(victim.transform.position+Vector3.back*8,Quaternion.identity);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=victim.transform.position+Vector3.back*4;
                wall.transform.localScale=new Vector3(20,20,1); Physics.SyncTransforms(); shark.Tick(.05f);
                Check(shark.State==FlyingShark.Behaviour.Patrol && health.HitsTaken==0,"Scenery must block detection and bites.");
                UnityEngine.Object.DestroyImmediate(wall);
                health.transform.position+=Vector3.forward*1000; var initial=instance.transform.position;
                for(int i=0;i<40;i++) shark.Tick(.05f);
                Check(Vector3.Distance(initial,instance.transform.position)>5 && shark.State==FlyingShark.Behaviour.Patrol,"Out-of-range shark must patrol.");
                shark.target=null; shark.Tick(.05f);
                health.TakeDamage(200); Check(health.Health==0,"Health must clamp at zero.");
                Check(!health.TakeDamage(25),"Defeated target must reject damage.");
                UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(victim);
                File.AppendAllText("Logs/Sharks/validation.txt","PASS play mode: in-range damage; one hit per bite; recovery; disabled and missing target; scenery occlusion; out-of-range patrol; health clamp.\n");
                Debug.Log("SHARK_PLAY_VALIDATION_PASS"); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}

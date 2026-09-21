using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MantaFlight.Rider.Editor
{
    public static class RiderMotionValidation
    {
        [MenuItem("Manta/Rider/Validate smooth service and safe jump (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required");
            var r = Object.FindFirstObjectByType<RiderController>();
            using var physics = new RiderPhysicsTestScope(r);
            var report = new List<string>(); var objects = new List<GameObject>();
            bool paused = r.Paused; r.mantaInput.SetMenu(false);
            void Check(bool ok, string name) { report.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) throw new Exception(name); }
            void Step(float dt = .02f) { r.mount.Tick(dt); RiderPhysicsTestScope.Step(dt); r.Tick(default, dt); }
            var origin = new Vector3(-900,1000,-900);
            try
            {
                var hull = r.mount.GetComponentInChildren<MeshCollider>();
                Check(hull != null && hull.enabled && hull.bounds.size.sqrMagnitude > 1, "Manta has a real collision shell bound to its kinematic root");
                r.mount.manta.ResetFlight(); r.mount.manta.SetExternalMotion(origin, Quaternion.Euler(50,0,0), new Vector3(0,-45,30));
                Physics.SyncTransforms(); r.Detach(true);
                Check(r.State == RiderState.Falling && r.Motor.Velocity.y >= r.settings.mount.jumpUpSpeed && r.IgnoringMountCollisions,
                    "Diving manta still produces an upward jump and ignores only manta collision pairs");
                Check(Physics.GetIgnoreCollision(r.Motor.Capsule,hull), "Rider/manta collision pair is temporarily ignored");
                Vector3 start = r.transform.position;
                for (int i=0;i<10;i++) Step();
                Check(r.transform.position.y > start.y + .5f && Vector3.Distance(r.transform.position,start) > 1,
                    "Jump visibly rises and separates from the manta");
                for (int i=0;i<100;i++) Step();
                Check(!r.IgnoringMountCollisions && !Physics.GetIgnoreCollision(r.Motor.Capsule,hull),
                    "Collision pair is restored after time and separation, without instant rescoop");

                r.mount.manta.ResetFlight(); r.mount.manta.SetExternalMotion(origin,Quaternion.identity,Vector3.zero);Physics.SyncTransforms();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);objects.Add(wall);
                wall.transform.position=origin+new Vector3(0,3,2.7f);wall.transform.localScale=new Vector3(3,6,.3f);Physics.SyncTransforms();
                r.Detach(true);
                Check(r.State==RiderState.Falling && Mathf.Abs(r.Motor.Velocity.x)>r.settings.mount.jumpForwardSpeed*.5f,
                    "Blocked forward take-off selects a clear side corridor");
                Object.DestroyImmediate(wall);objects.Remove(wall);
                r.mount.manta.SetExternalMotion(origin,Quaternion.identity,Vector3.zero);Physics.SyncTransforms();
                for(int i=0;i<50;i++) { r.Motor.Place(origin,Quaternion.identity,Vector3.zero); r.Tick(default,.02f); }
                Check(r.IgnoringMountCollisions, "Expiry alone never restores collisions while capsules still overlap the manta bounds");
                r.Motor.Place(origin+Vector3.right*30,Quaternion.identity,Vector3.zero);r.Tick(default,.02f);
                Check(!r.IgnoringMountCollisions, "Collision guard releases immediately once the overlapping rider separates");
                r.mount.manta.ResetFlight();r.mount.manta.SetExternalMotion(origin,Quaternion.identity,Vector3.zero);Physics.SyncTransforms();r.Detach(true);
                r.ResetRider();
                Check(!r.IgnoringMountCollisions && !Physics.GetIgnoreCollision(r.Motor.Capsule,hull), "Reset/remount cleanup restores collision pairs");

                var endpoints=new List<Vector3>();
                foreach(int fps in new[]{24,60,144})
                {
                    r.mount.manta.ResetFlight();r.PlaceForTest(origin,Vector3.forward*10,RiderState.Glide);
                    r.mount.manta.SetExternalMotion(origin+new Vector3(0,6,-16),Quaternion.identity,Vector3.zero);
                    float renderTime=0,physicsTime=0,frame=1f/fps;Vector3 previousVelocity=r.mount.Velocity;
                    while(physicsTime<6-.001f)
                    {
                        renderTime+=frame;
                        r.Motor.Place(origin+Vector3.forward*(10*renderTime),Quaternion.identity,Vector3.forward*10);
                        while(physicsTime+.02f<=renderTime && physicsTime<6-.001f)
                        {
                            Vector3 before=r.mount.transform.position;
                            r.mount.Tick(.02f);
                            if((r.mount.transform.position-before).sqrMagnitude>.000001f)throw new Exception("Service overwrote rendered Transform before physics commit");
                            RiderPhysicsTestScope.Step(.02f);physicsTime+=.02f;
                            if((r.mount.Velocity-previousVelocity).magnitude>r.settings.mount.callAcceleration*.02f+.01f)throw new Exception("Unbounded follow acceleration");
                            previousVelocity=r.mount.Velocity;
                        }
                    }
                    endpoints.Add(r.mount.manta.PhysicsPosition);
                    Check(r.mount.Mode==MantaServiceState.Following, "Physics-only follow remains stable with "+fps+" Hz rider updates");
                }
                Check(Vector3.Distance(endpoints[0],endpoints[2])<1 && Vector3.Distance(endpoints[1],endpoints[2])<1,
                    "24/60/144 Hz follow trajectories remain within one metre after six seconds");
                return string.Join("\n",report);
            }
            finally
            {
                foreach(var go in objects)if(go!=null)Object.DestroyImmediate(go);
                r.mount.manta.ResetFlight();r.mantaInput.SetMenu(paused);
                File.WriteAllLines("Logs/MantaFlight/validation-service-motion.txt",report);
            }
        }
    }
}

using System;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace MantaFlight.Rider.Editor
{
    public static class RiderValidation
    {
        static RiderController r;
        static List<string> report=new List<string>();
        static void Check(bool ok,string message){report.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new Exception(message);}
        static void Step(RiderCommand command=default,float dt=.02f){r.Tick(command,dt);Physics.SyncTransforms();}
        static void Advance(float time,RiderCommand command=default){for(int i=0;i<Mathf.CeilToInt(time/.02f);i++)Step(command);}
        [MenuItem("Manta/Rider/Validate ground and glide (Play mode)")]
        public static void RunMenu()=>Debug.Log(Run());
        public static string Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play mode required");
            r=Object.FindFirstObjectByType<RiderController>();report.Clear();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(-700,499.5f,-700);floor.transform.localScale=new Vector3(80,1,80);
            var origin=new Vector3(-700,500.05f,-700);bool paused=r.mantaInput.MenuOpen;
            r.mantaInput.SetMenu(false);
            try
            {
                Physics.SyncTransforms();r.PlaceForTest(origin,Vector3.down,RiderState.Falling);Advance(.5f);
                Check(r.State==RiderState.Grounded,"Fall lands and recovery releases grounded control");
                Step(new RiderCommand{roll=true});var start=r.transform.position;
                Advance(.25f,new RiderCommand{jump=true,toggleGlide=true});
                Check(r.State==RiderState.Rolling,"Roll remains committed despite jump/glide input");
                Advance(.42f);
                Check(r.State==RiderState.Grounded && Vector3.Distance(start,r.transform.position)>3.8f,"Roll travels configured distance and releases control");
                Step(new RiderCommand{roll=true});Check(r.State!=RiderState.Rolling,"Roll cooldown prevents immediate repeat");
                r.PlaceForTest(origin+Vector3.up*2,Vector3.down,RiderState.Falling);Step(new RiderCommand{toggleGlide=true});
                Check(r.State==RiderState.Falling && !r.GlideAvailable,"Low altitude blocks wingsuit deployment");
                r.PlaceForTest(origin,Vector3.down,RiderState.Falling);Advance(.5f);Step(new RiderCommand{jump=true});Step(new RiderCommand{toggleGlide=true});
                Check(r.State==RiderState.Falling,"Immediate post-jump deployment is blocked");
                r.PlaceForTest(origin+Vector3.up*80,Vector3.forward*20,RiderState.Falling);var velocity=r.Motor.Velocity;
                Step(new RiderCommand{toggleGlide=true});
                Check(r.State==RiderState.Deploying && (r.Motor.Velocity-velocity).magnitude<r.settings.ground.gravity*.03f,"Deployment retains incoming velocity");
                Vector3 toggleStart=r.transform.position;
                for(int i=0;i<12;i++){Step(new RiderCommand{toggleGlide=true});Step();}
                Check(r.Airborne && Vector3.Distance(toggleStart,r.transform.position)>1,"Rapid deployment toggles keep moving and never stick in a transition");
                r.PlaceForTest(origin+Vector3.up*80,Vector3.forward*20,RiderState.Glide);
                Advance(.8f,new RiderCommand{glide=Vector2.up});float diveSpeed=r.Motor.Velocity.magnitude,down=r.Motor.Velocity.y;
                Check(r.Glide.Pitch>20 && down< -5,"Diving pitches down and trades altitude for speed");
                Advance(1.1f,new RiderCommand{glide=Vector2.down});
                Check(r.Motor.Velocity.y>down,"Pull-out bends velocity upwards");
                r.PlaceForTest(origin+Vector3.up*100,Vector3.forward*2,RiderState.Glide);Advance(.1f);
                Check(r.Glide.Stalled,"Low airspeed collapses lift into a stall");
                Advance(2,new RiderCommand{glide=Vector2.up});
                Check(r.Motor.Velocity.magnitude>r.settings.glide.recoverSpeed && !r.Glide.Stalled,"Pitching down recovers airspeed and stall");
                r.PlaceForTest(origin+Vector3.up*2,Vector3.down*20+Vector3.forward*10,RiderState.Falling);Advance(.12f);
                Check(r.State==RiderState.Rolling,"High impact forces landing roll");
                r.mount.manta.ResetFlight();r.Detach(true);
                Check(r.State==RiderState.JumpOff && r.GlidePose==1 && !r.CanScoop,"Jump-off deploys wingsuit and blocks immediate scoop");
                Advance(1.1f);Check(r.CanScoop,"Grace period eventually permits scoop");
                return string.Join("\n",report);
            }
            finally
            {
                Object.DestroyImmediate(floor);r.mount.manta.ResetFlight();r.mantaInput.SetMenu(paused);
                Directory.CreateDirectory("Logs/MantaFlight");File.WriteAllLines("Logs/MantaFlight/validation-rider.txt",report);
            }
        }
        public static string RunTransitions()
        {
            r=Object.FindFirstObjectByType<RiderController>();report.Clear();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);var origin=new Vector3(-700,500,-700);
            floor.transform.position=origin+Vector3.down*.5f;floor.transform.localScale=new Vector3(80,1,80);
            bool paused=r.mantaInput.MenuOpen;r.mantaInput.SetMenu(false);
            var camera=Object.FindFirstObjectByType<RiderCamera>();bool inverted=r.Input.UserSettings.InvertPitch;
            UnityEngine.InputSystem.Gamepad pad=null;
            try
            {
                Physics.SyncTransforms();r.mount.manta.ResetFlight();r.mount.manta.SetExternalMotion(origin+Vector3.up*2.2f,Quaternion.identity,Vector3.zero);
                Check(r.mount.CanDismount(out _),"Low stationary manta permits a clear ground dismount");
                Step(new RiderCommand{context=true});Advance(.9f);
                Check(r.State==RiderState.Grounded && r.mount.Mode==MantaServiceState.Idle,"Step-off reaches ground and manta hovers idle");
                r.PlaceForTest(origin,Vector3.down,RiderState.Falling);Advance(.5f);
                r.mount.manta.SetExternalMotion(origin+new Vector3(30,20,0),Quaternion.identity,Vector3.zero);
                r.mount.ToggleCall();for(int i=0;i<700 && r.mount.Calling;i++){r.mount.Tick(.02f);Step();}
                Check(!r.mount.Calling && r.mount.Mode==MantaServiceState.Idle,"Ground call lands the manta nearby");
                var mountPoint=new Vector3(r.mount.Seat.position.x,origin.y,r.mount.Seat.position.z);
                r.Motor.Place(mountPoint,Quaternion.identity,Vector3.down);Advance(.5f);
                Check(r.mount.CanMountGround(),"Landed manta exposes contextual mount window");
                Step(new RiderCommand{context=true});Advance(.5f);Check(r.Mounted,"Ground contextual mount blends onto seat");
                r.PlaceForTest(origin+Vector3.up*100,Vector3.forward*25,RiderState.Glide);
                pad=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad,new UnityEngine.InputSystem.LowLevel.GamepadState{leftStick=Vector2.up});UnityEngine.InputSystem.InputSystem.Update();
                r.Input.UserSettings.SetInvertPitch(false);var normal=r.Input.Read(.02f);var before=r.Motor.Velocity;var rotation=r.transform.rotation;
                r.Input.UserSettings.SetInvertPitch(true);var inverse=r.Input.Read(.02f);
                Check(normal.glide.y>.5f && inverse.glide.y<-.5f && normal.move==inverse.move,"Pitch preference flips only the wingsuit input axis");
                Check(r.Motor.Velocity==before && r.transform.rotation==rotation,"Changing inversion does not jump orientation or velocity");
                foreach(var v in new[]{Vector3.down*60,Vector3.up*60,Vector3.zero,new Vector3(.01f,-60,.01f)})
                {
                    r.PlaceForTest(origin+Vector3.up*100,v,RiderState.Glide);
                    for(int i=0;i<30;i++)camera.Tick(.02f);
                    Check(!float.IsNaN(camera.transform.rotation.x) && Vector3.Dot(camera.transform.up,Vector3.up)>.2f,"Velocity camera remains upright at "+v);
                }
                float pass=r.settings.mount.passTimeout,timeout=r.settings.mount.callTimeout;
                try
                {
                    r.settings.mount.passTimeout=.1f;r.settings.mount.callTimeout=2;
                    r.PlaceForTest(origin+Vector3.up*100,Vector3.forward*30,RiderState.Glide);
                    r.mount.manta.SetExternalMotion(origin+new Vector3(100,100,-100),Quaternion.identity,Vector3.zero);
                    r.mount.ToggleCall();for(int i=0;i<8;i++){r.mount.Tick(.02f);Step();}
                    Check(r.mount.Mode==MantaServiceState.Retry && r.mount.PassCount>1,"Missed intercept schedules a fresh pass");
                }
                finally{r.settings.mount.passTimeout=pass;r.settings.mount.callTimeout=timeout;}
                return string.Join("\n",report);
            }
            finally
            {
                if(pad!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(pad);
                r.Input.UserSettings.SetInvertPitch(inverted);Object.DestroyImmediate(floor);r.mount.manta.ResetFlight();r.mantaInput.SetMenu(paused);
                File.WriteAllLines("Logs/MantaFlight/validation-rider-transitions.txt",report);
            }
        }
        public static string RunMount()
        {
            r=Object.FindFirstObjectByType<RiderController>();report.Clear();bool paused=r.mantaInput.MenuOpen;r.mantaInput.SetMenu(false);
            try
            {
                var origin=new Vector3(-700,700,-700);
                r.PlaceForTest(origin,Vector3.forward*25,RiderState.Glide);
                r.mount.manta.SetExternalMotion(origin-Vector3.forward*35-Vector3.up*3,Quaternion.identity,Vector3.zero);
                r.mount.CallToPosition(r.transform.position,r.Motor.Velocity,true);
                for(int i=0;i<700 && !r.Mounted;i++){r.mount.Tick(.02f);Step();}
                Check(r.Mounted,"Fly-by intercept catches moving glider and completes remount");
                r.PlaceForTest(origin,Vector3.down*12,RiderState.Falling);
                r.mount.manta.SetExternalMotion(origin-Vector3.forward*10,Quaternion.identity,Vector3.zero);
                r.mount.ToggleCall();Check(r.mount.Calling,"Call starts while falling");r.mount.ToggleCall();Check(!r.mount.Calling,"Second call cancels cleanly");
                float timeout=r.settings.mount.callTimeout;r.settings.mount.callTimeout=.15f;
                r.mount.ToggleCall();for(int i=0;i<15;i++){r.mount.Tick(.02f);Step();}
                Check(!r.mount.Calling,"Intercept timeout returns manta to idle");r.settings.mount.callTimeout=timeout;
                return string.Join("\n",report);
            }
            finally {r.mount.manta.ResetFlight();r.mantaInput.SetMenu(paused);File.WriteAllLines("Logs/MantaFlight/validation-rider-mount.txt",report);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MantaFlight.Editor
{
    public static class FlightLookSetup
    {
        const string Path="Assets/MantaFlight/Input/MantaControls.inputactions";
        [MenuItem("Manta/Configure flight free look")]
        public static void Install()
        {
            var asset=InputActionAsset.FromJson(File.ReadAllText(Path));
            try
            {
                var flight=asset.FindActionMap("Flight",true);var climb=flight.FindAction("Climb",true);
                var dive=flight.FindAction("Dive",true);
                for(int i=dive.bindings.Count-1;i>=0;i--)
                    if(dive.bindings[i].path=="<Gamepad>/rightStickPress")dive.ChangeBinding(i).Erase();
                if(flight.FindAction("LookBack")==null)flight.AddAction("LookBack",InputActionType.Button).AddBinding("<Gamepad>/rightStickPress",groups:"Gamepad");
                // The left stick already controls pitch. Reserve the right stick for independent viewing.
                for(int i=climb.bindings.Count-1;i>=0;i--)
                    if(climb.bindings[i].path=="<Gamepad>/rightStick/y")climb.ChangeBinding(i).Erase();
                var look=flight.FindAction("Look");
                if(look==null)
                {
                    look=flight.AddAction("Look",InputActionType.Value,expectedControlLayout:"Vector2");
                    look.AddBinding("<Mouse>/delta",groups:"KeyboardMouse");
                    look.AddBinding("<Gamepad>/rightStick",groups:"Gamepad");
                }
                File.WriteAllText(Path,asset.ToJson());
            }
            finally {UnityEngine.Object.DestroyImmediate(asset);}
            AssetDatabase.ImportAsset(Path);
            const string riderPath="Assets/MantaFlight/Rider/Settings/RiderControls.inputactions";
            var rider=InputActionAsset.FromJson(File.ReadAllText(riderPath));
            try
            {
                var map=rider.FindActionMap("Rider",true);
                if(map.FindAction("LookBack")==null)map.AddAction("LookBack",InputActionType.Button).AddBinding("<Gamepad>/rightStickPress");
                File.WriteAllText(riderPath,rider.ToJson());
            }
            finally{UnityEngine.Object.DestroyImmediate(rider);}
            AssetDatabase.ImportAsset(riderPath);Validate();
        }
        public static void Validate()
        {
            Directory.CreateDirectory("Logs/MantaFlight");
            var lines=new System.Collections.Generic.List<string>();
            void Check(bool value,string message){if(!value)throw new Exception(message);lines.Add("PASS "+message);}
            var asset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(Path);
            var flight=asset.FindActionMap("Flight",true);
            Check(flight["Look"].bindings.Any(b=>b.path=="<Mouse>/delta") && flight["Look"].bindings.Any(b=>b.path=="<Gamepad>/rightStick"),"Manta mouse/right-stick look bindings");
            Check(!flight["Climb"].bindings.Any(b=>b.path.Contains("rightStick")),"Right-stick look does not also command climb");
            var rider=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/MantaFlight/Rider/Settings/RiderControls.inputactions");
            Check(rider.FindActionMap("Rider")["Look"].bindings.Any(b=>b.path=="<Gamepad>/rightStick"),"Glide uses Rider look action");
            var look=new FlightFreeLook();look.Tick(new Vector2(90,30),.02f);
            Check(look.Offset==new Vector2(90,30),"Wide independent horizontal and vertical look");
            look.Tick(Vector2.zero,1);Check(look.Offset==new Vector2(90,30),"Look holds while briefly idle");
            look.Tick(Vector2.zero,2);Check(look.Offset.x>0 && look.Offset.x<90,"Smooth delayed recenter");
            look.Tick(new Vector2(1000,1000),.02f);Check(look.Offset==new Vector2(160,65),"Look limits prevent camera flipping");
            look.Reset();Check(look.Offset==Vector2.zero,"Reset clears camera offset");
            Vector2 AtRate(int fps)
            {
                var f=new FlightFreeLook();for(int i=0;i<fps;i++)f.Tick(new Vector2(100f/fps,0),1f/fps);
                for(int i=0;i<fps*3;i++)f.Tick(Vector2.zero,1f/fps);return f.Offset;
            }
            Check(Vector2.Distance(AtRate(30),AtRate(144))<.01f,"Stick sensitivity and recenter are frame-rate independent");
            File.WriteAllLines("Logs/MantaFlight/validation-free-look.txt",lines);Debug.Log(string.Join("\n",lines));
        }
    }
}

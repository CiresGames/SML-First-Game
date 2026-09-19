using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace MantaFlight.Editor
{
    public static class MantaPrototypeBuilder
    {
        public const string Root = "Assets/MantaFlight";
        public const string ScenePath = Root + "/Scenes/MantaFlight.unity";
        static Material skin, belly, fin, coral, rock, grass, sand, water, gold, cloud;

        [MenuItem("Manta/Save current tuning as new preset")]
        public static void SaveTuning()
        {
            MantaTuningExporter.SaveNewFromMenu();
        }

        [MenuItem("Manta/Build or open flight playground")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before generating the playground.");
            if (File.Exists(ScenePath))
            {
                var existing = SceneManager.GetSceneByPath(ScenePath);
                if (!existing.isLoaded) existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(existing); return;
            }
            foreach (string dir in new[] { "Scenes", "Settings", "Input", "Prefabs", "Materials" }) Directory.CreateDirectory(Root + "/" + dir);
            AssetDatabase.Refresh();
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.isDirty)
                EditorSceneManager.SaveScene(active, AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/PreviousUnsavedScene.unity"));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                string.IsNullOrEmpty(active.path) && !active.isDirty ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            skin = Material("Manta jade", new Color(.045f, .31f, .36f));
            fin = Material("Wing turquoise", new Color(.07f, .49f, .5f));
            belly = Material("Warm ivory", new Color(.83f, .88f, .74f));
            coral = Material("Rider terracotta", new Color(.8f, .27f, .12f));
            rock = Material("Cliff blue slate", new Color(.31f, .43f, .44f));
            grass = Material("Moss gold", new Color(.55f, .58f, .31f));
            sand = Material("Sand warm", new Color(.68f, .64f, .45f));
            water = Material("Lagoon", new Color(.13f, .43f, .47f));
            gold = Material("Navigation copper", new Color(.98f, .58f, .2f));
            cloud = Material("Cloud ivory", new Color(.86f, .88f, .79f));
            var settings = ScriptableObject.CreateInstance<MantaFlightSettings>();
            AssetDatabase.CreateAsset(settings, Root + "/Settings/FlightSettings.asset");
            var inputs = CreateInputs();
            CreateLighting();
            CreateWorld();
            var manta = CreateManta(settings, inputs);
            var cameraObject = new GameObject("Manta Camera", typeof(Camera), typeof(AudioListener), typeof(MantaCameraController));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>(); camera.farClipPlane = 2400; camera.nearClipPlane = .2f;
            camera.backgroundColor = new Color(.55f, .72f, .72f); camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.GetComponent<MantaCameraController>().target = manta.GetComponent<MantaController>();
            cameraObject.transform.SetPositionAndRotation(manta.transform.position + new Vector3(0, 5, -15), Quaternion.Euler(9, 0, 0));
            var debug = new GameObject("Manta Debug & Tuning").AddComponent<MantaDebug>();
            debug.controller = manta.GetComponent<MantaController>();
            debug.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = manta;
            Debug.Log("MANTA: playground created; BasicFlight phase ready.");
        }

        static InputActionAsset CreateInputs()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "MantaControls";
            var map = asset.AddActionMap("Flight");
            var steer = map.AddAction("Steering", InputActionType.Value); steer.expectedControlType = "Vector2";
            steer.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w", groups: "KeyboardMouse")
                .With("Down", "<Keyboard>/s", groups: "KeyboardMouse").With("Left", "<Keyboard>/a", groups: "KeyboardMouse").With("Right", "<Keyboard>/d", groups: "KeyboardMouse");
            steer.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow", groups: "KeyboardMouse")
                .With("Down", "<Keyboard>/downArrow", groups: "KeyboardMouse").With("Left", "<Keyboard>/leftArrow", groups: "KeyboardMouse").With("Right", "<Keyboard>/rightArrow", groups: "KeyboardMouse");
            steer.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            var lift = map.AddAction("Climb", InputActionType.Value); lift.expectedControlType = "Axis";
            lift.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/space", groups: "KeyboardMouse").With("Negative", "<Keyboard>/leftCtrl", groups: "KeyboardMouse");
            lift.AddBinding("<Gamepad>/rightStick/y", groups: "Gamepad");
            var mouse = map.AddAction("MouseSteering", InputActionType.Value); mouse.expectedControlType = "Vector2";
            mouse.AddBinding("<Mouse>/delta", groups: "KeyboardMouse");
            Add(map, "MouseEnable", "<Mouse>/rightButton", null);
            Add(map, "Accelerate", "<Keyboard>/leftShift", "<Gamepad>/rightTrigger", true);
            Add(map, "Brake", "<Keyboard>/c", "<Gamepad>/leftTrigger", true);
            Add(map, "RollLeft", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Add(map, "RollRight", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            Add(map, "LoopForward", "<Keyboard>/f", "<Gamepad>/buttonWest");
            Add(map, "LoopBackward", "<Keyboard>/g", "<Gamepad>/buttonNorth");
            Add(map, "Turnaround", "<Keyboard>/x", "<Gamepad>/buttonEast");
            Add(map, "TightTurn", "<Keyboard>/leftAlt", "<Gamepad>/buttonSouth");
            Add(map, "Dive", "<Keyboard>/v", "<Gamepad>/rightStickPress", true);
            Add(map, "Reset", "<Keyboard>/r", "<Gamepad>/select");
            Add(map, "Menu", "<Keyboard>/escape", "<Gamepad>/start");
            Add(map, "HUD", "<Keyboard>/f1", null);
            asset.AddControlScheme("KeyboardMouse").WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            asset.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            string path = Root + "/Input/MantaControls.inputactions";
            File.WriteAllText(path, asset.ToJson()); Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        static void Add(InputActionMap map, string name, string keyboard, string gamepad, bool axis = false)
        {
            var action = map.AddAction(name, axis ? InputActionType.Value : InputActionType.Button);
            action.expectedControlType = axis ? "Axis" : "Button";
            action.AddBinding(keyboard, groups: "KeyboardMouse");
            if (gamepad != null) action.AddBinding(gamepad, groups: "Gamepad");
        }
        static Material Material(string name, Color color)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.name = name;
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .18f);
            AssetDatabase.CreateAsset(m, Root + "/Materials/" + name + ".mat"); return m;
        }
        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.isStatic = true;
            return go;
        }
        static Transform Node(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
        }
        static GameObject CreateManta(MantaFlightSettings settings, InputActionAsset actions)
        {
            var root = new GameObject("Manta • Rider Rig"); root.SetActive(false); root.layer = 2;
            var input = root.AddComponent<MantaInput>(); input.actions = actions;
            root.AddComponent<Rigidbody>(); root.AddComponent<MantaManeuvers>();
            var controller = root.AddComponent<MantaController>(); controller.settings = settings;
            var visuals = root.AddComponent<MantaVisuals>();
            var visual = Node("Visuals", root.transform, Vector3.zero); visuals.visualRoot = visual;
            Primitive("Body", PrimitiveType.Sphere, visual, Vector3.zero, new Vector3(3.6f, .8f, 4.8f), skin, false);
            Primitive("Underside", PrimitiveType.Sphere, visual, new Vector3(0, -.25f, .15f), new Vector3(3.3f, .45f, 4.3f), belly, false);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var wing = Node(sign < 0 ? "Left Wing" : "Right Wing", visual, new Vector3(sign * 1.1f, 0, 0));
                var inner = Primitive("Wing inner", PrimitiveType.Sphere, wing, new Vector3(sign * 1.45f, -.03f, -.3f), new Vector3(4.5f, .28f, 3.2f), fin, false);
                inner.transform.localRotation = Quaternion.Euler(0, sign * 22, 0);
                var tip = Node("Wing Tip Pivot", wing, new Vector3(sign * 3, 0, -1));
                var outer = Primitive("Wing swept tip", PrimitiveType.Sphere, tip, new Vector3(sign * .8f, 0, -.45f), new Vector3(2.6f, .16f, 1.3f), skin, false);
                outer.transform.localRotation = Quaternion.Euler(0, sign * 32, 0);
                Primitive("Cephalic fin", PrimitiveType.Capsule, visual, new Vector3(sign * .9f, .02f, 2.3f), new Vector3(.28f, .72f, .25f), fin, false).transform.localRotation = Quaternion.Euler(75, sign * -15, 0);
                Primitive("Eye", PrimitiveType.Sphere, visual, new Vector3(sign * 1.25f, .23f, 1.4f), new Vector3(.19f, .16f, .22f), gold, false);
                if (sign < 0) { visuals.leftWing = wing; visuals.leftTip = tip; } else { visuals.rightWing = wing; visuals.rightTip = tip; }
            }
            visuals.tail = Node("Tail Pivot", visual, new Vector3(0, 0, -1.9f));
            Primitive("Tail", PrimitiveType.Capsule, visuals.tail, new Vector3(0, 0, -2), new Vector3(.18f, 2.4f, .18f), skin, false).transform.localRotation = Quaternion.Euler(90, 0, 0);
            var saddle = Node("Rider Attachment • future mount socket", visual, new Vector3(0, .5f, .15f));
            Primitive("Saddle", PrimitiveType.Sphere, saddle, Vector3.zero, new Vector3(1.1f, .25f, 1.3f), coral, false);
            visuals.rider = Node("Rider Visuals", saddle, new Vector3(0, .3f, 0));
            Primitive("Tunic", PrimitiveType.Capsule, visuals.rider, new Vector3(0, .5f, 0), new Vector3(.58f, .55f, .38f), coral, false);
            Primitive("Head", PrimitiveType.Sphere, visuals.rider, new Vector3(0, 1.17f, .08f), new Vector3(.48f, .52f, .46f), belly, false);
            Primitive("Hair", PrimitiveType.Sphere, visuals.rider, new Vector3(0, 1.36f, .01f), new Vector3(.51f, .24f, .49f), skin, false);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Primitive("Seated leg", PrimitiveType.Capsule, visuals.rider, new Vector3(sign * .4f, .02f, .26f), new Vector3(.2f, .42f, .22f), skin, false).transform.localRotation = Quaternion.Euler(55, 0, sign * 20);
                Primitive("Arm", PrimitiveType.Capsule, visuals.rider, new Vector3(sign * .31f, .55f, .23f), new Vector3(.15f, .34f, .16f), belly, false).transform.localRotation = Quaternion.Euler(-35, 0, sign * 12);
            }
            var trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trailMaterial.SetColor("_BaseColor", new Color(.7f, .94f, .9f, .5f));
            AssetDatabase.CreateAsset(trailMaterial, Root + "/Materials/Wing trails.mat");
            var trails = new List<TrailRenderer>();
            foreach (var tip in new[] { visuals.leftTip, visuals.rightTip })
            {
                var trail = tip.gameObject.AddComponent<TrailRenderer>(); trail.sharedMaterial = trailMaterial;
                trail.time = .65f; trail.minVertexDistance = .5f; trail.startWidth = .08f; trail.endWidth = 0;
                trail.startColor = new Color(.7f, .94f, .9f, .4f); trail.endColor = new Color(.7f, .94f, .9f, 0); trail.emitting = false;
                trails.Add(trail);
            }
            visuals.trails = trails.ToArray();
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 2;
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Root + "/Prefabs/MantaRider.prefab", InteractionMode.AutomatedAction);
            root.transform.position = new Vector3(0, 55, -210);
            return root;
        }
        static void CreateLighting()
        {
            var sun = new GameObject("Sun • late afternoon", typeof(Light)); sun.transform.rotation = Quaternion.Euler(34, -35, 0);
            var light = sun.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.65f;
            light.color = new Color(1, .86f, .65f); light.shadows = LightShadows.Soft;
            RenderSettings.sun = light; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.52f, .72f, .78f); RenderSettings.ambientEquatorColor = new Color(.47f, .56f, .52f);
            RenderSettings.ambientGroundColor = new Color(.23f, .28f, .27f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0011f; RenderSettings.fogColor = new Color(.61f, .74f, .73f);
            var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetColor("_SkyTint", new Color(.51f, .68f, .76f));
            sky.SetColor("_GroundColor", new Color(.55f, .65f, .65f)); sky.SetFloat("_AtmosphereThickness", .85f);
            AssetDatabase.CreateAsset(sky, Root + "/Materials/Contemplative sky.mat"); RenderSettings.skybox = sky;
        }
        static void CreateWorld()
        {
            var world = Node("Flight Playground • 2400 m", null, Vector3.zero);
            Primitive("Valley floor", PrimitiveType.Cube, world, new Vector3(0, -12, 300), new Vector3(2400, 20, 2400), sand);
            Primitive("Lagoon", PrimitiveType.Cube, world, new Vector3(0, -.5f, 150), new Vector3(200, 1, 1400), water);
            var random = new System.Random(73);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)random.NextDouble() * 2100 - 1050;
                float z = (float)random.NextDouble() * 2000 - 700;
                if (Mathf.Abs(x) < 100) x += x < 0 ? -120 : 120;
                float height = random.Next(18, 95);
                var hill = Primitive("Rolling highland " + i, PrimitiveType.Sphere, world, new Vector3(x, height * .05f - 8, z), new Vector3(random.Next(100, 270), height, random.Next(100, 260)), grass);
                hill.transform.rotation = Quaternion.Euler(0, random.Next(180), 0);
            }
            var canyon = Node("01 • Canyon corridor", world, Vector3.zero);
            for (int i = 0; i < 15; i++)
            {
                float z = 130 + i * 42; float curve = Mathf.Sin(i * .45f) * 18;
                foreach (int side in new[] { -1, 1 })
                {
                    float h = random.Next(65, 110);
                    var cliff = Primitive("Canyon cliff", PrimitiveType.Cube, canyon, new Vector3(curve + side * 65, h * .5f - 3, z), new Vector3(48, h, 48), rock);
                    cliff.transform.rotation = Quaternion.Euler(side * 5, random.Next(-9, 10), 0);
                    Primitive("Cliff moss cap", PrimitiveType.Cube, canyon, new Vector3(curve + side * 65, h - 3, z), new Vector3(49, 3, 47), grass);
                    Primitive("Waterline marker", PrimitiveType.Cylinder, canyon, new Vector3(curve + side * 32, 3, z), new Vector3(2, 4, 2), gold);
                }
            }
            Arch(world, new Vector3(0, 0, 35), 54, 67, 14);
            Arch(world, new Vector3(0, 0, 450), 44, 105, 20);
            Arch(world, new Vector3(-290, 20, 100), 60, 100, 22);
            var slalom = Node("02 • Pillar slalom", world, Vector3.zero);
            for (int i = 0; i < 10; i++)
            {
                float x = -260 + (i % 2 == 0 ? -35 : 35); float z = 250 + i * 65;
                Primitive("Slalom tower " + i, PrimitiveType.Cylinder, slalom, new Vector3(x, 60, z), new Vector3(18, 60, 18), rock);
                Primitive("Tower cap", PrimitiveType.Cylinder, slalom, new Vector3(x, 122, z), new Vector3(21, 2, 21), gold);
            }
            var tunnel = Node("03 • Tunnel / low pass", world, new Vector3(270, 0, 120));
            for (int i = 0; i < 7; i++)
            {
                Primitive("Tunnel left", PrimitiveType.Cube, tunnel, new Vector3(-24, 24, i * 24), new Vector3(12, 50, 26), rock);
                Primitive("Tunnel right", PrimitiveType.Cube, tunnel, new Vector3(24, 24, i * 24), new Vector3(12, 50, 26), rock);
                Primitive("Tunnel ceiling", PrimitiveType.Cube, tunnel, new Vector3(0, 49, i * 24), new Vector3(60, 10, 26), grass);
            }
            var heights = Node("04 • Vertical garden", world, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 2.4f; float y = 80 + i * 27;
                var p = new Vector3(440 + Mathf.Cos(a) * 110, y, 650 + Mathf.Sin(a) * 110);
                Primitive("Floating island", PrimitiveType.Sphere, heights, p, new Vector3(66, 28, 55), rock);
                Primitive("Floating meadow", PrimitiveType.Cylinder, heights, p + Vector3.up * 10, new Vector3(48, 2, 42), grass);
            }
            Primitive("Dive spire", PrimitiveType.Cylinder, heights, new Vector3(440, 160, 650), new Vector3(32, 165, 32), rock);
            for (int i = 0; i < 16; i++)
            {
                var p = new Vector3(random.Next(-950, 950), random.Next(330, 460), random.Next(-600, 1200));
                Primitive("Distant cloud", PrimitiveType.Sphere, world, p, new Vector3(150, 18, 58), cloud, false);
            }
            // Visible close scenery along the otherwise wide open take-off area.
            for (int i = 0; i < 24; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                Primitive("Lagoon stepping stone", PrimitiveType.Sphere, world, new Vector3(side * random.Next(35, 90), 2, -200 + i * 16), new Vector3(12, 10, 18), rock);
            }
        }
        static void Arch(Transform parent, Vector3 origin, float width, float height, float thickness)
        {
            var arch = Node("Natural arch", parent, origin);
            float radius = width * .5f; float spring = height - radius;
            foreach (int side in new[] { -1, 1 }) Primitive("Arch leg", PrimitiveType.Cube, arch, new Vector3(side * radius, spring * .5f, 0), new Vector3(thickness, spring, thickness * 1.4f), rock);
            for (int i = 0; i < 12; i++)
            {
                float a = (i + .5f) / 12 * Mathf.PI;
                var segment = Primitive("Arch crown", PrimitiveType.Cube, arch, new Vector3(Mathf.Cos(a) * radius, spring + Mathf.Sin(a) * radius, 0), new Vector3(thickness, radius * Mathf.PI / 12 + 2, thickness * 1.4f), rock);
                segment.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
        }
    }
}

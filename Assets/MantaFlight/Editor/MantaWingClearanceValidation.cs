using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaWingClearanceValidation
    {
        // Exercise the imported skin, not just the joint positions, in an isolated prefab scene.
        public static string Run()
        {
            var prefab = PrefabUtility.LoadPrefabContents("Assets/MantaFlight/Prefabs/MantaRider.prefab");
            GameObject floor = null;
            var baked = new Mesh();
            try
            {
                var visual = prefab.GetComponentInChildren<MantaVisuals>();
                var root = visual.visualRoot;
                var joints = new[] { visual.leftWing, visual.rightWing, visual.leftTip, visual.rightTip, visual.tail };
                var rotations = new Quaternion[joints.Length];
                for (int i = 0; i < joints.Length; i++) rotations[i] = joints[i].localRotation;
                var origin = new Vector3(-1800, 500, -1800);
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.hideFlags = HideFlags.HideAndDontSave;
                floor.layer = 30;
                floor.transform.localScale = new Vector3(100, 1, 100);
                var type = typeof(MantaVisuals).Assembly.GetType("MantaFlight.MantaWingClearance");
                var apply = type.GetMethod("Apply");
                var report = new StringBuilder();
                int cases = 0;
                foreach (float slope in new[] { 0f, 20f, -20f })
                foreach (float height in new[] { 1.4f, 2.2f })
                foreach (float bank in new[] { -65f, 0f, 65f })
                {
                    var groundRotation = Quaternion.Euler(0, 0, slope);
                    var normal = groundRotation * Vector3.up;
                    floor.transform.SetPositionAndRotation(origin - normal * .5f, groundRotation);
                    prefab.transform.SetPositionAndRotation(origin + normal * height, Quaternion.identity);
                    root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity;
                    for (int i = 0; i < joints.Length; i++) joints[i].localRotation = rotations[i];
                    Physics.SyncTransforms();
                    if (!Physics.Raycast(origin + Vector3.up * 20, Vector3.down, 50, 1 << 30))
                        throw new Exception("Test floor is not visible to physics.");
                    var clearance = Activator.CreateInstance(type, new object[] { root, joints });
                    float minimum = float.PositiveInfinity;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        float phase = frame * Mathf.PI * 2 / 60;
                        root.localRotation = Quaternion.Euler(8, 0, bank);
                        root.localPosition = Vector3.up * Mathf.Sin(phase) * .24f;
                        joints[0].localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(phase) * 40);
                        joints[1].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 40);
                        joints[2].localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(phase - .65f) * 26);
                        joints[3].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase - .65f) * 26);
                        joints[4].localRotation = Quaternion.Euler(Mathf.Sin(phase) * 8, Mathf.Sin(phase - .8f) * 16, 0);
                        apply.Invoke(clearance, new object[] { .02f, 1 << 30, .3f });
                        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(baked);
                            foreach (var vertex in baked.vertices)
                                minimum = Mathf.Min(minimum, Vector3.Dot(skin.transform.TransformPoint(vertex) - origin, normal));
                        }
                    }
                    if (minimum < .25f) throw new Exception($"Wing penetration: slope={slope}, height={height}, bank={bank}, clearance={minimum:F3}");
                    report.AppendLine($"PASS slope={slope}, height={height}, bank={bank}: minimum clearance {minimum:F3}m");
                    cases++;
                    // At altitude the same pose must recover completely.
                    prefab.transform.position += Vector3.up * 100;
                    for (int i = 0; i < 200; i++) apply.Invoke(clearance, new object[] { .02f, 1 << 30, .3f });
                    if ((float)type.GetProperty("Blend").GetValue(clearance) < .999f)
                        throw new Exception("Wing animation did not recover at altitude.");
                }
                return $"{cases} terrain/pose cases passed, plus altitude recovery.\n" + report;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
                if (floor) UnityEngine.Object.DestroyImmediate(floor);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
    }
}

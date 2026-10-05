using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MantaFlight.Editor
{
    [InitializeOnLoad]
    public static class MantaWindPlayValidation
    {
        const string Key = "Manta.Wind.PlayValidation";
        static double deadline;
        static float stageStart;
        static int stage, weakCount;
        static WindZoneParticles visual;
        static WindZone3D[] zones;
        static Camera camera;
        static bool failed;

        static MantaWindPlayValidation()
        {
            EditorApplication.playModeStateChanged += Changed;
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            Debug.Log(MantaWindValidation.Run());
            EditorSceneManager.OpenScene(MantaPrototypeBuilder.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 120;
                stage = 0; stageStart = Time.time;
            }
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("WIND PLAY FAIL: " + message);
            Debug.Log("PASS " + message);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) throw new Exception("Wind Play validation timed out.");
                if (stage == 0)
                {
                    if (Time.time < .2f) return;
                    Check(WindManager.Instance, "Runtime manager registration");
                    zones = UnityEngine.Object.FindObjectsByType<WindZone3D>(FindObjectsSortMode.None);
                    Check(zones.Length == 3, "Three authored zones loaded");
                    visual = Array.Find(UnityEngine.Object.FindObjectsByType<WindZoneParticles>(FindObjectsSortMode.None), p => p.zone.name == "Prevailing wind");
                    Check(visual, "Authored particle system linked");
                    foreach (var z in zones) { z.debugOverride = true; z.debugWind = Vector3.right * 2; }
                    camera = new GameObject("Wind validation camera").AddComponent<Camera>();
                    camera.transform.position = visual.zone.WorldBounds.center + new Vector3(0, -200, -400);
                    camera.transform.rotation = Quaternion.Euler(5, 0, 0); camera.farClipPlane = 5000;
                    camera.clearFlags = CameraClearFlags.Skybox;
                    visual.viewCamera = camera; visual.follow = camera.transform;
                    visual.maxParticles = 60; visual.Configure();
                    stageStart = Time.time; stage = 1;
                }
                else if (stage == 1 && Time.time - stageStart > 3)
                {
                    weakCount = visual.GetComponent<ParticleSystem>().particleCount;
                    Check(weakCount > 0, "Weak wind emits sparse particles");
                    foreach (var z in zones) z.debugWind = Vector3.right * 25;
                    visual.Configure(); stageStart = Time.time; stage = 2;
                }
                else if (stage == 2 && Time.time - stageStart > 3)
                {
                    var ps = visual.GetComponent<ParticleSystem>();
                    Check(ps.particleCount > weakCount && ps.particleCount <= 60, "Strong wind increases density within budget");
                    var particles = new ParticleSystem.Particle[60]; int count = ps.GetParticles(particles);
                    for (int i = 0; i < count; i++)
                        if (particles[i].remainingLifetime > 0)
                            Check(Vector3.Dot(particles[i].velocity.normalized, Vector3.right) > .99f, "Particle follows local wind");
                    Capture();
                    // Frustum case: move the followed volume behind the camera within the LOD range.
                    var target = new GameObject("Wind culling target").transform;
                    target.position = camera.transform.position - camera.transform.forward * 100;
                    visual.follow = target; stageStart = Time.time; stage = 3;
                }
                else if (stage == 3 && Time.time - stageStart > .3f)
                {
                    Check(visual.GetComponent<ParticleSystem>().particleCount == 0, "Off-frustum system stops and clears");
                    visual.follow = camera.transform; visual.zone.enabled = false;
                    stageStart = Time.time; stage = 4;
                }
                else if (stage == 4 && Time.time - stageStart > .3f)
                {
                    Check(visual.GetComponent<ParticleSystem>().particleCount == 0, "Disabled zone stops emission");
                    Check(!ShaderUtil.ShaderHasError(Shader.Find("Manta/Wind streak")), "URP wind shader compiles on graphics device");
                    Debug.Log("WIND_PLAY_VALIDATION_PASS"); Finish(0);
                }
            }
            catch (Exception e) { if (!failed) Debug.LogException(e); failed = true; Finish(1); }
        }
        static void Capture()
        {
            var target = new RenderTexture(960, 540, 24);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/wind-preview.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            EditorApplication.Exit(code);
        }
    }
}

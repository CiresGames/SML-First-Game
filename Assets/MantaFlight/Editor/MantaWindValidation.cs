using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MantaFlight.Editor
{
    public static class MantaWindValidation
    {
        [MenuItem("Manta/Environment/Validate wind zones")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var report = new List<string>();
            var created = new List<GameObject>();
            var existing = WindManager.Instance;
            if (existing) existing.enabled = false;
            Rider.RiderSettings settings = null;
            GameObject New(string name)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                created.Add(go); return go;
            }
            void Check(bool condition, string label)
            { if (!condition) throw new Exception("WIND FAIL: " + label); report.Add("PASS " + label); }
            WindZone3D Box(Vector3 center, Vector3 wind)
            {
                var go = New("Wind test zone"); go.transform.position = center;
                var box = go.AddComponent<BoxCollider>(); box.size = Vector3.one * 100; box.isTrigger = true;
                var zone = go.AddComponent<WindZone3D>(); zone.volume = box; zone.falloff = 10;
                zone.debugOverride = true; zone.debugWind = wind;
                WindManager.Register(zone); return zone;
            }
            try
            {
                var manager = New("Wind test manager").AddComponent<WindManager>();
                // Explicit lifecycle supports both the Edit-mode menu and Play-mode validation.
                manager.SendMessage("OnEnable"); manager.cellSize = 100;
                Vector3 origin = new Vector3(20000, 20000, 20000);
                var a = Box(origin, Vector3.right * 10);
                Physics.SyncTransforms(); manager.RebuildIndex();
                Check((manager.Sample(origin) - Vector3.right * 10).magnitude < .001f, "Box center and m/s");
                Check(manager.Sample(origin + Vector3.right * 55) == Vector3.zero, "Outside volume");
                Check(Mathf.Abs(manager.Sample(origin + Vector3.right * 45).x - 5) < .01f, "Single-zone falloff survives normalization");
                Check(manager.Sample(origin + Vector3.right * 50).magnitude < .001f, "Zero at boundary");
                var b = Box(origin, Vector3.forward * 20); b.weight = 3;
                Physics.SyncTransforms(); manager.RebuildIndex();
                Check((manager.Sample(origin) - new Vector3(2.5f, 0, 15)).magnitude < .01f, "Weighted overlap");
                b.enabled = false;
                Check((manager.Sample(origin) - Vector3.right * 10).magnitude < .01f, "Disabled zone excluded immediately");
                a.transform.rotation = Quaternion.Euler(0, 45, 0); a.transform.localScale = new Vector3(2, 1, 1);
                Physics.SyncTransforms(); manager.RebuildIndex();
                Check(a.InfluenceAt(a.transform.TransformPoint(new Vector3(47.5f, 0, 0))) > .49f, "Rotated scaled box");
                a.enabled = false;
                var sphereObject = New("Sphere wind"); sphereObject.transform.position = origin;
                sphereObject.transform.localScale = new Vector3(2, 1, 1);
                var sphere = sphereObject.AddComponent<SphereCollider>(); sphere.radius = 10; sphere.isTrigger = true;
                var spherical = sphereObject.AddComponent<WindZone3D>(); spherical.volume = sphere; spherical.falloff = 10;
                WindManager.Register(spherical); Physics.SyncTransforms();
                Check(Mathf.Abs(spherical.InfluenceAt(origin + Vector3.right * 15) - .5f) < .01f, "Sphere nonuniform scale");
                spherical.enabled = false;
                var capsuleObject = New("Custom capsule"); capsuleObject.transform.position = origin;
                var capsule = capsuleObject.AddComponent<CapsuleCollider>(); capsule.radius = 10; capsule.height = 40; capsule.isTrigger = true;
                var custom = capsuleObject.AddComponent<WindZone3D>(); custom.volume = capsule; custom.falloff = 10;
                Physics.SyncTransforms();
                Check(Mathf.Abs(custom.InfluenceAt(origin + Vector3.right * 5) - .5f) < .01f, "Custom collider surface falloff");
                custom.enabled = false;
                var local = Box(origin, Vector3.up * 15);
                for (int i = 0; i < 80; i++) Box(origin + Vector3.right * (500 + i * 200), Vector3.one);
                Physics.SyncTransforms(); manager.RebuildIndex(); manager.Sample(origin);
                Check(manager.LastCandidateCount <= 2, "Spatial query excludes 80 distant zones");
                local.transform.position += Vector3.forward * 300;
                Physics.SyncTransforms(); manager.RebuildIndex();
                Check(manager.Sample(origin).sqrMagnitude < .001f && manager.Sample(local.transform.position).y > 14, "Moved zone reindex");
                manager.maxCellsPerZone = 1; manager.RebuildIndex();
                Check(manager.Sample(local.transform.position).y > 14, "Oversized-zone fallback");
                local.debugOverride = false; local.gusts = 0; local.directionVariation = 0; local.direction = Vector3.up; local.strength = 10;
                Check(Mathf.Abs(local.Sample(0, 2, 0).y - 20) < .01f, "Weather amplification");
                local.debugOverride = true; local.debugWind = Vector3.right * 7;
                Check(local.Sample(100, 5, 90) == local.debugWind, "Debug bypasses weather and gusts");
                // Galilean invariance: identical airspeed produces identical aerodynamic forces.
                var glideObject = New("Glide wind test");
                var glide = glideObject.AddComponent<Rider.RiderGlideMotor>();
                settings = ScriptableObject.CreateInstance<Rider.RiderSettings>(); glide.settings = settings;
                Vector3 airspeed = new Vector3(0, -3, 30);
                manager.backgroundWind = Vector3.zero; glide.Deploy(airspeed);
                Vector3 calm = glide.Integrate(airspeed, Vector2.zero, 1, .02f);
                Vector3 updraft = new Vector3(8, 15, 0); manager.backgroundWind = updraft;
                glide.Deploy(airspeed + updraft);
                Vector3 windy = glide.Integrate(airspeed + updraft, Vector2.zero, 1, .02f);
                Check((windy - updraft - calm).magnitude < .001f, "Glide relative airspeed and upward transport");
                Check(windy.y > calm.y + 14, "Updraft allows altitude gain");
                glide.windInfluence = 0; glide.Deploy(airspeed);
                Check((glide.Integrate(airspeed, Vector2.zero, 1, .02f) - calm).magnitude < .001f, "Glide wind opt-out");
                var visual = New("Wind particle test").AddComponent<WindZoneParticles>(); visual.Configure();
                var ps = visual.GetComponent<ParticleSystem>();
                Check(ps.main.simulationSpace == ParticleSystemSimulationSpace.World && !ps.emission.enabled
                    && ps.main.maxParticles == visual.maxParticles, "Particle budget and world-space configuration");
                return string.Join("\n", report);
            }
            finally
            {
                foreach (var go in created)
                {
                    if (!go) continue;
                    var zone = go.GetComponent<WindZone3D>(); if (zone) WindManager.Unregister(zone);
                    Object.DestroyImmediate(go);
                }
                if (settings) Object.DestroyImmediate(settings);
                if (existing) { existing.enabled = true; existing.SendMessage("OnEnable"); }
            }
        }
    }
}

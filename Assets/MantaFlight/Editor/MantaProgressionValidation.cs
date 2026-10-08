using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaProgressionValidation
    {
        [MenuItem("Manta/Progression/Validate progression rules")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var report = new List<string>();
            void Check(bool condition, string label)
            { if (!condition) throw new Exception("PROGRESSION FAIL: " + label); report.Add("PASS " + label); }
            var d = new MantaProgressionData();
            Check(d.IsValid() && d.level == 1 && d.Strength(MantaStat.Speed) == 0, "Fresh save starts without stat bonuses");
            d.AddXP(99); Check(d.level == 1, "XP threshold is exact");
            d.AddXP(1); Check(d.level == 2 && d.xp == 0 && d.Strength(MantaStat.Speed) == 0, "Level carries XP without awarding stat upgrades");
            d.AddXP(float.NaN); d.AddXP(-100); d.AddXP(float.PositiveInfinity);
            Check(d.IsValid() && d.xp == 0, "Invalid XP cannot poison a save");
            d.AddXP(100000); Check(d.level == 20 && d.xp == 0 && d.Strength(MantaStat.Speed) == 0, "Level cap does not grant passive stat bonuses");
            d.AddXP(1000); Check(d.IsValid() && d.xp == 0, "XP stops at the level cap");
            d = new MantaProgressionData();
            Check(d.CollectFruit("a", MantaStat.Force) && !d.CollectFruit("a", MantaStat.Force), "Fruit cannot be collected twice");
            Check(!d.CollectFruit("a", MantaStat.Speed), "Unique fruit ID cannot be replayed as another stat");
            Check(d.Feed(MantaStat.Force) && d.fruits[3] == 1 && !d.Feed(MantaStat.Force), "Feeding consumes inventory once");
            Check(Mathf.Abs(d.Strength(MantaStat.Force) - .12f) < .0001f, "Each fed fruit adds permanent strength");
            Check(d.CollectFruit("b", MantaStat.Force) && !d.CollectFruit("c", MantaStat.Force), "Combined inventory and consumed fruit cap is two");
            Check(d.Discover("landmark") && !d.Discover("landmark"), "Location rewards are idempotent");
            Check(d.CollectFruit("obedience", MantaStat.Obedience) && d.Feed(MantaStat.Obedience), "Obedience fruit is implemented without obedience behavior");
            int bondEvents = 0, priorBondLevel = d.BondLevel;
            d.BondLevelChanged += _ => bondEvents++;
            d.AddBond(99999); Check(d.BondLevel == 10 && d.Strength(MantaStat.Speed) == 0, "Bond caps independently of stats");
            Check(bondEvents == 10 - priorBondLevel, "Future Bond unlocks receive every crossed level event");
            d.endurance = 12.5f; d.exhausted = true;
            var restored = JsonUtility.FromJson<MantaProgressionData>(JsonUtility.ToJson(d));
            Check(restored.IsValid() && restored.endurance == 12.5f && restored.exhausted && restored.collectedFruits.Contains("a")
                && restored.inventory[3] == 1 && restored.fruits[4] == 1, "JSON round-trip preserves progression, fatigue, fruit and reward IDs");
            restored.inventory[3] = 3; Check(!restored.IsValid(), "Corrupt fruit counts rejected");
            restored = new MantaProgressionData(); restored.fruits = null;
            Check(!restored.IsValid(), "Malformed save arrays rejected");
            Check(!d.CollectFruit("invalid", (MantaStat)99) && !d.Feed((MantaStat)(-1)), "Invalid stat IDs rejected");
            var legacyState = new MantaProgressionData { level = 2, mantaName = "Saved Manta", endurance = 77 };
            legacyState.CollectFruit("saved-fed", MantaStat.Speed); legacyState.Feed(MantaStat.Speed);
            legacyState.CollectFruit("saved-carried", MantaStat.Speed); legacyState.Discover("saved-landmark", 0);
            string legacy = JsonUtility.ToJson(legacyState).Replace("\"version\":2", "\"version\":1");
            legacy = legacy.Substring(0, legacy.Length - 1) + ",\"points\":1,\"ranks\":[2,1,1,1,1],\"practice\":[2,0,0,0,0],\"training\":[34,0,0,0,0]}";
            var migrated = MantaProgressionSave.Read(legacy);
            Check(migrated != null && migrated.version == 2 && migrated.mantaName == "Saved Manta" && migrated.level == 2
                && migrated.endurance == 77 && migrated.bond == legacyState.bond && migrated.fruits[0] == 1 && migrated.inventory[0] == 1
                && migrated.collectedFruits.Contains("saved-fed") && migrated.discoveries.Contains("saved-landmark"), "Legacy migration preserves fruit, name, level, Bond, fatigue and rewards");
            Check(Mathf.Abs(migrated.Strength(MantaStat.Speed) - .12f) < .0001f, "Retired ranks and practice no longer affect stats");
            string current = JsonUtility.ToJson(migrated);
            Check(!current.Contains("points") && !current.Contains("ranks") && !current.Contains("practice") && !current.Contains("training"), "New saves omit retired training fields");
            Check(MantaProgressionSave.Read(legacy.Replace("\"points\":1", "\"points\":100")) == null, "Corrupt legacy budget is rejected before migration");
            Check(MantaProgressionSave.Read(current) != null && MantaProgressionSave.Read("{\"version\":999}") == null, "Current saves load and unsupported versions are rejected");
            return string.Join("\n", report);
        }

        [MenuItem("Manta/Progression/Validate play mode integration")]
        public static void RunPlayMenu() => Debug.Log(RunPlay());
        public static string RunPlay()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
            var p = UnityEngine.Object.FindFirstObjectByType<MantaProgression>();
            if (p == null || p.Mount == null) throw new Exception("A manta and rider are required.");
            var report = new List<string>();
            void Check(bool condition, string label)
            { if (!condition) throw new Exception("PROGRESSION PLAY FAIL: " + label); report.Add("PASS " + label); }
            var input = p.GetComponent<MantaInput>();
            var panel = p.GetComponent<MantaProgressionPanel>();
            string snapshot = JsonUtility.ToJson(p.Data), originalId = p.saveId;
            float cargo = p.cargoKilograms;
            // A disposable save slot keeps validation out of the player's persisted progression.
            p.saveId = "integration-validation";
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new MantaProgressionData()), p.Data);
                Check(p.Accessible, "Panel available while mounted");
                panel.Open(); Check(input.ProgressionMenuOpen && !input.SettingsMenuOpen, "Progression menu owns input without opening tuning");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(UnityEngine.FindObjectsSortMode.None).Length == 1, "Progression reuses one UI EventSystem");
                float oldSpeed = p.SpeedMultiplier;
                Check(p.CollectFruit("test-speed", MantaStat.Speed) && p.Feed(MantaStat.Speed) && p.SpeedMultiplier > oldSpeed, "Fruit feeding changes flight speed");
                p.Data.AddXP(100000);
                float oldTurn = p.TurnMultiplier; p.CollectFruit("test-handling", MantaStat.Manoeuvrability); p.Feed(MantaStat.Manoeuvrability);
                Check(p.TurnMultiplier > oldTurn, "Handling fruit changes turning");
                float oldWind = p.WindMultiplier; p.CollectFruit("test-force", MantaStat.Force); p.Feed(MantaStat.Force);
                Check(p.WindMultiplier < oldWind, "Force fruit reduces wind drift");
                float oldEndurance = p.MaxEndurance; p.CollectFruit("test-endurance", MantaStat.Endurance); p.Feed(MantaStat.Endurance);
                Check(p.MaxEndurance > oldEndurance, "Endurance fruit increases the stamina reserve");
                var ui = p.transform.Find("Manta Progression UI/Progression");
                Check(ui.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == 6, "Panel contains only Close and five fruit feeding buttons");
                foreach (var label in ui.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    Check(!label.text.Contains("Training") && !label.text.Contains("Practice") && !label.text.Contains("Rank "), "Panel omits retired training text: " + label.name);
                p.cargoKilograms = p.Capacity; Check(p.AccelerationMultiplier < 1 && p.ClimbMultiplier < 1, "Cargo affects acceleration and climbing");
                input.SetProgressionMenu(false); p.cargoKilograms = 0;
                p.Data.endurance = .001f;
                p.SendMessage("FixedUpdate"); Check(p.Failing && !p.CanFly, "Zero endurance enters loss of control");
                for (int i = 0; i < 200; i++) p.SendMessage("FixedUpdate");
                Check(!p.Failing && p.Mount.Landed && p.Mount.rider.State == Rider.RiderState.Grounded, "Failure returns rider to ground beside resting manta");
                Check(p.Data.exhausted && !p.Mount.CanMountGround(), "Exhaustion blocks remounting");
                Check(p.Fade == 0, "Failure fade clears");
                p.Data.endurance = p.MaxEndurance * .3f; p.SendMessage("FixedUpdate");
                Check(!p.Data.exhausted && p.CanFly, "Recovery unlocks flight at 30 percent");
                p.cargoKilograms = p.Capacity + 1;
                Check(!p.CanFly && !p.Mount.CanMountGround(), "Over-capacity load blocks takeoff");
                p.Mount.OrderFollow(); p.Mount.Tick(.02f);
                Check(p.Mount.Landed && p.Mount.Mode == Rider.MantaServiceState.Stay, "Overweight load also blocks autonomous takeoff");
                p.cargoKilograms = 0;
                panel.Open(); Check(panel.IsOpen, "Panel opens beside grounded manta");
                var cargoObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    cargoObject.name = "Progression validation cargo";
                    var item = cargoObject.AddComponent<MantaCargo>(); item.companion = p; item.kilograms = p.Capacity + 1;
                    Check(item.PickUp() && item.Carried && !p.CanFly, "Cargo object transfers its mass and blocks takeoff");
                    Check(item.Drop() && !item.Carried && p.cargoKilograms == 0, "Cargo unloads onto clear ground and restores capacity");
                }
                finally { UnityEngine.Object.DestroyImmediate(cargoObject); }
                Check(UnityEngine.Object.FindObjectsByType<MantaFruit>(UnityEngine.FindObjectsSortMode.None).Length > 0, "Exploration fruit exists in the running world");
                p.Save(); p.Save();
                PlayerPrefs.SetString("Manta.Progression.v1.integration-validation", "{\"version\":999}");
                p.SendMessage("Load");
                Check(p.Data.IsValid() && p.Data.level == 20, "Invalid primary save restores the valid backup");
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(snapshot, p.Data); p.cargoKilograms = cargo; p.saveId = originalId;
                PlayerPrefs.DeleteKey("Manta.Progression.v1.integration-validation"); PlayerPrefs.DeleteKey("Manta.Progression.v1.integration-validation.backup");
                input.SetProgressionMenu(false); p.Controller.ResetFlight();
            }
            return string.Join("\n", report);
        }
    }
}

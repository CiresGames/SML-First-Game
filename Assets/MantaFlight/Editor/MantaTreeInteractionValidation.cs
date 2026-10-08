using System;
using System.Collections.Generic;
using MantaFlight.Rider;
using MantaFlight.Rider.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MantaFlight.Editor
{
    public static class MantaTreeInteractionValidation
    {
        [MenuItem("Manta/Progression/Validate tree punching (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required.");
            var p = UnityEngine.Object.FindFirstObjectByType<MantaProgression>(); var r = p.Mount.rider;
            var snapshot = JsonUtility.ToJson(p.Data); string id = p.saveId; float cargo = p.cargoKilograms;
            p.saveId = "tree-interaction-validation";
            var report = new List<string>();
            void Check(bool ok, string message) { if (!ok) throw new Exception("TREE FAIL: " + message); report.Add("PASS " + message); }
            var origin = new Vector3(-700, 500, -700);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = origin + Vector3.down * .5f; floor.transform.localScale = new Vector3(80, 1, 80);
            var treeObject = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(MantaFruitTree.ResourcePath(MantaStat.Speed)), origin, Quaternion.identity);
            var tree = treeObject.GetComponent<MantaFruitTree>();
            var fruitObject = new GameObject("Validation falling fruit");
            var fruit = fruitObject.AddComponent<MantaFruit>(); fruit.fruitId = "tree-test-fruit"; fruit.stat = MantaStat.Speed; fruit.companion = p;
            fruitObject.transform.position = tree.fruitAnchor.position; tree.fruit = fruit;
            var visual = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(MantaStatStyle.Get(MantaStat.Speed).resource), fruitObject.transform, false); fruit.fruitVisual = visual.transform;
            Gamepad pad = null;
            using var physics = new RiderPhysicsTestScope(r);
            var visuals = r.GetComponentInChildren<RiderVisuals>();
            void Step(RiderCommand command = default)
            {
                RiderPhysicsTestScope.Step(.02f); r.Tick(command, .02f); Physics.SyncTransforms();
                visuals.SendMessage("LateUpdate"); visuals.animator.Update(.02f);
            }
            try
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new MantaProgressionData()), p.Data); p.cargoKilograms = 0;
                r.mantaInput.SetMenu(false); r.TreeInteraction.Cancel();
                p.Controller.SetExternalMotion(origin + new Vector3(40, 10, 0), Quaternion.identity, Vector3.zero); p.Mount.OrderStay();
                Physics.SyncTransforms();
                var wood = tree.GetComponentInChildren<MeshCollider>();
                Check(wood.Raycast(new Ray(origin + new Vector3(0, 1.3f, -10), Vector3.forward), out var hit, 20), "Real tree has a reachable trunk surface");
                Vector3 start = hit.point + hit.normal * 1.1f; start.y = origin.y + .05f;
                r.PlaceForTest(start, Vector3.down, RiderState.Falling);
                for (int i = 0; i < 50; i++) Step();
                Check(r.State == RiderState.Grounded && r.TreeInteraction.Nearby(out _) == tree, "Ground rider can interact near the trunk");
                tree.TryGetPunchPoint(r, 2, out var punchPoint);
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    block.transform.position = Vector3.Lerp(r.transform.position + Vector3.up * 1.3f, hit.point, .5f); block.transform.localScale = new Vector3(.8f, 2, .15f);
                    Physics.SyncTransforms(); Check(!tree.TryGetPunchPoint(r, 2, out _), "Walls block the tree interaction");
                }
                finally { UnityEngine.Object.DestroyImmediate(block); Physics.SyncTransforms(); }
                pad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West)); InputSystem.Update();
                Step(r.Input.Read(.02f));
                Check(r.TreeInteraction.IsBusy && !fruit.Dropped, "X starts approach and wind-up without immediately dropping fruit");
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                for (int i = 0; i < 90 && !fruit.Dropped; i++)
                {
                    if (r.TreeInteraction.PlayingPunch && r.TreeInteraction.PunchTime < RiderTreeInteraction.ImpactTime - .021f)
                        Check(!fruit.Dropped, "Fruit remains attached before impact");
                    Step(new RiderCommand { context = true, jump = true, move = Vector2.up });
                }
                Check(visuals.animator.GetCurrentAnimatorStateInfo(0).IsName("TreePunch"), "Rider plays the authored humanoid punch animation");
                var hand = visuals.animator.GetBoneTransform(HumanBodyBones.RightHand);
                Check(Vector3.Distance(hand.position, punchPoint) < .35f, "Punch hand reaches the trunk at fruit release: " + Vector3.Distance(hand.position, punchPoint).ToString("F2") + " m; hand " + hand.position + "; surface " + punchPoint);
                Check(fruit.Dropped && fruit.Body != null && fruit.Body.useGravity && fruit.Body.angularVelocity.sqrMagnitude > 0, "Punch impact releases a tumbling physics fruit");
                Check(!tree.KnockFruit(r.transform.position) && !tree.HasHangingFruit, "Repeated punches cannot release another fruit");
                fruit.Body.interpolation = RigidbodyInterpolation.None;
                Vector3 position = fruit.Body.position; r.mantaInput.SetProgressionMenu(true); fruit.SendMessage("FixedUpdate");
                visuals.SendMessage("LateUpdate"); Check(visuals.animator.speed == 0, "Menus pause the punch animation");
                for (int i = 0; i < 10; i++) RiderPhysicsTestScope.Step(.02f);
                Check(fruit.Body.isKinematic && Vector3.Distance(position, fruit.Body.position) < .001f, "Menus freeze the falling fruit");
                r.mantaInput.SetProgressionMenu(false); fruit.SendMessage("FixedUpdate");
                Check(!fruit.Body.isKinematic && fruit.Body.linearVelocity.sqrMagnitude > 0, "Closing menus resumes fruit motion");
                for (int i = 0; i < 400; i++) Step();
                Check(!r.TreeInteraction.IsBusy && r.State == RiderState.Grounded, "Punch recovery returns grounded control");
                Check(fruit.Landed && fruit.Body.position.y < origin.y + 2 && fruit.Body.position.y > origin.y, "Dropped fruit lands and stays above solid ground");
                r.Motor.Place(new Vector3(fruit.Body.position.x + 1, origin.y + .05f, fruit.Body.position.z), Quaternion.identity, Vector3.zero);
                for (int i = 0; i < 20 && fruitObject.activeSelf; i++) fruit.SendMessage("Update");
                Check(!fruitObject.activeSelf && p.Data.inventory[0] == 1 && p.Data.collectedFruits.Contains(fruit.fruitId), "Ground pickup adds the same unique fruit to inventory");
                Check(!p.CollectFruit(fruit.fruitId, fruit.stat) && treeObject.activeSelf, "Collected fruit cannot replay and its tree remains");
                return string.Join("\n", report);
            }
            finally
            {
                if (pad != null) InputSystem.RemoveDevice(pad);
                r.TreeInteraction.Cancel(); r.mantaInput.SetMenu(false);
                UnityEngine.Object.DestroyImmediate(fruitObject); UnityEngine.Object.DestroyImmediate(treeObject); UnityEngine.Object.DestroyImmediate(floor);
                JsonUtility.FromJsonOverwrite(snapshot, p.Data); p.saveId = id; p.cargoKilograms = cargo;
                PlayerPrefs.DeleteKey("Manta.Progression.v1.tree-interaction-validation"); PlayerPrefs.DeleteKey("Manta.Progression.v1.tree-interaction-validation.backup");
                p.Controller.ResetFlight();
            }
        }
    }
}

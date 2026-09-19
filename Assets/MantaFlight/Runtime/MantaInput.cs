using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MantaFlight
{
    public enum MantaTrick { None, RollLeft, RollRight, LoopForward, LoopBackward, Turnaround }
    public struct FlightInput
    {
        public Vector2 steering;
        public float throttle, brake, dive;
        public bool tightTurn;
    }

    [DefaultExecutionOrder(-100)]
    public sealed class MantaInput : MonoBehaviour
    {
        public InputActionAsset actions;
        [Range(0, .8f)] public float stickDeadZone = .12f;
        [Range(.5f, 3)] public float sensitivityExponent = 1.4f;
        [Range(.001f, .2f)] public float mouseSensitivity = .035f;
        public bool invertPitch;
        public FlightInput State { get; private set; }
        public bool MenuOpen { get; private set; }
        public string RebindingLabel { get; private set; }
        public event Action ResetRequested;
        public event Action MenuChanged;
        public event Action HudRequested;
        InputActionMap flight;
        InputAction steer, lift, mouse, mouseEnable;
        InputActionRebindingExtensions.RebindingOperation rebind;
        MantaTrick queued;
        Vector2 mouseSteer;
        const string PreferencesKey = "MantaFlight.Bindings.v1";

        void Awake()
        {
            actions = Instantiate(actions);
            if (PlayerPrefs.HasKey(PreferencesKey))
            {
                try { actions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(PreferencesKey)); }
                catch (Exception) { PlayerPrefs.DeleteKey(PreferencesKey); }
            }
            flight = actions.FindActionMap("Flight", true);
            steer = flight.FindAction("Steering", true);
            lift = flight.FindAction("Climb", true);
            mouse = flight.FindAction("MouseSteering", true);
            mouseEnable = flight.FindAction("MouseEnable", true);
            Hook("RollLeft", MantaTrick.RollLeft); Hook("RollRight", MantaTrick.RollRight);
            Hook("LoopForward", MantaTrick.LoopForward); Hook("LoopBackward", MantaTrick.LoopBackward);
            Hook("Turnaround", MantaTrick.Turnaround);
            flight["Reset"].performed += _ => { if (!MenuOpen) ResetRequested?.Invoke(); };
            flight["Menu"].performed += _ => { if (rebind == null) SetMenu(!MenuOpen); };
            flight["HUD"].performed += _ => HudRequested?.Invoke();
        }
        void Hook(string name, MantaTrick trick) => flight[name].performed += _ => { if (!MenuOpen) queued = trick; };
        void OnEnable() { if (actions != null) actions.Enable(); }
        void OnDisable()
        {
            rebind?.Cancel(); actions?.Disable(); queued = MantaTrick.None; State = default;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnDestroy() { rebind?.Dispose(); if (actions != null) Destroy(actions); }
        void Update()
        {
            if (MenuOpen) { State = default; return; }
            Vector2 value = steer.ReadValue<Vector2>();
            if (steer.activeControl == null || steer.activeControl.device is Gamepad)
            {
                // Read bound raw sticks even below the layout's default dead zone: only our configurable curve applies.
                value = Vector2.zero;
                foreach (var control in steer.controls)
                    if (control.device is Gamepad && control is InputControl<Vector2> stick)
                    {
                        Vector2 raw = stick.ReadUnprocessedValue();
                        if (raw.sqrMagnitude > value.sqrMagnitude) value = raw;
                    }
                float amount = Mathf.InverseLerp(stickDeadZone, 1, value.magnitude);
                value = value.normalized * Mathf.Pow(amount, sensitivityExponent);
            }
            if (mouseEnable.IsPressed())
            {
                // Relative virtual stick: responsive steering, with an exponential return to centre.
                mouseSteer = Vector2.ClampMagnitude(mouseSteer + mouse.ReadValue<Vector2>() * mouseSensitivity, 1);
                mouseSteer *= Mathf.Exp(-3 * Time.unscaledDeltaTime);
                value += mouseSteer;
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            }
            else { mouseSteer = Vector2.zero; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            float liftValue = lift.ReadValue<float>();
            if (lift.activeControl == null || lift.activeControl.device is Gamepad)
            {
                float raw = 0;
                foreach (var control in lift.controls)
                    if (control.device is Gamepad && control is InputControl<float> axis && Mathf.Abs(axis.ReadUnprocessedValue()) > Mathf.Abs(raw))
                        raw = axis.ReadUnprocessedValue();
                liftValue = Mathf.Sign(raw) * Mathf.Pow(Mathf.InverseLerp(stickDeadZone, 1, Mathf.Abs(raw)), sensitivityExponent);
            }
            value.y += liftValue;
            value = Vector2.ClampMagnitude(value, 1);
            if (invertPitch) value.y *= -1;
            State = new FlightInput { steering = value, throttle = flight["Accelerate"].ReadValue<float>(),
                brake = flight["Brake"].ReadValue<float>(), dive = flight["Dive"].ReadValue<float>(),
                tightTurn = flight["TightTurn"].IsPressed() };
        }
        public MantaTrick ConsumeTrick() { var result = queued; queued = MantaTrick.None; return result; }
        public void SetMenu(bool open)
        {
            if (!open) CancelRebind();
            MenuOpen = open; queued = MantaTrick.None; mouseSteer = Vector2.zero;
            State = default; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            MenuChanged?.Invoke();
        }
        public void Rebind(string actionName, int bindingIndex)
        {
            if (rebind != null || !MenuOpen) return;
            var action = flight.FindAction(actionName, true);
            if (action.bindings[bindingIndex].isComposite) return;
            action.Disable(); RebindingLabel = actionName;
            var group = action.bindings[bindingIndex].groups;
            rebind = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath(group.Contains("Gamepad") ? "<Gamepad>" : "<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => EndRebind(action, false)).OnComplete(_ => EndRebind(action, true));
            if (!group.Contains("Gamepad")) rebind.WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta");
            rebind.Start();
        }
        void EndRebind(InputAction action, bool save)
        {
            rebind.Dispose(); rebind = null; RebindingLabel = null; action.Enable();
            if (save) { PlayerPrefs.SetString(PreferencesKey, actions.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }
        }
        public void RestoreBindings()
        {
            rebind?.Cancel(); actions.RemoveAllBindingOverrides(); PlayerPrefs.DeleteKey(PreferencesKey);
        }
        public void CancelRebind() => rebind?.Cancel();
    }
}

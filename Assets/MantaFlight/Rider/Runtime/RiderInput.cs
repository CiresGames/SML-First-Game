using UnityEngine;
using UnityEngine.InputSystem;
namespace MantaFlight.Rider
{
    public struct RiderCommand
    {
        public Vector2 move, look, glide;
        public bool run, crouch, jump, roll, context, toggleGlide, call, jumpOff, drop;
    }
    public sealed class RiderInput : MonoBehaviour
    {
        public InputActionAsset actions;
        public RiderSettings settings;
        public RiderUserSettings UserSettings { get; private set; }
        InputActionMap map;
        void Awake() { actions = Instantiate(actions); map = actions.FindActionMap("Rider", true); UserSettings = RiderUserSettings.Load(); }
        void OnEnable() { actions?.Enable(); }
        void OnDisable() { actions?.Disable(); }
        void OnDestroy() { if (actions != null) Destroy(actions); if (UserSettings != null) Destroy(UserSettings); }
        public RiderCommand Read(float dt)
        {
            Vector2 move = map["Move"].ReadValue<Vector2>();
            float amount = Mathf.Pow(Mathf.InverseLerp(settings.inputDeadZone, 1, move.magnitude), settings.inputExponent);
            move = move.normalized * amount;
            Vector2 look = map["Look"].ReadValue<Vector2>();
            look *= map["Look"].activeControl?.device is Mouse ? settings.mouseLookSensitivity : settings.stickLookSensitivity * dt;
            if (map["InvertPitch"].WasPressedThisFrame()) UserSettings.SetInvertPitch(!UserSettings.InvertPitch);
            return new RiderCommand { move = move, look = look, glide = new Vector2(move.x, move.y * (UserSettings.InvertPitch ? -1 : 1)),
                run = map["Run"].IsPressed(), crouch = map["Crouch"].IsPressed(), jump = Press("Jump"), roll = Press("Roll"),
                context = Press("Context"), toggleGlide = Press("Glide"), call = Press("Call"), jumpOff = Press("JumpOff"), drop = Press("Drop") };
        }
        bool Press(string name) => map[name].WasPressedThisFrame();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
namespace MantaFlight.Rider
{
    public struct RiderCommand
    {
        public Vector2 move, look, glide;
        public Vector2 orderSelection, orderMouseDelta;
        public bool ordersPressed, ordersHeld, ordersReleased, orderConfirm, orderCancel;
        public bool run, crouch, jump, roll, context, toggleGlide, call, callHeld, callReleased, jumpOff, drop, lookBack;
    }
    public sealed class RiderInput : MonoBehaviour
    {
        public InputActionAsset actions;
        public RiderSettings settings;
        public RiderUserSettings UserSettings { get; private set; }
        InputActionMap map;
        void Awake() { actions = Instantiate(actions); map = actions.FindActionMap("Rider", true); ConfigureOrders(map); UserSettings = RiderUserSettings.Load(); }
        public static void ConfigureOrders(InputActionMap map)
        {
            var crouch = map.FindAction("Crouch");
            if (crouch != null) for (int i = 0; i < crouch.bindings.Count; i++)
                if (crouch.bindings[i].path == "<Gamepad>/leftShoulder") crouch.ChangeBinding(i).WithPath("<Gamepad>/leftTrigger");
            if (map.FindAction("Orders") == null)
            {
                var action = map.AddAction("Orders", InputActionType.Button);
                action.AddBinding("<Gamepad>/leftShoulder"); action.AddBinding("<Keyboard>/tab");
            }
            if (map.FindAction("OrderSelect") == null)
            {
                var action = map.AddAction("OrderSelect", InputActionType.Value, expectedControlLayout: "Vector2");
                action.AddBinding("<Gamepad>/rightStick");
                action.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            }
            if (map.FindAction("OrderConfirm") == null)
            {
                var action = map.AddAction("OrderConfirm", InputActionType.Button);
                action.AddBinding("<Gamepad>/buttonSouth"); action.AddBinding("<Mouse>/leftButton"); action.AddBinding("<Keyboard>/enter");
            }
            if (map.FindAction("OrderCancel") == null)
            {
                var action = map.AddAction("OrderCancel", InputActionType.Button);
                action.AddBinding("<Gamepad>/buttonEast"); action.AddBinding("<Mouse>/rightButton"); action.AddBinding("<Keyboard>/backspace");
            }
        }
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
                ordersPressed = Press("Orders"), ordersHeld = map["Orders"].IsPressed(), ordersReleased = map["Orders"].WasReleasedThisFrame(),
                orderSelection = map["OrderSelect"].ReadValue<Vector2>(), orderMouseDelta = map["Look"].activeControl?.device is Mouse ? map["Look"].ReadValue<Vector2>() : Vector2.zero,
                orderConfirm = Press("OrderConfirm"), orderCancel = Press("OrderCancel"),
                lookBack = map["LookBack"].IsPressed(), run = map["Run"].IsPressed(), crouch = map["Crouch"].IsPressed(), jump = Press("Jump"), roll = Press("Roll"),
                context = Press("Context"), toggleGlide = Press("Glide"), call = Press("Call"), callHeld = map["Call"].IsPressed(), callReleased = map["Call"].WasReleasedThisFrame(), jumpOff = Press("JumpOff"), drop = Press("Drop") };
        }
        bool Press(string name) => map[name].WasPressedThisFrame();
    }
}

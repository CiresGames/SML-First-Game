using UnityEngine;
using TMPro;

namespace MantaFlight.Rider
{
    public enum MantaOrder { None = -1, Follow, Stay, GoThere, ComeHere }

    public sealed class RiderCommandWheel : MonoBehaviour
    {
        public RiderController rider;
        public bool IsOpen { get; private set; }
        public bool Targeting { get; private set; }
        public MantaOrder Selected { get; private set; } = MantaOrder.None;
        public string Feedback { get; private set; } = "";
        GameObject canvas, wheel, shade;
        TextMeshProUGUI center, detail, footer, hint;
        readonly MantaOrderSector[] sectors = new MantaOrderSector[4];
        readonly TextMeshProUGUI[] labels = new TextMeshProUGUI[4];
        static readonly string[] Names = { "FOLLOW", "STAY", "GO THERE", "COME HERE" };
        static readonly string[] Descriptions = { "Keep close to me", "Wait in place", "Choose a landing point", "Return to me" };
        Vector2 pointer;
        bool mouseSelection;
        float feedbackUntil;
        public static MantaOrder SelectDirection(Vector2 value)
        {
            if (value.magnitude < .3f) return MantaOrder.None;
            if (Mathf.Abs(value.y) >= Mathf.Abs(value.x)) return value.y >= 0 ? MantaOrder.Follow : MantaOrder.GoThere;
            return value.x >= 0 ? MantaOrder.Stay : MantaOrder.ComeHere;
        }
        // Return true when this interaction owns landing input for this frame.
        public bool Handle(ref RiderCommand command)
        {
            if (rider.Attached || rider.State == RiderState.Dismounting || rider.Paused) { Cancel(); return false; }
            if (command.ordersPressed)
            {
                Cancel(); IsOpen = true; Selected = MantaOrder.None; pointer = Vector2.zero; mouseSelection = false;
                rider.LandingTarget.CancelPreview();
            }
            if (IsOpen)
            {
                if (command.orderSelection.sqrMagnitude > .09f) mouseSelection = false;
                else if (command.orderMouseDelta.sqrMagnitude > .01f) mouseSelection = true;
                if (mouseSelection) pointer = Vector2.ClampMagnitude(pointer + command.orderMouseDelta * .008f, 1);
                Selected = SelectDirection(mouseSelection ? pointer : command.orderSelection);
                if (command.orderCancel) Cancel();
                else if (command.ordersReleased)
                {
                    var order = Selected; IsOpen = false;
                    Execute(order);
                }
                else if (!command.ordersHeld) Cancel(); // Lost device/focus must never issue an order.
                command = new RiderCommand { move = command.move, run = command.run, glide = command.glide };
                return true;
            }
            if (Targeting)
            {
                if (command.orderCancel) Cancel();
                else
                {
                    rider.LandingTarget.Tick(true, false);
                    if (command.orderConfirm && rider.LandingTarget.Valid)
                    {
                        rider.LandingTarget.Tick(false, true, true); Targeting = false;
                        Notify(rider.LandingTarget.Valid ? "GO THERE  ·  Landing and waiting" : "Landing unavailable");
                    }
                }
                command = new RiderCommand { move = command.move, run = command.run, look = command.look, glide = command.glide };
                return true;
            }
            return false;
        }
        void Execute(MantaOrder order)
        {
            switch (order)
            {
                case MantaOrder.Follow: rider.mount.OrderFollow(); Notify("FOLLOW  ·  Staying close"); break;
                case MantaOrder.Stay: rider.mount.OrderStay(); Notify("STAY  ·  Waiting in place"); break;
                case MantaOrder.GoThere: Targeting = true; rider.LandingTarget.Tick(true, false); break;
                case MantaOrder.ComeHere: Notify(rider.mount.OrderComeHere() ? "COME HERE  ·  Returning to you" : "COME HERE  ·  No reachable landing nearby or manta too far away"); break;
            }
        }
        void Notify(string message) { Feedback = message; feedbackUntil = Time.unscaledTime + 3; }
        public void Cancel()
        {
            IsOpen = Targeting = false; Selected = MantaOrder.None;
            if (rider && rider.LandingTarget) rider.LandingTarget.CancelPreview();
            if (wheel) wheel.SetActive(false); if (shade) shade.SetActive(false);
        }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void OnDisable() { Cancel(); if (canvas) canvas.SetActive(false); }
        void OnDestroy() { if (canvas) Destroy(canvas); }
        void LateUpdate()
        {
            if (!rider) return;
            if (rider.Paused || rider.Attached) Cancel();
            if (!canvas) BuildUI();
            bool available = !rider.Attached && !rider.Paused && rider.State != RiderState.Dismounting;
            canvas.SetActive(available);
            if (!available) return;
            wheel.SetActive(IsOpen); shade.SetActive(IsOpen);
            for (int i = 0; i < 4; i++)
            {
                bool selected = (int)Selected == i;
                sectors[i].color = selected ? new Color(.83f, .69f, .39f, .98f) : new Color(.025f, .12f, .15f, .96f);
                labels[i].color = selected ? new Color(.04f, .09f, .1f) : new Color(.88f, .95f, .92f);
            }
            center.text = Selected == MantaOrder.None ? "CHOOSE\nAN ORDER" : Names[(int)Selected];
            detail.text = Selected == MantaOrder.None ? "Centre to cancel" : Descriptions[(int)Selected];
            footer.text = "Right stick / Arrows / Mouse: choose\nRelease LB / Tab: issue order     B / Right click: cancel";
            hint.text = IsOpen ? "" : Targeting ? (rider.LandingTarget.Valid ? "GO THERE  ·  A / Click: land here     B / Right click: cancel" : "GO THERE  ·  Aim at clear ground     B / Right click: cancel")
                : Time.unscaledTime < feedbackUntil ? Feedback : "Hold LB / Tab  ·  Manta orders";
        }
        static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        static TextMeshProUGUI Label(string name, Transform parent, string value, Vector2 size, Vector2 position, float fontSize)
        {
            var label = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.88f,.95f,.92f); label.raycastTarget = false; return label;
        }
        void BuildUI()
        {
            canvas = new GameObject("Manta Orders", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 30;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var dim = Rect("Backdrop", canvas.transform, Vector2.zero, Vector2.zero);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            var image = dim.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(0,.025f,.04f,.45f); image.raycastTarget = false; shade = dim.gameObject;
            wheel = Rect("Command Wheel", canvas.transform, new Vector2(540,540), new Vector2(0,20)).gameObject;
            Label("Title", wheel.transform, "MANTA ORDERS", new Vector2(500,50), new Vector2(0,305), 28);
            for (int i = 0; i < 4; i++)
            {
                var sector = Rect(Names[i], wheel.transform, new Vector2(500,500), Vector2.zero).gameObject.AddComponent<MantaOrderSector>();
                sector.angle = i * 90; sector.raycastTarget = false; sectors[i] = sector;
                float angle = i * Mathf.PI * .5f;
                labels[i] = Label("Order", sector.transform, Names[i], new Vector2(156,54), new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*174, 22);
                labels[i].fontStyle = FontStyles.Bold;
            }
            var disc = Rect("Centre", wheel.transform, new Vector2(210,210), Vector2.zero).gameObject.AddComponent<MantaOrderSector>();
            disc.innerRadius = 0; disc.outerRadius = 102; disc.sweep = 360;
            disc.color = new Color(.02f,.075f,.09f,.98f); disc.raycastTarget = false;
            center = Label("Selection", wheel.transform, "", new Vector2(205,64), new Vector2(0,16), 23);
            center.fontStyle = FontStyles.Bold;
            detail = Label("Description", wheel.transform, "", new Vector2(200,54), new Vector2(0,-42), 16);
            footer = Label("Controls", wheel.transform, "", new Vector2(850,70), new Vector2(0,-310), 19);
            hint = Label("Order Status", canvas.transform, "", new Vector2(1100,50), Vector2.zero, 22);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f,0);
            hint.rectTransform.anchoredPosition = new Vector2(0,70);
            wheel.SetActive(false); shade.SetActive(false);
        }
    }
}

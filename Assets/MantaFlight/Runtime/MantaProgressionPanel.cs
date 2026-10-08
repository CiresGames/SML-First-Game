using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MantaFlight
{
    public sealed class MantaProgressionPanel : MonoBehaviour
    {
        MantaProgression progression;
        MantaInput input;
        GameObject canvas, panel, hud;
        TextMeshProUGUI header, footer, enduranceText, hint, toast;
        string lastNotice;
        float noticeUntil;
        UnityEngine.UI.Image bar, fade;
        readonly TextMeshProUGUI[] rows = new TextMeshProUGUI[5], previews = new TextMeshProUGUI[5];
        readonly UnityEngine.UI.Button[] feed = new UnityEngine.UI.Button[5];
        readonly TextMeshProUGUI[] feedLabels = new TextMeshProUGUI[5];
        UnityEngine.UI.Button close;
        TMP_FontAsset font;
        float refresh;
        static readonly Color Ink = new Color(.025f, .065f, .095f, .98f), Jade = new Color(.46f, .92f, .77f);
        public bool IsOpen => input != null && input.ProgressionMenuOpen;
        bool ControllerShortcutAvailable => progression.Mount != null && progression.Mount.rider != null
            && (progression.Mount.rider.Attached || progression.Mount.rider.State == Rider.RiderState.Grounded);

        void Start()
        {
            progression = GetComponent<MantaProgression>(); input = GetComponent<MantaInput>();
            var debug = GetComponent<MantaDebug>(); font = debug ? debug.font : TMP_Settings.defaultFontAsset;
            Build(); input.MenuChanged += Sync; Sync();
        }
        void OnDisable() { if (input != null && input.ProgressionMenuOpen) input.SetProgressionMenu(false); }
        void OnDestroy() { if (input != null) input.MenuChanged -= Sync; if (canvas) Destroy(canvas); }
        public void Open()
        {
            if (input == null || !progression.Accessible || progression.Failing) return;
            input.SetProgressionMenu(true); Refresh();
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }
        void Sync()
        {
            if (panel) panel.SetActive(IsOpen);
            if (hud) hud.SetActive(!input.MenuOpen && (progression.Airborne || progression.Data.exhausted));
            if (hint) hint.gameObject.SetActive(!input.MenuOpen && progression.Accessible);
        }
        void Update()
        {
            if (input == null) return;
            if (lastNotice != progression.Notice) { lastNotice = progression.Notice; noticeUntil = Time.unscaledTime + 6; }
            toast.text = lastNotice;
            toast.transform.parent.gameObject.SetActive(!input.MenuOpen && !string.IsNullOrEmpty(lastNotice) && Time.unscaledTime < noticeUntil);
            bool controllerInfo = Gamepad.current?.buttonNorth.wasPressedThisFrame == true && (IsOpen || ControllerShortcutAvailable);
            if (Keyboard.current?.pKey.wasPressedThisFrame == true || controllerInfo)
            { if (IsOpen) input.SetProgressionMenu(false); else if (!input.SettingsMenuOpen) Open(); }
            if (IsOpen && (Gamepad.current?.buttonEast.wasPressedThisFrame == true || !progression.Accessible || progression.Failing)) input.SetProgressionMenu(false);
            fade.color = new Color(0, 0, 0, progression.Fade); fade.raycastTarget = progression.Failing;
            hud.SetActive(!input.MenuOpen && (progression.Airborne || progression.Data.exhausted));
            float fraction = progression.Endurance01;
            bar.rectTransform.anchorMax = new Vector2(fraction, 1);
            Color color = fraction < .1f ? new Color(1, .35f, .25f) : fraction < .25f ? new Color(1, .73f, .3f) : MantaStatStyle.Color(MantaStat.Endurance);
            bar.color = color;
            enduranceText.text = $"ENDURANCE  {progression.Data.endurance:F0} / {progression.MaxEndurance:F0}"
                + (progression.Data.exhausted ? "   • RESTING" : fraction < .1f ? "   • LAND NOW" : fraction < .25f ? "   • TIRED" : "");
            enduranceText.color = fraction < .25f ? Color.Lerp(color, Color.white, .3f + .3f * Mathf.Sin(Time.unscaledTime * 4)) : Color.white;
            hint.gameObject.SetActive(!input.MenuOpen && progression.Accessible);
            string mountHint = "";
            if (progression.Mount != null && !progression.Mount.rider.Attached && progression.Mount.Landed
                && progression.Mount.WithinGroundMountReach())
                mountHint = !progression.CanFly ? progression.FlightBlockReason + "   |   "
                    : progression.Mount.CanMountGround() ? "X / Square / F • Mount   |   " : "";
            hint.text = mountHint + (ControllerShortcutAvailable ? "Y / Triangle / P" : "P") + " • Manta Info   |   Keyboard B • Cargo";
            refresh -= Time.unscaledDeltaTime;
            if (IsOpen && refresh <= 0) { Refresh(); refresh = .15f; }
        }
        void Refresh()
        {
            var d = progression.Data;
            string xp = d.level == MantaProgressionData.MaxLevel ? "MAX LEVEL" : $"{d.xp:F0} / {d.NextXP:F0} XP";
            string bond = d.BondLevel == 10 ? "MAX" : $"{d.bond:F0} / {d.NextBond:F0}";
            header.text = $"<color=#75EBC4><b>{d.mantaName.ToUpperInvariant()}</b></color>   •   LEVEL {d.level}   /   {xp}\n"
                + $"Endurance {d.endurance:F0}/{progression.MaxEndurance:F0}    •    Bond {d.BondLevel} ({bond})";
            for (int i = 0; i < 5; i++)
            {
                var stat = (MantaStat)i;
                string hex = MantaStatStyle.Get(stat).hex;
                rows[i].text = $"<color=#{hex}><b>{stat.ToString().ToUpperInvariant()}</b></color>\n"
                    + $"Fruit upgrades {d.fruits[i]}/{MantaProgressionData.MaxFruits}   •   In bag {d.inventory[i]}\n<color=#B2C7CD>Permanent bonus +{d.Strength(stat) * 100:F0}%</color>";
                previews[i].text = Describe(stat, false) + (d.fruits[i] < MantaProgressionData.MaxFruits
                    ? $"\n<color=#{hex}>Next fruit: " + Describe(stat, true) + "</color>" : $"\n<color=#{hex}>All fruit upgrades gained</color>");
                feed[i].interactable = d.inventory[i] > 0 && d.fruits[i] < MantaProgressionData.MaxFruits;
                feedLabels[i].text = d.fruits[i] == MantaProgressionData.MaxFruits ? "FULLY FED" : $"FEED FRUIT ({d.inventory[i]})";
            }
            footer.text = $"Cargo {progression.cargoKilograms:F0}/{progression.Capacity:F0} kg   •   {progression.FlightBlockReason}\n"
                + progression.Notice + "\nB: load/unload cargo on the ground. Effects shown at full stamina, without cargo. Bond abilities come later.";
        }
        string Describe(MantaStat stat, bool next)
        {
            float value = progression.Data.Strength(stat, next ? 1 : 0);
            var s = progression.Controller.settings;
            switch (stat)
            {
                case MantaStat.Speed: return $"Top speed {s.maximumSpeed * (1 + .5f * value):F1} m/s • Dive {s.diveMaximumSpeed * (1 + .5f * value):F1} m/s";
                case MantaStat.Manoeuvrability: return $"Turn {s.yawSpeed * (1 + .6f * value):F1}°/s • Response {s.turnAcceleration * (1 + .8f * value):F1}\nInertia control {s.momentumResponse * (1 + .8f * value):F1} • Recovery ×{1 + .8f * value:F2}";
                case MantaStat.Endurance: return $"Stamina {100 * (1 + value):F0} • Cruise ≈{100 * (1 + value) / .18f / 60:F1} min";
                case MantaStat.Force: return $"Capacity {40 + 100 * value:F0} kg • Wind drift {100 / (1 + 2 * value):F0}% of base";
                default: return $"Potential {value * 100:F0}% • Saved for future obedience gameplay";
            }
        }
        void Build()
        {
            canvas = new GameObject("Manta Progression UI", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(transform, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 80;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Manta Progression Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(canvas.transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            panel = Box("Progression", canvas.transform, new Vector2(.5f, .5f), new Vector2(1500, 950), Vector2.zero, Ink).gameObject;
            header = Label(panel.transform, "Header", new Vector2(35, -25), new Vector2(1250, 100), 29);
            close = Button(panel.transform, "CLOSE", new Vector2(1300, -30), new Vector2(160, 52), () => input.SetProgressionMenu(false), out _);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var row = Box("Stat " + (MantaStat)i, panel.transform, new Vector2(0, 1), new Vector2(1430, 128), new Vector2(35, -140 - i * 137), new Color(.055f, .125f, .15f, 1));
                Color statColor = MantaStatStyle.Color((MantaStat)i);
                Box("Stat color", row, new Vector2(0, 1), new Vector2(6, 128), Vector2.zero, statColor);
                rows[i] = Label(row, "Fruit upgrades", new Vector2(18, -15), new Vector2(480, 105), 22);
                previews[i] = Label(row, "Effects and next fruit", new Vector2(510, -12), new Vector2(650, 112), 20);
                feed[i] = Button(row, "FEED", new Vector2(1180, -42), new Vector2(230, 44), () => { progression.Feed((MantaStat)index); Refresh(); }, out feedLabels[i]);
                feedLabels[i].color = statColor;
                Box("Fruit color", feed[i].transform, new Vector2(0, 1), new Vector2(4, 44), Vector2.zero, statColor);
            }
            footer = Label(panel.transform, "Journey", new Vector2(35, -840), new Vector2(1430, 100), 22);
            var hudRect = Box("Endurance HUD", canvas.transform, new Vector2(.5f, 0), new Vector2(630, 82), new Vector2(0, 75), Ink);
            hud = hudRect.gameObject;
            enduranceText = Label(hudRect, "Endurance", new Vector2(18, -8), new Vector2(600, 35), 22);
            var track = Box("Track", hudRect, new Vector2(0, 1), new Vector2(594, 13), new Vector2(18, -53), new Color(.13f, .23f, .27f));
            bar = Box("Fill", track, Vector2.zero, Vector2.zero, Vector2.zero, Jade).GetComponent<UnityEngine.UI.Image>();
            bar.rectTransform.anchorMin = Vector2.zero; bar.rectTransform.anchorMax = Vector2.one; bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            hint = Label(canvas.transform, "Open hint", Vector2.zero, new Vector2(700, 35), 22);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 42); hint.alignment = TextAlignmentOptions.Center;
            var toastBox = Box("Journey notification", canvas.transform, new Vector2(.5f, 1), new Vector2(1000, 60), new Vector2(0, -25), Ink);
            toast = Label(toastBox, "Reward notification", new Vector2(15, -10), new Vector2(970, 45), 24);
            toast.alignment = TextAlignmentOptions.Center; toast.color = Jade;
            fade = Box("Exhaustion fade", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear).GetComponent<UnityEngine.UI.Image>();
            fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one; fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
        }
        RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = position;
            var img = go.GetComponent<UnityEngine.UI.Image>(); img.color = color; img.raycastTarget = false; return rect;
        }
        TextMeshProUGUI Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); if (font) text.font = font;
            text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size; return text;
        }
        UnityEngine.UI.Button Button(Transform parent, string title, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, out TextMeshProUGUI label)
        {
            var rect = Box(title, parent, new Vector2(0, 1), size, position, new Color(.12f, .32f, .31f));
            rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(action);
            label = Label(rect, title, Vector2.zero, size, 20); label.text = title; label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}

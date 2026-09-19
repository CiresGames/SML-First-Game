using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace MantaFlight
{
    public sealed class MantaDebug : MonoBehaviour
    {
        public MantaController controller;
        public TMP_FontAsset font;
        MantaInput input;
        MantaManeuvers maneuvers;
        TextMeshProUGUI readout, status, rebindStatus;
        GameObject menu, hud;
        GameObject hint;
        bool hudVisible = true;
        RectTransform bindingRows;
        string group = "KeyboardMouse";
        float refresh;
        static readonly Color Ink = new Color(.035f, .10f, .15f, .94f);
        static readonly Color Accent = new Color(.48f, .92f, .86f);

        void Start()
        {
            input = controller.GetComponent<MantaInput>(); maneuvers = controller.GetComponent<MantaManeuvers>();
            var canvas = new GameObject("Flight HUD", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Manta UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            hud = Panel("Telemetry", canvas.transform, new Vector2(32, -30), new Vector2(360, 202), Ink).gameObject;
            Label(hud.transform, "M A N T A   /   A I R B O R N E", new Vector2(20, -16), new Vector2(325, 30), 20, Accent);
            readout = Label(hud.transform, "", new Vector2(20, -58), new Vector2(320, 94), 23, Color.white);
            status = Label(hud.transform, "", new Vector2(20, -160), new Vector2(325, 30), 17, Accent);
            var hintLabel = Label(canvas.transform, "VOL LIBRE     •     Échap / Start : réglages et commandes     •     R / Select : retour au départ     •     F1 : HUD",
                new Vector2(32, 30), new Vector2(1650, 36), 19, Color.white);
            hint = hintLabel.gameObject;
            var hintRect = hintLabel.rectTransform; hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0, 0);
            BuildMenu(canvas.transform);
            input.MenuChanged += MenuChanged;
            input.HudRequested += ToggleHud;
        }
        void OnDestroy()
        {
            if (input != null) { input.MenuChanged -= MenuChanged; input.HudRequested -= ToggleHud; }
        }
        void ToggleHud() { hudVisible = !hudVisible; hud.SetActive(hudVisible && !input.MenuOpen); hint.SetActive(hudVisible && !input.MenuOpen); }
        void MenuChanged()
        {
            menu.SetActive(input.MenuOpen);
            hud.SetActive(hudVisible && !input.MenuOpen); hint.SetActive(hudVisible && !input.MenuOpen);
            if (input.MenuOpen) EventSystem.current.SetSelectedGameObject(menu.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
        }
        void Update()
        {
            if (readout == null) return;
            if (Time.unscaledTime < refresh) return;
            refresh = Time.unscaledTime + .1f;
            float roll = Mathf.DeltaAngle(0, controller.Bank + maneuvers.VisualRoll);
            readout.text = $"{controller.Speed * 3.6f:000} km/h   <size=16>{controller.Speed:0.0} m/s</size>\nALT {controller.transform.position.y:000} m   SOL {controller.GroundClearance:000} m\nPITCH {controller.Pitch:+00;-00;00}°    ROLL {roll:+00;-00;00}°";
            status.text = maneuvers.Current != MantaTrick.None ? maneuvers.Current.ToString() :
                input.State.tightTurn ? "VIRAGE SERRÉ" : controller.Pitch > 30 ? "PLONGEON" : controller.Pitch < -30 ? "REMONTÉE" : "VOL LIBRE";
            if (rebindStatus != null) rebindStatus.text = input.RebindingLabel == null ? "Choisir une commande • Échap annule la capture" : "Appuyer sur une nouvelle commande : " + input.RebindingLabel;
        }
        void BuildMenu(Transform parent)
        {
            var panel = Panel("Flight tuning", parent, new Vector2(0, 0), new Vector2(1500, 940), Ink);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            menu = panel.gameObject;
            Label(panel, "L’ATELIER DU VOL", new Vector2(32, -22), new Vector2(800, 50), 34, Accent);
            Button(panel, "REPRENDRE", new Vector2(1240, -26), new Vector2(225, 44), () => input.SetMenu(false));
            Label(panel, "Réglages de session • retour aux valeurs de l’asset à la prochaine lecture", new Vector2(32, -78), new Vector2(1350, 34), 20, Color.white);
            var s = controller.settings;
            float y = -145;
            Slider(panel, "Vitesse maximale", ref y, 35, 95, () => s.maximumSpeed, v => { s.maximumSpeed = v; s.diveMaximumSpeed = Mathf.Max(v, s.diveMaximumSpeed); });
            Slider(panel, "Accélération", ref y, 5, 45, () => s.acceleration, v => s.acceleration = v);
            Slider(panel, "Yaw / virage", ref y, 25, 120, () => s.yawSpeed, v => s.yawSpeed = v);
            Slider(panel, "Pitch / montée", ref y, 25, 110, () => s.pitchSpeed, v => s.pitchSpeed = v);
            Slider(panel, "Inertie directionnelle", ref y, 1, 12, () => s.momentumResponse, v => s.momentumResponse = v);
            Slider(panel, "Inclinaison maximale", ref y, 10, 70, () => s.maximumBanking, v => s.maximumBanking = v);
            Slider(panel, "Distance caméra", ref y, 8, 22, () => s.cameraDistance, v => s.cameraDistance = v);
            Slider(panel, "Retard caméra", ref y, .03f, .6f, () => s.cameraLag, v => s.cameraLag = v);
            Slider(panel, "FOV maximal", ref y, 62, 90, () => s.maximumFOV, v => s.maximumFOV = v);
            Slider(panel, "Animation des ailes", ref y, 0, 2, () => s.animationIntensity, v => s.animationIntensity = v);
            Slider(panel, "Zone morte stick", ref y, 0, .35f, () => input.stickDeadZone, v => input.stickDeadZone = v);
            Slider(panel, "Courbe du stick", ref y, .6f, 2.5f, () => input.sensitivityExponent, v => input.sensitivityExponent = v);
            Button(panel, "Inverser le pitch", new Vector2(32, -825), new Vector2(285, 40), () => input.invertPitch = !input.invertPitch);
            Button(panel, "Replacer la manta", new Vector2(335, -825), new Vector2(285, 40), controller.ResetFlight);
            Button(panel, "Phase : " + s.phase, new Vector2(32, -877), new Vector2(588, 38), () =>
            {
                s.phase = (FlightPhase)(((int)s.phase + 1) % 4);
                controller.GetComponent<MantaManeuvers>().Cancel(); controller.ResetFlight();
                panel.Find("PhaseButton").GetComponentInChildren<TextMeshProUGUI>().text = "Phase : " + s.phase;
            }).name = "PhaseButton";
            Button(panel, "Clavier / souris", new Vector2(700, -132), new Vector2(340, 40), () => { group = "KeyboardMouse"; BuildBindings(); });
            Button(panel, "Manette", new Vector2(1060, -132), new Vector2(380, 40), () => { group = "Gamepad"; BuildBindings(); });
            rebindStatus = Label(panel, "", new Vector2(700, -186), new Vector2(740, 50), 18, Accent);
            bindingRows = Panel("Bindings", panel, new Vector2(700, -245), new Vector2(740, 620), new Color(0, 0, 0, 0));
            BuildBindings();
            Button(panel, "Restaurer les commandes", new Vector2(700, -877), new Vector2(740, 38), () => { input.RestoreBindings(); BuildBindings(); });
            menu.SetActive(false);
        }
        void BuildBindings()
        {
            foreach (Transform child in bindingRows) Destroy(child.gameObject);
            int row = 0;
            foreach (var action in input.actions.FindActionMap("Flight").actions)
            {
                if (action.name == "Menu" || action.name == "HUD" || action.name == "MouseSteering") continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || !binding.groups.Contains(group)) continue;
                    int index = i; string actionName = action.name;
                    int column = row / 14, slot = row % 14;
                    if (column > 1) continue;
                    string title = action.name + (binding.isPartOfComposite ? " / " + binding.name : "");
                    var button = Button(bindingRows, title + " : " + action.GetBindingDisplayString(i),
                        new Vector2(column * 380, -slot * 43), new Vector2(360, 36), () => input.Rebind(actionName, index));
                    var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.fontSize = 15;
                    var updater = button.AddComponent<MantaBindingLabel>(); updater.input = input; updater.actionName = actionName; updater.index = index; updater.title = title; updater.label = label;
                    row++;
                }
            }
        }
        void Slider(Transform parent, string name, ref float y, float min, float max, Func<float> get, Action<float> set)
        {
            var text = Label(parent, name + "   " + get().ToString("0.00"), new Vector2(32, y), new Vector2(330, 30), 19, Color.white);
            var rect = Panel(name, parent, new Vector2(365, y - 4), new Vector2(255, 25), new Color(.15f, .25f, .3f));
            var slider = rect.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.minValue = min; slider.maxValue = max;
            var handle = Panel("Handle", rect, Vector2.zero, new Vector2(14, 30), Accent);
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f); handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero; handle.sizeDelta = new Vector2(14, 0);
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.value = get(); slider.onValueChanged.AddListener(v => { set(v); text.text = name + "   " + v.ToString("0.00"); });
            y -= 55;
        }
        RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = color.a > 0;
            return rect;
        }
        TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); if (font != null) label.font = font;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            label.text = text; label.fontSize = fontSize; label.color = color; label.raycastTarget = false;
            return label;
        }
        GameObject Button(Transform parent, string title, Vector2 position, Vector2 size, Action click)
        {
            var rect = Panel(title, parent, position, size, new Color(.12f, .25f, .3f));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(() => click());
            var label = Label(rect, title, new Vector2(10, -3), size - new Vector2(20, 6), 19, Color.white);
            label.alignment = TextAlignmentOptions.Midline;
            return rect.gameObject;
        }
    }
}

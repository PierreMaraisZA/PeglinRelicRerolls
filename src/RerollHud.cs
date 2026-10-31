using System.Linq;
using PeglinRelicRerolls.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Mod-owned overlay: a reroll button on relic screens, a reroll counter in the shop, and a
    /// die badge with the count beside the relic bar. Built in code because the game's prefabs have
    /// no slot for extra UI.
    /// </summary>
    internal class RerollHud : MonoBehaviour
    {
        private const float ButtonHeight = 72f;
        private const float ButtonPadding = 48f;
        private const float MinButtonWidth = 380f;
        private const float MaxButtonWidth = 1200f;

        private static RerollHud _instance;
        private static double _lastRerollTime = double.NegativeInfinity;

        private GameObject _panel;
        private RectTransform _panelRect;
        private Image _panelImage;
        private Button _button;
        private TextMeshProUGUI _label;

        private GameObject _badge;
        private RectTransform _badgeRect;
        private TextMeshProUGUI _badgeCount;
        private TextMeshProUGUI _badgeCountShadow;
        private bool _badgeStyled;
        private RelicUI _relicUI;
        private float _nextRelicUISearch;
        private string _lastBadgeState;
        private float _nextControllerCheck;
        private bool _controllerConnected;
        private readonly Vector3[] _corners = new Vector3[4];

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }
            GameObject host = new GameObject("PeglinRelicRerollsHud");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<RerollHud>();
        }

        /// <summary>Runs a coroutine on the HUD, which survives scene changes.</summary>
        public static void RunCoroutine(System.Collections.IEnumerator routine)
        {
            Ensure();
            _instance.StartCoroutine(routine);
        }

        private void Awake()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            BuildButton(canvasObject.transform);
        }

        private void BuildButton(Transform parent)
        {
            _panel = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            _panel.transform.SetParent(parent, false);
            _panelRect = _panel.GetComponent<RectTransform>();
            _panelRect.anchorMin = _panelRect.anchorMax = _panelRect.pivot = new Vector2(0.5f, 0f);
            _panelRect.anchoredPosition = new Vector2(0f, 36f);
            _panelRect.sizeDelta = new Vector2(MinButtonWidth, ButtonHeight);

            _panelImage = _panel.GetComponent<Image>();
            Image image = _panelImage;
            image.color = new Color32(92, 58, 36, 240);
            Outline outline = _panel.GetComponent<Outline>();
            outline.effectColor = new Color32(240, 196, 92, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            _button = _panel.GetComponent<Button>();
            ColorBlock colors = _button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
            _button.colors = colors;
            // Never take keyboard/controller focus: otherwise after a mouse click, the next Submit (Space, Enter,
            // controller A) meant for the game's relic buttons would press Reroll again.
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(Reroll);

            _label = CreateText("Label", _panel.transform, 34f, TextAlignmentOptions.Center);
            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(ButtonPadding / 2f, 0f);
            labelRect.offsetMax = new Vector2(-ButtonPadding / 2f, 0f);
            // If a message is wider than the widest button, shrink the text rather than overflow.
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 18f;
            _label.fontSizeMax = 34f;
            _label.color = new Color32(255, 236, 184, 255);

            _panel.SetActive(false);
        }

        /// <summary>
        /// Die icon above the first relic, with the count in its bottom-right like a relic countdown.
        ///
        /// It lives inside the game's own relic bar UI (as a sibling of the relic bar), not on our overlay
        /// canvas. That way it shares the relic bar's canvas, scaling, letterboxing and fades, so it stays
        /// put at any resolution or aspect ratio with no screen/camera conversion. Rebuilt whenever the relic
        /// bar is recreated (e.g. on a scene change, which destroys the old badge with it).
        /// </summary>
        private void BuildBadge(Transform parent)
        {
            if (_badge != null)
            {
                Destroy(_badge);
            }
            _badge = new GameObject("PeglinRelicRerollsBadge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            _badge.transform.SetParent(parent, false);
            _badge.transform.SetAsLastSibling();
            // Don't let a layout group on the relic bar's parent move or resize the badge.
            _badge.GetComponent<LayoutElement>().ignoreLayout = true;
            _badgeRect = _badge.GetComponent<RectTransform>();
            _badgeRect.anchorMin = _badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            _badgeRect.pivot = Vector2.zero;

            Image image = _badge.GetComponent<Image>();
            image.sprite = DieSprite.Get();
            image.preserveAspect = true;
            // Purely informational: never block clicks or aiming underneath.
            image.raycastTarget = false;

            _badgeCountShadow = CreateBadgeText("CountShadow");
            _badgeCountShadow.color = new Color32(20, 12, 18, 255);
            _badgeCount = CreateBadgeText("Count");
            _badgeCount.color = Color.white;
            _badgeStyled = false;
        }

        private TextMeshProUGUI CreateBadgeText(string name)
        {
            TextMeshProUGUI text = CreateText(name, _badge.transform, 36f, TextAlignmentOptions.BottomRight);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            return text;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private void Update()
        {
            UpdateButton(PauseMenu.Paused);
            UpdateBadge();
#if DEBUG
            DevTools.Update();
#endif
        }

        private void UpdateButton(bool paused)
        {
            bool relicScreen = RelicRerollSession.IsActive;
            bool shop = !relicScreen && ShopRerollSession.IsActive;
            bool show = (relicScreen || shop) && !paused;
            if (_panel.activeSelf != show)
            {
                _panel.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            UseGameFont(_label);
            bool canReroll = relicScreen ? RelicRerollSession.CanRerollNow : ShopRerollSession.CanRerollNow;
            _button.interactable = canReroll;
            // A greyed-out button shouldn't swallow clicks meant for whatever is underneath it.
            _panelImage.raycastTarget = canReroll;

            string disabledReason = relicScreen ? RelicRerollSession.DisabledReason : ShopRerollSession.DisabledReason;
            string text;
            if (disabledReason != null)
            {
                text = $"Rerolls - {RerollState.Count} ({disabledReason})";
            }
            else if (relicScreen)
            {
                text = $"Reroll ({RerollState.Count})  [{KeyHint()}]";
            }
            else
            {
                int left = ShopRerollSession.RemainingInShop;
                string limit = left == int.MaxValue ? "" : $", {left} left in this shop";
                text = $"Reroll shop relics ({RerollState.Count}{limit})  [{KeyHint()}]";
            }
            SetButtonText(text);

            if (Plugin.RerollHotkey.Value.IsDown() || Plugin.ControllerRerollButton.Value.IsDown())
            {
                Reroll();
            }
        }

        /// <summary>"T", or "T / R3" once a controller is connected (checked at most once a second).</summary>
        private string KeyHint()
        {
            if (Time.unscaledTime >= _nextControllerCheck)
            {
                _nextControllerCheck = Time.unscaledTime + 1f;
                _controllerConnected = Input.GetJoystickNames().Any(name => !string.IsNullOrEmpty(name));
            }
            string keyboard = Plugin.RerollHotkey.Value.ToString();
            KeyCode button = Plugin.ControllerRerollButton.Value.MainKey;
            return _controllerConnected && button != KeyCode.None ? $"{keyboard} / {ControllerButtonName(button)}" : keyboard;
        }

        /// <summary>Friendly names for the Xbox-layout joystick buttons Unity reports (Steam Input uses this layout).</summary>
        private static string ControllerButtonName(KeyCode button)
        {
            switch (button)
            {
                case KeyCode.JoystickButton0: return "A";
                case KeyCode.JoystickButton1: return "B";
                case KeyCode.JoystickButton2: return "X";
                case KeyCode.JoystickButton3: return "Y";
                case KeyCode.JoystickButton4: return "LB";
                case KeyCode.JoystickButton5: return "RB";
                case KeyCode.JoystickButton6: return "View";
                case KeyCode.JoystickButton7: return "Menu";
                case KeyCode.JoystickButton8: return "L3";
                case KeyCode.JoystickButton9: return "R3";
                default: return button.ToString();
            }
        }

        /// <summary>
        /// Rerolls whatever is on screen: the relic choice screen, or the shop's unbought relics. Ignores presses within
        /// 200ms of the last reroll (keyboard, controller or click), so an accidental double press can't spend two.
        /// </summary>
        private static void Reroll()
        {
            if (!RerollRules.DebouncePassed(Time.unscaledTimeAsDouble, _lastRerollTime))
            {
                return;
            }
            _lastRerollTime = Time.unscaledTimeAsDouble;
            if (RelicRerollSession.IsActive)
            {
                RelicRerollSession.TryReroll();
            }
            else if (ShopRerollSession.IsActive)
            {
                ShopRerollSession.TryReroll();
            }
        }

        /// <summary>Sets the button text and widens the button to fit it (the disabled messages are long).</summary>
        private void SetButtonText(string text)
        {
            if (_label.text == text)
            {
                return;
            }
            _label.text = text;
            float width = _label.GetPreferredValues(text, float.PositiveInfinity, ButtonHeight).x + ButtonPadding;
            _panelRect.sizeDelta = new Vector2(Mathf.Clamp(width, MinButtonWidth, MaxButtonWidth), ButtonHeight);
        }

        private void UpdateBadge()
        {
            // Part of the game's UI now, so it shows and hides with the relic bar (pause menu included).
            string hiddenReason = !Plugin.ShowRerollBadge.Value ? "turned off in config" : RelicBarHiddenReason();
            if (hiddenReason == null && _relicUI.transform.parent == null)
            {
                hiddenReason = "relic bar has no parent to attach to";
            }
            if (hiddenReason != null)
            {
                LogBadgeState($"hidden ({hiddenReason})");
                if (_badge != null && _badge.activeSelf)
                {
                    _badge.SetActive(false);
                }
                return;
            }

            // The relic bar's parent is the badge's home; (re)build if it's missing or the relic bar was replaced.
            Transform home = _relicUI.transform.parent;
            if (_badge == null || _badge.transform.parent != home)
            {
                BuildBadge(home);
            }
            if (!_badge.activeSelf)
            {
                _badge.SetActive(true);
            }
            // Stay drawn on top: anything the game adds next to the relic bar later would otherwise cover the badge.
            if (_badge.transform.GetSiblingIndex() != home.childCount - 1)
            {
                _badge.transform.SetAsLastSibling();
            }
            LogBadgeState($"shown, attached to {PathOf(home)}");

            PositionBadge();
            if (!_badgeStyled)
            {
                _badgeStyled = CopyRelicCountdownStyle();
            }
            UseGameFont(_badgeCount);
            UseGameFont(_badgeCountShadow);
            string count = RerollState.Count.ToString();
            _badgeCount.text = count;
            _badgeCountShadow.text = count;
        }

        /// <summary>
        /// Null while the game's relic bar is on screen (active, its canvas enabled, not faded out); otherwise why not.
        /// If the remembered relic bar is gone or inactive, looks for an active one (at most twice a second), so a
        /// stale relic bar left over from an earlier scene or run can't keep the badge hidden.
        /// </summary>
        private string RelicBarHiddenReason()
        {
            if (_relicUI == null || !_relicUI.isActiveAndEnabled)
            {
                if (Time.unscaledTime < _nextRelicUISearch)
                {
                    return _relicUI == null ? "no relic bar in this scene" : "relic bar inactive";
                }
                _nextRelicUISearch = Time.unscaledTime + 0.5f;
                RelicUI found = FindObjectsOfType<RelicUI>().FirstOrDefault(relicUI => relicUI.isActiveAndEnabled);
                if (found != null && found != _relicUI)
                {
                    _relicUI = found;
                    ModLog.Info($"Reroll badge: using relic bar {PathOf(found.transform)}");
                }
                if (_relicUI == null)
                {
                    return "no relic bar in this scene";
                }
                if (!_relicUI.isActiveAndEnabled)
                {
                    return "relic bar inactive";
                }
            }
            Canvas canvas = _relicUI.GetComponentInParent<Canvas>();
            if (canvas != null && !canvas.enabled)
            {
                return "relic bar canvas disabled";
            }
            float alpha = EffectiveAlpha(_relicUI.transform);
            return alpha > 0.05f ? null : $"relic bar faded out, alpha {alpha:0.00}";
        }

        /// <summary>Logs the badge's state only when it changes, so the log shows why it appears or disappears.</summary>
        private void LogBadgeState(string state)
        {
            if (state != _lastBadgeState)
            {
                _lastBadgeState = state;
                ModLog.Info($"Reroll badge: {state}");
            }
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                path = current.name + "/" + path;
            }
            return $"{path} (scene {transform.gameObject.scene.name})";
        }

        /// <summary>Combined CanvasGroup alpha up the hierarchy, as Unity applies it.</summary>
        private static float EffectiveAlpha(Transform transform)
        {
            float alpha = 1f;
            for (Transform current = transform; current != null; current = current.parent)
            {
                CanvasGroup group = current.GetComponent<CanvasGroup>();
                if (group == null || !group.enabled)
                {
                    continue;
                }
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                {
                    break;
                }
            }
            return alpha;
        }

        /// <summary>
        /// Places the badge directly above the first visible relic icon, at the same size. Everything is
        /// measured in the badge's parent's own coordinates (the same UI as the relic bar), so resolution,
        /// aspect ratio, letterboxing and camera movement can't pull them apart. With no relics yet, it uses
        /// the top-left of the relic bar.
        /// </summary>
        private void PositionBadge()
        {
            RectTransform home = _badge.transform.parent as RectTransform;
            if (_relicUI == null || home == null)
            {
                return;
            }

            RectTransform reference = _relicUI.transform as RectTransform;
            bool aboveIcon = false;
            for (int i = 0; i < _relicUI.transform.childCount; i++)
            {
                Transform child = _relicUI.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy && child.GetComponent<RelicIcon>() != null)
                {
                    reference = (RectTransform)child;
                    aboveIcon = true;
                    break;
                }
            }
            if (reference == null)
            {
                return;
            }

            // World corners (0 bottom-left, 1 top-left, 2 top-right) into the badge parent's local space.
            reference.GetWorldCorners(_corners);
            Vector2 topLeft = home.InverseTransformPoint(_corners[1]);
            Vector2 topRight = home.InverseTransformPoint(_corners[2]);
            Vector2 bottomLeft = home.InverseTransformPoint(_corners[0]);
            float size = Mathf.Min(topRight.x - topLeft.x, topLeft.y - bottomLeft.y);
            if (!aboveIcon)
            {
                // The bare relic bar can be a tall strip; use its width as a stand-in relic size.
                size = topRight.x - topLeft.x;
            }
            if (size <= 0f)
            {
                return;
            }

            float gap = size * 0.12f;
            _badgeRect.localScale = Vector3.one;
            _badgeRect.sizeDelta = new Vector2(size, size);
            _badgeRect.localPosition = new Vector3(topLeft.x, topLeft.y + gap, 0f);

            float fontSize = size * 0.5f;
            _badgeCount.fontSize = fontSize;
            _badgeCountShadow.fontSize = fontSize;
            Vector2 textSize = new Vector2(size, size * 0.6f);
            Vector2 textOffset = new Vector2(size * 0.12f, -size * 0.14f);
            _badgeCount.rectTransform.sizeDelta = textSize;
            _badgeCountShadow.rectTransform.sizeDelta = textSize;
            _badgeCount.rectTransform.anchoredPosition = textOffset;
            _badgeCountShadow.rectTransform.anchoredPosition = textOffset + new Vector2(size * 0.04f, -size * 0.04f);
        }

        /// <summary>Match the font and colours of a relic countdown number if one is on screen.</summary>
        private bool CopyRelicCountdownStyle()
        {
            foreach (RelicIcon icon in FindObjectsOfType<RelicIcon>())
            {
                if (icon.countdownText == null || icon.countdownText.font == null)
                {
                    continue;
                }
                _badgeCount.font = icon.countdownText.font;
                _badgeCount.color = icon.countdownText.color;
                if (icon.countdownTextDropShadow != null)
                {
                    _badgeCountShadow.font = icon.countdownTextDropShadow.font != null ? icon.countdownTextDropShadow.font : icon.countdownText.font;
                    _badgeCountShadow.color = icon.countdownTextDropShadow.color;
                }
                return true;
            }
            return false;
        }

        /// <summary>Borrow the font from any game text on screen so the overlay matches Peglin's style.</summary>
        private static void UseGameFont(TMP_Text target)
        {
            if (target.font != null && target.font != TMP_Settings.defaultFontAsset)
            {
                return;
            }
            foreach (TextMeshProUGUI text in FindObjectsOfType<TextMeshProUGUI>())
            {
                if (text != target && text.font != null && text.font != TMP_Settings.defaultFontAsset)
                {
                    target.font = text.font;
                    return;
                }
            }
        }
    }

    /// <summary>Make sure the HUD exists whenever the relic bar appears, so the badge can show beside it.</summary>
    [HarmonyLib.HarmonyPatch(typeof(RelicUI), "OnEnable")]
    internal static class RelicBarEnablePatch
    {
        private static void Postfix()
        {
            RerollHud.Ensure();
        }
    }
}

using System.Linq;
using BepInEx.Configuration;
using PeglinRelicRerolls.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Mod-owned overlay: a reroll button on relic screens, a reroll counter in the shop, and a
    /// die badge with the count above the first relic. Built in code because the game's prefabs
    /// have no slot for extra UI.
    /// </summary>
    internal class RerollHud : MonoBehaviour
    {
        private const float ButtonHeight = 72f;
        private const float ButtonPadding = 48f;
        private const float MinButtonWidth = 380f;
        private const float MaxButtonWidth = 1200f;
        // The die before the text; 48 is 1.5x the 32px sprite, so its pixels stay even.
        private const float IconSize = 48f;
        private const float IconGap = 14f;
        // TextMeshPro's measured text box (ascender to descender, not the digits' ink), as a fraction of the die:
        // the digits come out about 0.3 of the die tall, matching the game's relic countdown numbers.
        private const float CountHeightRatio = 0.44f;

        private static RerollHud _instance;
        private static double _lastRerollTime = double.NegativeInfinity;

        private GameObject _panel;
        private RectTransform _panelRect;
        private Image _panelImage;
        private Button _button;
        private TextMeshProUGUI _label;
        private Image _icon;

        private GameObject _badge;
        private RectTransform _badgeRect;
        private TextMeshProUGUI _badgeCount;
        private TextMeshProUGUI _badgeCountShadow;
        // Count font size per unit of die height, measured from what TextMeshPro actually draws.
        private float _countFontScale = 0.5f;
        private float _countMeasuredAt = -1f;
        private TMP_FontAsset _countMeasuredFont;
        private bool _countCalibrated;
        private bool _badgeStyled;
        private float _nextStyleAttempt;
        private RelicUI _relicUI;
        private float _nextRelicUISearch;
        private const string ShownState = "shown";
        private string _lastBadgeState;
        private Transform _lastBadgeParent;
        private int _countCalibrationTries;
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

            _icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _icon.transform.SetParent(_panel.transform, false);
            _icon.sprite = DieSprite.Get();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);

            // Icon and label are placed side by side, centred together, in SetButtonText.
            _label = CreateText("Label", _panel.transform, 34f, TextAlignmentOptions.Center);
            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
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
                // Destroy takes effect at the end of the frame; hide it so the old badge can't show beside the new.
                _badge.SetActive(false);
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
            // The button's tint only reaches its background; grey the die with it.
            _icon.color = canReroll ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.75f);

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

            if (Plugin.RerollHotkey.Value.IsDown() || IsButtonDown(Plugin.ControllerRerollButton.Value))
            {
                Reroll();
            }
        }

        /// <summary>
        /// Controller buttons can't use <see cref="KeyboardShortcut.IsDown"/>: it fails while any other key is held,
        /// and Unity reports every pad button twice (JoystickButton9 and Joystick1Button9), so it never passes.
        /// </summary>
        private static bool IsButtonDown(KeyboardShortcut shortcut)
        {
            return ShortcutRules.IsDown(shortcut.MainKey, shortcut.Modifiers, Input.GetKeyDown, Input.GetKey);
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
            const float iconSpace = IconSize + IconGap;
            float textWidth = _label.GetPreferredValues(text, float.PositiveInfinity, ButtonHeight).x;
            float width = Mathf.Clamp(textWidth + iconSpace + ButtonPadding, MinButtonWidth, MaxButtonWidth);
            _panelRect.sizeDelta = new Vector2(width, ButtonHeight);
            // Past the widest button the label gets what's left and its auto-sizing shrinks the text.
            float labelWidth = Mathf.Min(textWidth, width - iconSpace - ButtonPadding);
            float left = -(iconSpace + labelWidth) / 2f;
            _icon.rectTransform.anchoredPosition = new Vector2(left + IconSize / 2f, 0f);
            _label.rectTransform.sizeDelta = new Vector2(labelWidth, ButtonHeight);
            _label.rectTransform.anchoredPosition = new Vector2(left + iconSpace + labelWidth / 2f, 0f);
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
                if (_lastBadgeState != hiddenReason)
                {
                    _lastBadgeState = hiddenReason;
                    ModLog.Info($"Reroll badge: hidden ({hiddenReason})");
                }
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
            if (_lastBadgeState != ShownState || _lastBadgeParent != home)
            {
                _lastBadgeState = ShownState;
                _lastBadgeParent = home;
                ModLog.Info($"Reroll badge: shown, attached to {PathOf(home)}");
            }

            PositionBadge();
            if (!_badgeStyled && Time.unscaledTime >= _nextStyleAttempt)
            {
                // Fall back to another game font only until the relic countdown style has been copied; once it has,
                // the badge must keep the countdown font (it may be TextMeshPro's default asset).
                UseGameFont(_badgeCount);
                UseGameFont(_badgeCountShadow);
                _badgeStyled = CopyRelicCountdownStyle();
                // Until styling succeeds this searches the scene; retry half as often instead of every frame.
                _nextStyleAttempt = Time.unscaledTime + 0.5f;
            }
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
            for (int i = 0; i < _relicUI.transform.childCount; i++)
            {
                Transform child = _relicUI.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy && child.GetComponent<RelicIcon>() != null)
                {
                    reference = (RectTransform)child;
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
            // With no relic icon the reference is the relic bar, which can be a wide or tall strip: the smaller
            // side is the closest stand-in for a relic's size (its width, for the usual tall strip).
            float size = Mathf.Min(topRight.x - topLeft.x, topLeft.y - bottomLeft.y);
            if (size <= 0f)
            {
                return;
            }

            float gap = size * 0.12f;
            _badgeRect.localScale = Vector3.one;
            _badgeRect.sizeDelta = new Vector2(size, size);
            _badgeRect.localPosition = new Vector3(topLeft.x, topLeft.y + gap, 0f);

            // As tall as the game's relic countdowns, whatever font or font asset is in use.
            float fontSize = CountFontSize(size);
            _badgeCount.fontSize = fontSize;
            _badgeCountShadow.fontSize = fontSize;
            // Sit in the die's bottom-right corner, like a relic's countdown number on its icon.
            Vector2 textSize = new Vector2(size * 0.95f, size * 0.7f);
            Vector2 textOffset = new Vector2(-size * 0.02f, size * 0.02f);
            _badgeCount.rectTransform.sizeDelta = textSize;
            _badgeCountShadow.rectTransform.sizeDelta = textSize;
            _badgeCount.rectTransform.anchoredPosition = textOffset;
            _badgeCountShadow.rectTransform.anchoredPosition = textOffset + new Vector2(size * 0.03f, -size * 0.03f);
        }

        /// <summary>
        /// Font size that draws the count <see cref="CountHeightRatio"/> of the die tall.
        ///
        /// A font size number can't be trusted across font assets: each asset carries its own point size, so the
        /// same fontSize can draw several times taller in one font than another. So set a probe size, measure what
        /// TextMeshPro actually draws, and scale from there. Re-measured only when the die size or font changes.
        /// </summary>
        private float CountFontSize(float size)
        {
            bool sizeOrFontChanged = Mathf.Abs(size - _countMeasuredAt) > 0.5f || _countMeasuredFont != _badgeCount.font;
            if (sizeOrFontChanged)
            {
                _countCalibrationTries = 0;
            }
            if (sizeOrFontChanged || !_countCalibrated)
            {
                CalibrateCountFont(size);
            }
            return Mathf.Max(1f, size * _countFontScale);
        }

        private void CalibrateCountFont(float size)
        {
            _countMeasuredAt = size;
            _countMeasuredFont = _badgeCount.font;
            _countCalibrated = false;
            _countFontScale = 0.5f;
            if (string.IsNullOrEmpty(_badgeCount.text))
            {
                return; // Nothing drawn yet; measure on a later frame, once the count is set.
            }
            _badgeCount.fontSize = size;
            _badgeCount.ForceMeshUpdate();
            float drawn = _badgeCount.textBounds.size.y;
            if (drawn <= 0.01f)
            {
                // Nothing measurable (no atlas, or no font at all): stop probing every frame and keep the fallback.
                if (++_countCalibrationTries >= 3)
                {
                    _countCalibrated = true;
                }
                return;
            }
            _countCalibrationTries = 0;
            _countFontScale = Mathf.Clamp(CountHeightRatio * size / drawn, 0.1f, 1.5f);
            _countCalibrated = true;
            ModLog.Info($"Reroll badge: count {drawn:0.#} tall at font size {size:0.#}, scale {_countFontScale:0.00} ({_badgeCount.font?.name})");
        }

        /// <summary>Match the font, colours and size of a relic countdown number if one is on screen.</summary>
        private bool CopyRelicCountdownStyle()
        {
            foreach (RelicIcon icon in FindObjectsOfType<RelicIcon>())
            {
                if (icon.countdownText == null)
                {
                    continue;
                }
                // A countdown with no font asset of its own draws in TextMeshPro's default font, which is what the
                // game's own numbers use, so it has to be adopted too (null would fall back to another font).
                _badgeCount.font = icon.countdownText.font != null ? icon.countdownText.font : TMP_Settings.defaultFontAsset;
                // The game fades countdown numbers by tweening this colour; a copy taken mid-fade would stay dim.
                Color countColor = icon.countdownText.color;
                countColor.a = 1f;
                _badgeCount.color = countColor;
                // The countdown's material carries the thick black outline that keeps its numbers readable over
                // relic art; the die's white pips need the same. Read the shared one: reading fontMaterial would
                // give this game's countdown text a private material instance as a side effect.
                Material countdownMaterial = icon.countdownText.fontSharedMaterial;
                if (countdownMaterial != null)
                {
                    _badgeCount.fontSharedMaterial = countdownMaterial;
                }
                // One font for both copies, so the calibrated size shifts the drop shadow with the number.
                _badgeCountShadow.font = _badgeCount.font;
                if (icon.countdownTextDropShadow != null)
                {
                    Color shadowColor = icon.countdownTextDropShadow.color;
                    shadowColor.a = 1f;
                    _badgeCountShadow.color = shadowColor;
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

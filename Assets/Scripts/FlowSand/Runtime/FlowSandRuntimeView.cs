using System;
using FlowSand.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FlowSand.Runtime
{
    public sealed class FlowSandRuntimeView
    {
        private const float BoardAspectRatio = 0.5f;

        private static readonly Color Background = Hex("070914");
        private static readonly Color Surface = Hex("0D1124");
        private static readonly Color RaisedSurface = Hex("121A35");
        private static readonly Color Accent = Hex("3EC9FF");
        private static readonly Color TextColor = Hex("F4F8FF");
        private static readonly Color Muted = Hex("8FA7C4");
        private Button pauseButton;
        private Button overlayButton;
        private TMP_Text titleText, subtitleText, scoreText, bestText, speedText, messageText;
        private GameObject dimmer;
        private OverlayReveal overlayReveal;
        private ComboPopup comboPopup;
        private TMP_FontAsset fontAsset;
        private int displayedScore = int.MinValue, displayedBest = int.MinValue, displayedSpeed = int.MinValue;
        public RawImage BoardImage { get; private set; }
        public RawImage NextImage { get; private set; }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject go = new("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        public async Awaitable BuildAsync(Action pause, Action overlay, Action leftPress, Action leftRepeat, Action rightPress, Action rightRepeat, Action rotate, Action dropPress, Action dropRelease, Action hardDrop)
        {
            Font sourceFont = Resources.Load<Font>("Fonts/NotoSansSC-FlowSand");
            if (sourceFont == null)
            {
                throw new InvalidOperationException("Missing Chinese font at Resources/Fonts/NotoSansSC-FlowSand.");
            }
            fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            GameObject root = new("FlowSand UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            Image("Background", root.transform, Background, Stretch());
            Transform safe = Container("Safe Area", root.transform, Stretch(new Vector2(42, 42), new Vector2(-42, -42)));

            // Top HUD Bar Layout
            Text("Score Label", safe, GameTexts.Score, 24, Muted, TextAlignmentOptions.TopLeft, L(0, 1, 0, 1, 10, -48, 200, 0));
            scoreText = Text("Score", safe, "0", 72, TextColor, TextAlignmentOptions.BottomLeft, L(0, 1, 0, 1, 10, -135, 250, -45));
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 36;
            scoreText.fontSizeMax = 72;

            Text("Next Label", safe, GameTexts.Next, 24, Muted, TextAlignmentOptions.Center, L(0.5f, 1, 0.5f, 1, -210, -48, -90, 0));
            Transform nextFrame = Container("Next Frame", safe, L(0.5f, 1, 0.5f, 1, -210, -150, -90, -45));
            Image("Border", nextFrame, Hex("24557A"), Stretch());
            Transform nextInset = Container("Inset", nextFrame, Stretch(new Vector2(4, 4), new Vector2(-4, -4)));
            Image("Surface", nextInset, Surface, Stretch());
            NextImage = RawImage("Preview", nextInset, Stretch(new Vector2(10, 10), new Vector2(-10, -10)));

            Text("Speed Label", safe, GameTexts.Speed, 24, Muted, TextAlignmentOptions.Center, L(1, 1, 1, 1, -480, -48, -360, 0));
            speedText = Text("Speed", safe, "1", 38, TextColor, TextAlignmentOptions.Center, L(1, 1, 1, 1, -480, -135, -360, -45));

            Text("Best Label", safe, GameTexts.Best, 24, Muted, TextAlignmentOptions.Center, L(1, 1, 1, 1, -320, -48, -170, 0));
            bestText = Text("Best", safe, "0", 36, Hex("FFCA40"), TextAlignmentOptions.Center, L(1, 1, 1, 1, -320, -135, -170, -45));

            // Maximized Game Area Layout (Centered width, maximized height)
            Transform boardSlot = Container("Board Slot", safe, L(0, 0, 1, 1, 0, 140, 0, -180));
            Transform board = Container("Board Frame", boardSlot, Stretch());
            AspectRatioFitter boardAspect = board.gameObject.AddComponent<AspectRatioFitter>();
            boardAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            boardAspect.aspectRatio = BoardAspectRatio;
            Image("Border", board, Hex("24557A"), Stretch());
            Transform inset = Container("Inset", board, Stretch(new Vector2(5, 5), new Vector2(-5, -5)));
            Image("Surface", inset, Surface, Stretch());
            Transform boardImageSlot = Container("Board Image Slot", inset, Stretch(new Vector2(18, 18), new Vector2(-18, -18)));
            BoardImage = RawImage("Board", boardImageSlot, Stretch());
            AspectRatioFitter boardImageAspect = BoardImage.gameObject.AddComponent<AspectRatioFitter>();
            boardImageAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            boardImageAspect.aspectRatio = BoardAspectRatio;
            BoardImage.raycastTarget = true;
            BoardSwipeInput swipeInput = BoardImage.gameObject.AddComponent<BoardSwipeInput>();
            swipeInput.OnSwipeLeft = leftPress;
            swipeInput.OnSwipeRight = rightPress;
            swipeInput.OnSwipeUp = rotate;
            swipeInput.OnSwipeDownPress = dropPress;
            swipeInput.OnSwipeDownRelease = dropRelease;

            TMP_Text comboText = Text("Combo", boardSlot, "", 54, Accent, TextAlignmentOptions.Center, L(.5f, 1, .5f, 1, -260, -400, 260, -310));
            comboText.fontStyle = FontStyles.Bold;
            comboPopup = comboText.gameObject.AddComponent<ComboPopup>();
            comboPopup.Hide();

            CreateControls(safe, pause, leftPress, leftRepeat, rightPress, rightRepeat, rotate, dropPress, dropRelease);
            dimmer = Image("Dimmer", root.transform, Hex("050710D9"), Stretch()).gameObject;
            dimmer.GetComponent<Image>().raycastTarget = true;
            Transform panel = Container("Overlay", root.transform, L(.5f, .5f, .5f, .5f, -420, -500, 420, 500));
            Image("Border", panel, Accent, Stretch());
            Transform content = Container("Content", panel, Stretch(new Vector2(5, 5), new Vector2(-5, -5)));
            Image("Surface", content, Surface, Stretch());
            titleText = Text("Title", content, GameTexts.GameName, 76, TextColor, TextAlignmentOptions.Center, L(0, 1, 1, 1, 60, -280, -60, -80));
            subtitleText = Text("Subtitle", content, GameTexts.StartSubtitle, 34, Muted, TextAlignmentOptions.Top, L(0, 1, 1, 1, 70, -530, -70, -330));
            messageText = Text("Message", content, "", 34, TextColor, TextAlignmentOptions.Center, L(0, 1, 1, 1, 70, -740, -70, -580));
            overlayButton = Button("Overlay Button", content, GameTexts.Start, L(.5f, 0, .5f, 0, -210, 70, 210, 182), Accent, Background);
            TMP_Text startButtonText = overlayButton.GetComponentInChildren<TMP_Text>();
            startButtonText.fontSize = 40;
            startButtonText.fontStyle = FontStyles.Bold;
            overlayButton.onClick.AddListener(() => overlay());
            panel.gameObject.AddComponent<CanvasGroup>();
            overlayReveal = panel.gameObject.AddComponent<OverlayReveal>();
        }

        public void SetHud(int score, int best, int speed)
        {
            if (score != displayedScore) { displayedScore = score; scoreText.text = score.ToString(); }
            if (best != displayedBest) { displayedBest = best; bestText.text = best.ToString(); }
            if (speed != displayedSpeed) { displayedSpeed = speed; speedText.text = speed.ToString(); }
        }

        public void ShowCombo(int combo) => comboPopup.Show(combo);

        public void HideCombo() => comboPopup.Hide();

        public void SetOverlay(bool visible, string title = null, string subtitle = null, string message = null, string buttonText = null)
        {
            dimmer.SetActive(visible);
            if (title != null) titleText.text = title;
            if (subtitle != null) subtitleText.text = subtitle;
            if (message != null) messageText.text = message;
            if (buttonText != null) overlayButton.GetComponentInChildren<TMP_Text>().text = buttonText;
            if (visible) overlayReveal.Show(); else overlayReveal.Hide();
        }

        public void SetPauseButton(bool visible, string label = GameTexts.Pause)
        {
            pauseButton.gameObject.SetActive(visible);
            pauseButton.GetComponentInChildren<TMP_Text>().text = label;
        }

        private void CreateControls(Transform parent, Action pause, Action leftPress, Action leftRepeat, Action rightPress, Action rightRepeat, Action rotate, Action dropPress, Action dropRelease)
        {
            // Compact Bottom Controls Layout for comfortable one-handed use
            const float secondaryWidth = 160, dropWidth = 200, gap = 14, height = 90, bottom = 20;
            float x = -((secondaryWidth * 4) + dropWidth + (gap * 4)) / 2;
            Button left = IconButton("Left", parent, ControlIcon.Left, L(.5f, 0, .5f, 0, x, bottom, x + secondaryWidth, bottom + height)); x += secondaryWidth + gap;
            Button right = IconButton("Right", parent, ControlIcon.Right, L(.5f, 0, .5f, 0, x, bottom, x + secondaryWidth, bottom + height)); x += secondaryWidth + gap;
            Button rotateButton = IconButton("Rotate", parent, ControlIcon.Rotate, L(.5f, 0, .5f, 0, x, bottom, x + secondaryWidth, bottom + height)); x += secondaryWidth + gap;
            Button drop = Button("Drop", parent, GameTexts.Drop, L(.5f, 0, .5f, 0, x, bottom, x + dropWidth, bottom + height), Accent, Background);
            x += dropWidth + gap;
            pauseButton = Button("Pause", parent, GameTexts.Pause, L(.5f, 0, .5f, 0, x, bottom, x + secondaryWidth, bottom + height), RaisedSurface, TextColor);
            pauseButton.onClick.AddListener(() => pause());
            Hold(left, leftPress, leftRepeat, null); Hold(right, rightPress, rightRepeat, null); Hold(drop, dropPress, null, dropRelease);
            rotateButton.onClick.AddListener(() => rotate());
        }

        private static Transform Container(string name, Transform parent, Layout layout)
        {
            GameObject go = new(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Apply(go.GetComponent<RectTransform>(), layout); return go.transform;
        }
        private static Image Image(string name, Transform parent, Color color, Layout layout)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; Apply(image.rectTransform, layout); return image;
        }
        private static RawImage RawImage(string name, Transform parent, Layout layout)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(RawImage)); go.transform.SetParent(parent, false); RawImage image = go.GetComponent<RawImage>(); image.raycastTarget = false; Apply(image.rectTransform, layout); return image;
        }
        private TextMeshProUGUI Text(string name, Transform parent, string value, int size, Color color, TextAlignmentOptions alignment, Layout layout)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>(); text.font = fontAsset; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Truncate; text.raycastTarget = false; Apply(text.rectTransform, layout); return text;
        }
        private Button Button(string name, Transform parent, string label, Layout layout, Color background, Color foreground)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = background; Button button = go.GetComponent<Button>(); ColorBlock colors = button.colors; colors.highlightedColor = Hex("68D7FF"); colors.pressedColor = Hex("168EC2"); button.colors = colors; Text("Label", go.transform, label, 32, foreground, TextAlignmentOptions.Center, Stretch()); Apply(go.GetComponent<RectTransform>(), layout); return button;
        }
        private Button IconButton(string name, Transform parent, ControlIcon icon, Layout layout)
        {
            Button button = Button(name, parent, string.Empty, layout, RaisedSurface, TextColor);
            RawImage image = RawImage("Icon", button.transform, L(.5f, .5f, .5f, .5f, -28, -28, 28, 28));
            image.texture = CreateControlIcon(icon);
            image.color = TextColor;
            return button;
        }
        private static Texture2D CreateControlIcon(ControlIcon icon)
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            Color32 white = new(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled;
                    if (icon == ControlIcon.Rotate)
                    {
                        float dx = x - 31.5f, dy = y - 31.5f;
                        float radius = Mathf.Sqrt((dx * dx) + (dy * dy));
                        bool ring = radius >= 18f && radius <= 23f;
                        bool arrowHead = x >= 43 && x <= 55 && Mathf.Abs(y - 31) <= (55 - x);
                        filled = ring || arrowHead;
                    }
                    else
                    {
                        int px = icon == ControlIcon.Left ? x : size - 1 - x;
                        bool head = px >= 10 && px <= 36 && Mathf.Abs(y - 32) <= (px - 10);
                        bool shaft = px >= 32 && px <= 54 && y >= 26 && y <= 38;
                        filled = head || shaft;
                    }
                    if (filled) pixels[(y * size) + x] = white;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
        private static void Hold(Button button, Action press, Action repeat, Action release) { HoldButton hold = button.gameObject.AddComponent<HoldButton>(); hold.OnPressed = press; hold.OnRepeated = repeat; hold.OnReleased = release; }
        private static void Apply(RectTransform rect, Layout layout) { rect.anchorMin = layout.Min; rect.anchorMax = layout.Max; rect.offsetMin = layout.OffsetMin; rect.offsetMax = layout.OffsetMax; }
        private static Layout Stretch(Vector2? min = null, Vector2? max = null) => new(Vector2.zero, Vector2.one, min ?? Vector2.zero, max ?? Vector2.zero);
        private static Layout L(float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top) => new(new Vector2(minX, minY), new Vector2(maxX, maxY), new Vector2(left, bottom), new Vector2(right, top));
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color color); return color; }
        private enum ControlIcon { Left, Right, Rotate }
        private readonly struct Layout { public Layout(Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { Min = min; Max = max; OffsetMin = offsetMin; OffsetMax = offsetMax; } public Vector2 Min { get; } public Vector2 Max { get; } public Vector2 OffsetMin { get; } public Vector2 OffsetMax { get; } }
    }
}

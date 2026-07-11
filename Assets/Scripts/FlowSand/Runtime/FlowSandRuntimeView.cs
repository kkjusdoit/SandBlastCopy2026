using System;
using FlowSand.UI;
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
        private Text titleText, subtitleText, scoreText, bestText, speedText, messageText;
        private GameObject dimmer;
        private OverlayReveal overlayReveal;
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

        public async Awaitable BuildAsync(Action pause, Action overlay, Action leftPress, Action leftRepeat, Action rightPress, Action rightRepeat, Action rotate, Action dropPress, Action dropRelease)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new("FlowSand UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            Image("Background", root.transform, Background, Stretch());
            Transform safe = Container("Safe Area", root.transform, Stretch(new Vector2(42, 42), new Vector2(-42, -42)));
            scoreText = Text("Score", safe, "0", 88, TextColor, TextAnchor.LowerLeft, L(0, 1, 0, 1, 0, -130, 360, -10), font);
            Text("Score Label", safe, "CURRENT SCORE", 26, Muted, TextAnchor.UpperLeft, L(0, 1, 0, 1, 0, -64, 280, -10), font);
            bestText = Text("Best", safe, "0", 52, Hex("FFCA40"), TextAnchor.LowerRight, L(1, 1, 1, 1, -290, -132, 0, -36), font);
            Text("Best Label", safe, "PERSONAL BEST", 24, Muted, TextAnchor.UpperRight, L(1, 1, 1, 1, -290, -66, 0, 0), font);
            pauseButton = Button("Pause", safe, "PAUSE", L(.5f, 1, .5f, 1, -95, -76, 95, 0), font, RaisedSurface, TextColor);
            pauseButton.onClick.AddListener(() => pause());
            Transform boardSlot = Container("Board Slot", safe, L(0, 0, 1, 1, 0, 160, -270, -220));
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
            Transform next = Container("Next", safe, L(1, 1, 1, 1, -220, -520, 0, 0));
            Text("Next Label", next, "NEXT", 25, Muted, TextAnchor.MiddleCenter, L(0, 1, 1, 1, 0, -50, 0, 0), font);
            Transform nextFrame = Container("Next Frame", next, L(0, 1, 1, 1, 0, -278, 0, -58));
            Image("Border", nextFrame, Hex("24557A"), Stretch());
            Transform nextInset = Container("Inset", nextFrame, Stretch(new Vector2(4, 4), new Vector2(-4, -4)));
            Image("Surface", nextInset, Surface, Stretch());
            NextImage = RawImage("Preview", nextInset, Stretch(new Vector2(20, 20), new Vector2(-20, -20)));
            Text("Speed Label", next, "SPEED", 23, Muted, TextAnchor.MiddleCenter, L(0, 1, 1, 1, 0, -362, 0, -320), font);
            speedText = Text("Speed", next, "1", 58, TextColor, TextAnchor.MiddleCenter, L(0, 1, 1, 1, 0, -455, 0, -365), font);
            CreateControls(safe, font, leftPress, leftRepeat, rightPress, rightRepeat, rotate, dropPress, dropRelease);
            dimmer = Image("Dimmer", root.transform, Hex("050710D9"), Stretch()).gameObject;
            dimmer.GetComponent<Image>().raycastTarget = true;
            Transform panel = Container("Overlay", root.transform, L(.5f, .5f, .5f, .5f, -420, -360, 420, 360));
            Image("Border", panel, Accent, Stretch());
            Transform content = Container("Content", panel, Stretch(new Vector2(5, 5), new Vector2(-5, -5)));
            Image("Surface", content, Surface, Stretch());
            titleText = Text("Title", content, "FLOW SAND", 76, TextColor, TextAnchor.MiddleCenter, L(0, 1, 1, 1, 60, -250, -60, -60), font);
            subtitleText = Text("Subtitle", content, "Build. Crumble. Connect.", 30, Muted, TextAnchor.UpperCenter, L(0, 1, 1, 1, 70, -420, -70, -270), font);
            messageText = Text("Message", content, "", 26, TextColor, TextAnchor.MiddleCenter, L(0, 1, 1, 1, 70, -540, -70, -440), font);
            overlayButton = Button("Overlay Button", content, "START", L(.5f, 0, .5f, 0, -210, 62, 210, 174), font, Accent, Background);
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

        public void SetOverlay(bool visible, string title = null, string subtitle = null, string message = null, string buttonText = null)
        {
            dimmer.SetActive(visible);
            if (title != null) titleText.text = title;
            if (subtitle != null) subtitleText.text = subtitle;
            if (message != null) messageText.text = message;
            if (buttonText != null) overlayButton.GetComponentInChildren<Text>().text = buttonText;
            if (visible) overlayReveal.Show(); else overlayReveal.Hide();
        }

        public void SetPauseButton(bool visible, string label = "PAUSE")
        {
            pauseButton.gameObject.SetActive(visible);
            pauseButton.GetComponentInChildren<Text>().text = label;
        }

        private void CreateControls(Transform parent, Font font, Action leftPress, Action leftRepeat, Action rightPress, Action rightRepeat, Action rotate, Action dropPress, Action dropRelease)
        {
            const float width = 235, gap = 14, height = 120;
            float x = -((width * 4) + (gap * 3)) / 2;
            Button left = Button("Left", parent, "LEFT", L(.5f, 0, .5f, 0, x, 42, x + width, 42 + height), font, RaisedSurface, TextColor); x += width + gap;
            Button right = Button("Right", parent, "RIGHT", L(.5f, 0, .5f, 0, x, 42, x + width, 42 + height), font, RaisedSurface, TextColor); x += width + gap;
            Button rotateButton = Button("Rotate", parent, "ROTATE", L(.5f, 0, .5f, 0, x, 42, x + width, 42 + height), font, RaisedSurface, TextColor); x += width + gap;
            Button drop = Button("Drop", parent, "DROP", L(.5f, 0, .5f, 0, x, 42, x + width, 42 + height), font, Accent, Background);
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
        private static Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, Layout layout, Font font)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Text text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; Apply(text.rectTransform, layout); return text;
        }
        private static Button Button(string name, Transform parent, string label, Layout layout, Font font, Color background, Color foreground)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = background; Button button = go.GetComponent<Button>(); ColorBlock colors = button.colors; colors.highlightedColor = Hex("68D7FF"); colors.pressedColor = Hex("168EC2"); button.colors = colors; Text("Label", go.transform, label, 32, foreground, TextAnchor.MiddleCenter, Stretch(), font); Apply(go.GetComponent<RectTransform>(), layout); return button;
        }
        private static void Hold(Button button, Action press, Action repeat, Action release) { HoldButton hold = button.gameObject.AddComponent<HoldButton>(); hold.OnPressed = press; hold.OnRepeated = repeat; hold.OnReleased = release; }
        private static void Apply(RectTransform rect, Layout layout) { rect.anchorMin = layout.Min; rect.anchorMax = layout.Max; rect.offsetMin = layout.OffsetMin; rect.offsetMax = layout.OffsetMax; }
        private static Layout Stretch(Vector2? min = null, Vector2? max = null) => new(Vector2.zero, Vector2.one, min ?? Vector2.zero, max ?? Vector2.zero);
        private static Layout L(float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top) => new(new Vector2(minX, minY), new Vector2(maxX, maxY), new Vector2(left, bottom), new Vector2(right, top));
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color color); return color; }
        private readonly struct Layout { public Layout(Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { Min = min; Max = max; OffsetMin = offsetMin; OffsetMax = offsetMax; } public Vector2 Min { get; } public Vector2 Max { get; } public Vector2 OffsetMin { get; } public Vector2 OffsetMax { get; } }
    }
}

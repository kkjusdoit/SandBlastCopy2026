using System;
using FlowSand.UI;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FlowSand.Runtime
{
    public sealed class FlowSandRuntimeView
    {
        public FlowSandRuntimeView(Font font)
        {
            UiFont = font;
        }

        public Font UiFont { get; }
        public RawImage BoardImage { get; private set; }
        public RawImage NextImage { get; private set; }
        public Text TitleText { get; private set; }
        public Text SubtitleText { get; private set; }
        public Text ScoreText { get; private set; }
        public Text HighScoreText { get; private set; }
        public Text LevelText { get; private set; }
        public Text MessageText { get; private set; }
        public Text StartButtonText { get; private set; }
        public Text PauseButtonText { get; private set; }
        public GameObject OverlayPanel { get; private set; }
        public GameObject PauseDimmer { get; private set; }
        public Button StartButton { get; private set; }
        public Button PauseButton { get; private set; }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemGo = new("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        public void Build(Action pauseAction, Action overlayAction, Action moveLeftPress, Action moveLeftRepeat, Action moveRightPress, Action moveRightRepeat, Action rotateAction, Action dropPress, Action dropRelease)
        {
            GameObject canvasGo = new("FlowSandCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject root = CreatePanel("Root", canvas.transform, new Color(0.03f, 0.04f, 0.1f, 1f));
            SetStretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            PauseDimmer = CreatePanel("Dimmer", canvas.transform, new Color(0f, 0f, 0f, 0.62f));
            SetStretch((RectTransform)PauseDimmer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateBoardFrame(root.transform);
            CreateTopHud(root.transform, pauseAction);
            CreateControls(root.transform, moveLeftPress, moveLeftRepeat, moveRightPress, moveRightRepeat, rotateAction, dropPress, dropRelease);
            CreateOverlay(canvas.transform, overlayAction);
        }

        private void CreateBoardFrame(Transform parent)
        {
            GameObject frame = CreatePanel("BoardFrame", parent, new Color32(11, 14, 29, 255));
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = new Vector2(0.09f, 0.22f);
            frameRect.anchorMax = new Vector2(0.71f, 0.88f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color32(55, 206, 255, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            GameObject boardGo = new("BoardTexture");
            boardGo.transform.SetParent(frame.transform, false);
            BoardImage = boardGo.AddComponent<RawImage>();
            BoardImage.color = Color.white;
            RectTransform boardRect = BoardImage.rectTransform;
            boardRect.anchorMin = new Vector2(0.04f, 0.03f);
            boardRect.anchorMax = new Vector2(0.96f, 0.97f);
            boardRect.offsetMin = Vector2.zero;
            boardRect.offsetMax = Vector2.zero;
        }

        private void CreateTopHud(Transform parent, Action pauseAction)
        {
            ScoreText = CreateText("Score", parent, 48, TextAnchor.UpperLeft);
            RectTransform scoreRect = ScoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0.08f, 0.88f);
            scoreRect.anchorMax = new Vector2(0.32f, 0.98f);
            scoreRect.offsetMin = Vector2.zero;
            scoreRect.offsetMax = Vector2.zero;

            HighScoreText = CreateText("Best", parent, 36, TextAnchor.UpperLeft);
            RectTransform bestRect = HighScoreText.rectTransform;
            bestRect.anchorMin = new Vector2(0.08f, 0.82f);
            bestRect.anchorMax = new Vector2(0.32f, 0.88f);
            bestRect.offsetMin = Vector2.zero;
            bestRect.offsetMax = Vector2.zero;

            LevelText = CreateText("Speed", parent, 36, TextAnchor.UpperLeft);
            RectTransform levelRect = LevelText.rectTransform;
            levelRect.anchorMin = new Vector2(0.72f, 0.82f);
            levelRect.anchorMax = new Vector2(0.92f, 0.88f);
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = Vector2.zero;

            Text nextLabel = CreateText("NEXT", parent, 34, TextAnchor.MiddleCenter);
            RectTransform nextLabelRect = nextLabel.rectTransform;
            nextLabelRect.anchorMin = new Vector2(0.74f, 0.9f);
            nextLabelRect.anchorMax = new Vector2(0.92f, 0.95f);
            nextLabelRect.offsetMin = Vector2.zero;
            nextLabelRect.offsetMax = Vector2.zero;

            GameObject previewFrame = CreatePanel("NextFrame", parent, new Color32(10, 12, 28, 255));
            RectTransform previewRect = (RectTransform)previewFrame.transform;
            previewRect.anchorMin = new Vector2(0.75f, 0.74f);
            previewRect.anchorMax = new Vector2(0.91f, 0.88f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewFrame.AddComponent<Outline>().effectColor = new Color32(55, 206, 255, 255);

            GameObject nextGo = new("NextTexture");
            nextGo.transform.SetParent(previewFrame.transform, false);
            NextImage = nextGo.AddComponent<RawImage>();
            RectTransform nextRect = NextImage.rectTransform;
            nextRect.anchorMin = new Vector2(0.1f, 0.1f);
            nextRect.anchorMax = new Vector2(0.9f, 0.9f);
            nextRect.offsetMin = Vector2.zero;
            nextRect.offsetMax = Vector2.zero;

            PauseButton = CreateButton("PauseButton", parent, "Pause", pauseAction);
            PauseButtonText = PauseButton.GetComponentInChildren<Text>();
            RectTransform pauseRect = (RectTransform)PauseButton.transform;
            pauseRect.anchorMin = new Vector2(0.74f, 0.93f);
            pauseRect.anchorMax = new Vector2(0.92f, 0.985f);
            pauseRect.offsetMin = Vector2.zero;
            pauseRect.offsetMax = Vector2.zero;
        }

        private void CreateControls(Transform parent, Action moveLeftPress, Action moveLeftRepeat, Action moveRightPress, Action moveRightRepeat, Action rotateAction, Action dropPress, Action dropRelease)
        {
            CreateHoldButton("LeftButton", parent, "LEFT", new Vector2(0.08f, 0.05f), new Vector2(0.29f, 0.17f), moveLeftPress, moveLeftRepeat, null);
            CreateHoldButton("RightButton", parent, "RIGHT", new Vector2(0.31f, 0.05f), new Vector2(0.52f, 0.17f), moveRightPress, moveRightRepeat, null);
            CreateHoldButton("DropButton", parent, "DROP", new Vector2(0.74f, 0.05f), new Vector2(0.92f, 0.17f), dropPress, null, dropRelease);

            Button rotateButton = CreateButton("RotateButton", parent, "ROTATE", rotateAction);
            RectTransform rotateRect = (RectTransform)rotateButton.transform;
            rotateRect.anchorMin = new Vector2(0.53f, 0.05f);
            rotateRect.anchorMax = new Vector2(0.73f, 0.17f);
            rotateRect.offsetMin = Vector2.zero;
            rotateRect.offsetMax = Vector2.zero;
        }

        private void CreateOverlay(Transform parent, Action overlayAction)
        {
            OverlayPanel = CreatePanel("Overlay", parent, new Color32(12, 16, 33, 235));
            RectTransform overlayRect = (RectTransform)OverlayPanel.transform;
            overlayRect.anchorMin = new Vector2(0.12f, 0.22f);
            overlayRect.anchorMax = new Vector2(0.88f, 0.78f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Outline outline = OverlayPanel.AddComponent<Outline>();
            outline.effectColor = new Color32(55, 206, 255, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            TitleText = CreateText("Title", OverlayPanel.transform, 86, TextAnchor.MiddleCenter);
            RectTransform titleRect = TitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.63f);
            titleRect.anchorMax = new Vector2(0.92f, 0.92f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            SubtitleText = CreateText("Subtitle", OverlayPanel.transform, 36, TextAnchor.MiddleCenter);
            RectTransform subtitleRect = SubtitleText.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.08f, 0.42f);
            subtitleRect.anchorMax = new Vector2(0.92f, 0.64f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            MessageText = CreateText("Message", OverlayPanel.transform, 30, TextAnchor.MiddleCenter);
            RectTransform messageRect = MessageText.rectTransform;
            messageRect.anchorMin = new Vector2(0.08f, 0.25f);
            messageRect.anchorMax = new Vector2(0.92f, 0.4f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;

            StartButton = CreateButton("OverlayButton", OverlayPanel.transform, "START", overlayAction);
            StartButtonText = StartButton.GetComponentInChildren<Text>();
            RectTransform startRect = (RectTransform)StartButton.transform;
            startRect.anchorMin = new Vector2(0.27f, 0.08f);
            startRect.anchorMax = new Vector2(0.73f, 0.2f);
            startRect.offsetMin = Vector2.zero;
            startRect.offsetMax = Vector2.zero;
        }

        private HoldButton CreateHoldButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onPress, Action onRepeat, Action onRelease)
        {
            Button button = CreateButton(name, parent, label, null);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            HoldButton holdButton = button.gameObject.AddComponent<HoldButton>();
            holdButton.OnPressed = onPress;
            holdButton.OnRepeated = onRepeat;
            holdButton.OnReleased = onRelease;
            return holdButton;
        }

        private Button CreateButton(string name, Transform parent, string label, Action onClick)
        {
            GameObject buttonGo = CreatePanel(name, parent, new Color32(18, 31, 62, 255));
            Button button = buttonGo.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color32(22, 35, 74, 255);
            colors.highlightedColor = new Color32(34, 65, 112, 255);
            colors.pressedColor = new Color32(64, 161, 241, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.targetGraphic = buttonGo.GetComponent<Image>();
            button.onClick.AddListener(() => onClick?.Invoke());

            Outline outline = buttonGo.AddComponent<Outline>();
            outline.effectColor = new Color32(62, 201, 255, 255);
            outline.effectDistance = new Vector2(3f, -3f);

            Text text = CreateText(label, buttonGo.transform, 34, TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = 34;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private Text CreateText(string content, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject go = new(content + "Text");
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color32(236, 248, 255, 255);
            return text;
        }

        private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}

using System;
using FlowSand.UI;
using PromptUGUI.Application;
using PromptUGUI.Controls;
using R3;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using PuiRawImage = PromptUGUI.Controls.RawImage;
using PuiText = PromptUGUI.Controls.Text;
using PromptUI = PromptUGUI.Application.UI;
using UnityRawImage = UnityEngine.UI.RawImage;

namespace FlowSand.Runtime
{
    public sealed class FlowSandRuntimeView
    {
        private IScreen screen;
        private Btn pauseButton;
        private Btn overlayButton;
        private PuiText titleText;
        private PuiText subtitleText;
        private PuiText scoreText;
        private PuiText bestText;
        private PuiText speedText;
        private PuiText messageText;
        private IControl overlayPanel;
        private IControl dimmer;
        private OverlayReveal overlayReveal;
        private int displayedScore = int.MinValue;
        private int displayedBest = int.MinValue;
        private int displayedSpeed = int.MinValue;

        public UnityRawImage BoardImage { get; private set; }
        public UnityRawImage NextImage { get; private set; }

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

        public async Awaitable BuildAsync(
            Action pauseAction,
            Action overlayAction,
            Action moveLeftPress,
            Action moveLeftRepeat,
            Action moveRightPress,
            Action moveRightRepeat,
            Action rotateAction,
            Action dropPress,
            Action dropRelease)
        {
            PromptUI.UseResourcesResolver("UI");
            await PromptUI.LoadDocumentAsync("FlowSand.ui");
            PromptUI.Theme.Set("flow-sand");
            screen = PromptUI.Open("FlowSand");

            pauseButton = screen.Get<Btn>("pauseButton");
            overlayButton = screen.Get<Btn>("overlayButton");
            titleText = screen.Get<PuiText>("titleText");
            subtitleText = screen.Get<PuiText>("subtitleText");
            scoreText = screen.Get<PuiText>("scoreText");
            bestText = screen.Get<PuiText>("bestText");
            speedText = screen.Get<PuiText>("speedText");
            messageText = screen.Get<PuiText>("messageText");
            overlayPanel = screen.Get<IControl>("overlayPanel");
            dimmer = screen.Get<IControl>("dimmer");
            overlayReveal = overlayPanel.GameObject.AddComponent<OverlayReveal>();

            BoardImage = screen.Get<PuiRawImage>("boardImage").GameObject.GetComponent<UnityRawImage>();
            NextImage = screen.Get<PuiRawImage>("nextImage").GameObject.GetComponent<UnityRawImage>();

            pauseButton.OnClick.Subscribe(_ => pauseAction()).AddTo(screen);
            overlayButton.OnClick.Subscribe(_ => overlayAction()).AddTo(screen);
            screen.Get<Btn>("rotateButton").OnClick.Subscribe(_ => rotateAction()).AddTo(screen);

            ConfigureHoldButton(screen.Get<Btn>("leftButton"), moveLeftPress, moveLeftRepeat, null);
            ConfigureHoldButton(screen.Get<Btn>("rightButton"), moveRightPress, moveRightRepeat, null);
            ConfigureHoldButton(screen.Get<Btn>("dropButton"), dropPress, null, dropRelease);
        }

        public void SetHud(int score, int best, int speed)
        {
            if (score != displayedScore)
            {
                displayedScore = score;
                scoreText.TextValue = score.ToString();
            }

            if (best != displayedBest)
            {
                displayedBest = best;
                bestText.TextValue = best.ToString();
            }

            if (speed != displayedSpeed)
            {
                displayedSpeed = speed;
                speedText.TextValue = speed.ToString();
            }
        }

        public void SetOverlay(bool visible, string title = null, string subtitle = null, string message = null, string buttonText = null)
        {
            dimmer.GameObject.SetActive(visible);
            if (title != null) titleText.TextValue = title;
            if (subtitle != null) subtitleText.TextValue = subtitle;
            if (message != null) messageText.TextValue = message;
            if (buttonText != null) overlayButton.Text = buttonText;

            if (visible)
            {
                overlayReveal.Show();
            }
            else
            {
                overlayReveal.Hide();
            }
        }

        public void SetPauseButton(bool visible, string label = "PAUSE")
        {
            pauseButton.GameObject.SetActive(visible);
            pauseButton.Text = label;
        }

        private static void ConfigureHoldButton(Btn button, Action onPress, Action onRepeat, Action onRelease)
        {
            HoldButton holdButton = button.GameObject.AddComponent<HoldButton>();
            holdButton.OnPressed = onPress;
            holdButton.OnRepeated = onRepeat;
            holdButton.OnReleased = onRelease;
        }
    }
}

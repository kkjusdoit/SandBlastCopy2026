using System;
using FlowSand.Audio;
using FlowSand.Core;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace FlowSand.Runtime
{
    public sealed class FlowSandGameController : MonoBehaviour
    {
        private const int CoarseCols = 12;
        private const int CoarseRows = 20;
        private const int GrainScale = 16;
        private const string HighScoreKey = "FlowSand.HighScore.CellEquivalentV2";
        private const string SoundEnabledKey = "FlowSand.SoundEnabled";
        private const string VibrationEnabledKey = "FlowSand.VibrationEnabled";
        private const float GameOverRowInterval = 0.05f;
        private const float MaximumGameplayDeltaTime = 0.1f;
        private const int ControlHintUseThreshold = 25;
        private const int MaximumControlHintsPerMatch = 2;
        private const float ControlHintInterval = 60f;
        private const int ClearRuleHintPieceThreshold = 3;

        private readonly Color32 backgroundColor = new(18, 20, 44, 255);
        private readonly Color32 borderColor = new(62, 201, 255, 255);
        private readonly Color32[] palette =
        {
            new(18, 20, 44, 255),
            new(255, 73, 124, 255),
            new(79, 220, 124, 255),
            new(255, 202, 64, 255),
            new(69, 164, 241, 255),
            new(162, 111, 255, 255),
        };

        private FlowSandBoard board;
        private System.Random random;
        private FlowSandRuntimeView view;
        private FlowSandBoardRenderer boardRenderer;
        private FlowSandMatchCoordinator match;
        private FlowSandSfxPlayer sfxPlayer;
        private PlatformKeyboard keyboard;
        private bool softDropHeld;
        private bool uiSoftDropHeld;
        private bool boardVisualDirty;
        private bool nextVisualDirty;
        private bool hudVisualDirty;
        private bool gameOverEffectPlaying;
        private float gameOverEffectTimer;
        private int gameOverOverlayRows;
        private bool initialized;
        private int lockedButtonDirection;
        private int bottomControlUseCount;
        private int controlHintsShownInSession;
        private int piecesLockedThisMatch;
        private float lastControlHintTime;
        private bool soundEnabled;
        private bool vibrationEnabled;
#if UNITY_WEBGL && !UNITY_EDITOR
        private Action<GeneralCallbackResult> onWechatHide;
        private Action<OnShowListenerResult> onWechatShow;
#endif

        private async void Start()
        {
            Application.targetFrameRate = 60;
            keyboard = new PlatformKeyboard();
            random = new System.Random();
            board = new FlowSandBoard(CoarseCols, CoarseRows, GrainScale);
            match = new FlowSandMatchCoordinator(PlayerPrefs.GetInt(HighScoreKey, 0));
            soundEnabled = PlayerPrefs.GetInt(SoundEnabledKey, 1) != 0;
            vibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, 1) != 0;

            FlowSandRuntimeView.EnsureEventSystem();
            ConfigureCamera();
            view = new FlowSandRuntimeView();
            await view.BuildAsync(
                TogglePause,
                OnOverlayButtonPressed,
                RestartFromPause,
                () => TryMove(-1),
                () => TryMove(1),
                BeginSoftDrop,
                () => uiSoftDropHeld = false,
                () => BeginButtonMove(-1),
                () => RepeatButtonMove(-1),
                () => EndButtonMove(-1),
                () => BeginButtonMove(1),
                () => RepeatButtonMove(1),
                () => EndButtonMove(1),
                OnRotateButtonPressed,
                OnDropButtonPressed,
                () => uiSoftDropHeld = false,
                TryHardDrop,
                ToggleSound,
                ToggleVibration);

            sfxPlayer = gameObject.AddComponent<FlowSandSfxPlayer>();
            sfxPlayer.SetEnabled(soundEnabled);
            view.SetSettings(soundEnabled, vibrationEnabled);
            boardRenderer = new FlowSandBoardRenderer(board, view.BoardImage, view.NextImage, palette, backgroundColor, borderColor);

            ShowTitleScreen();
            initialized = true;
            RegisterLifecycleCallbacks();
            InvalidateAllVisuals();
            FlushVisuals();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            HandleKeyboardShortcuts();
            keyboard.EndFrame();
            if (gameOverEffectPlaying)
            {
                UpdateGameOverEffect(Time.unscaledDeltaTime);
                return;
            }

            if (match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, MaximumGameplayDeltaTime);
            GameplayUpdate update = match.UpdateGameplay(board, random, deltaTime, softDropHeld);
            ApplyGameplayUpdate(update);
        }

        private void ApplyGameplayUpdate(GameplayUpdate update)
        {
            boardVisualDirty |= update.BoardChanged;
            hudVisualDirty |= update.HudChanged;
            nextVisualDirty |= update.NextChanged;

            if (update.PieceLocked)
            {
                lockedButtonDirection = 0;
                sfxPlayer.PlayLock();
                piecesLockedThisMatch += 1;
                if (piecesLockedThisMatch == ClearRuleHintPieceThreshold)
                {
                    view.ShowControlHint(GameTexts.ClearRuleHint);
                }
            }

            if (update.Cleared)
            {
                sfxPlayer.PlayClear();
                VibrateOnClear();
                if (match.Combo >= 2)
                {
                    view.ShowCombo(match.Combo);
                }
            }

            if (update.ColorChallenge)
            {
                view.ShowColorChallenge();
            }

            if (update.HighScoreChanged)
            {
                OnHighScoreChanged(match.HighScore);
            }

            if (update.NeedsSpawn)
            {
                SpawnNextPieceOrEnd();
            }
        }

        private void LateUpdate()
        {
            if (initialized)
            {
                FlushVisuals();
            }
        }

        private void HandleKeyboardShortcuts()
        {
#if UNITY_EDITOR
            if (keyboard.GetKeyDown(KeyCode.F8) && match.Phase == FlowSandMatchCoordinator.GamePhase.Playing)
            {
                ApplyGameplayUpdate(match.TriggerColorChallenge(board, random));
            }
#endif
            if ((keyboard.GetKeyDown(KeyCode.Return) || keyboard.GetKeyDown(KeyCode.KeypadEnter)) &&
                !gameOverEffectPlaying &&
                (match.Phase is FlowSandMatchCoordinator.GamePhase.Title or FlowSandMatchCoordinator.GamePhase.GameOver))
            {
                StartGame();
            }

            if (keyboard.GetKeyDown(KeyCode.P) && match.Phase is FlowSandMatchCoordinator.GamePhase.Playing or FlowSandMatchCoordinator.GamePhase.Paused)
            {
                TogglePause();
            }

            if (match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            if (keyboard.GetKeyDown(KeyCode.LeftArrow) || keyboard.GetKeyDown(KeyCode.A))
            {
                TryMove(-1);
            }

            if (keyboard.GetKeyDown(KeyCode.RightArrow) || keyboard.GetKeyDown(KeyCode.D))
            {
                TryMove(1);
            }

            if (keyboard.GetKeyDown(KeyCode.UpArrow) || keyboard.GetKeyDown(KeyCode.W))
            {
                TryRotate();
            }

            if (keyboard.GetKeyDown(KeyCode.Space))
            {
                TryHardDrop();
            }

            bool keyboardSoftDropHeld = keyboard.GetKey(KeyCode.DownArrow) || keyboard.GetKey(KeyCode.S);
            softDropHeld = uiSoftDropHeld || keyboardSoftDropHeld;
        }

        private void OnDestroy()
        {
            UnregisterLifecycleCallbacks();
            keyboard?.Dispose();
            boardRenderer?.Dispose();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                PauseForBackground();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                PauseForBackground();
            }
        }

        private void PauseForBackground()
        {
            softDropHeld = false;
            uiSoftDropHeld = false;
            lockedButtonDirection = 0;
            if (!initialized || match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            match.TogglePause();
            view.SetOverlay(
                true,
                GameTexts.PausedTitle,
                GameTexts.PausedSubtitle,
                GameTexts.PausedInstructions,
                GameTexts.Resume,
                true,
                true);
            view.SetPauseButton(true, GameTexts.Resume);
        }

        private void RegisterLifecycleCallbacks()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            onWechatHide = _ => PauseForBackground();
            onWechatShow = _ =>
            {
                softDropHeld = false;
                uiSoftDropHeld = false;
            };
            WX.OnHide(onWechatHide);
            WX.OnShow(onWechatShow);
            WX.ReportGameStart();
#endif
        }

        private void UnregisterLifecycleCallbacks()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (onWechatHide != null) WX.OffHide(onWechatHide);
            if (onWechatShow != null) WX.OffShow(onWechatShow);
#endif
        }

        private void StartGame()
        {
            board.Reset(random);
            softDropHeld = false;
            uiSoftDropHeld = false;
            lockedButtonDirection = 0;
            gameOverEffectPlaying = false;
            gameOverEffectTimer = 0f;
            gameOverOverlayRows = 0;
            match.StartMatch();

            bottomControlUseCount = 0;
            controlHintsShownInSession = 0;
            piecesLockedThisMatch = 0;
            lastControlHintTime = float.NegativeInfinity;
            view.HideCombo();
            view.HideControlHint();
            view.SetOverlay(false);
            view.SetPauseButton(true);
            view.ShowControlHint(GameTexts.ClearRuleHint);

            SpawnNextPieceOrEnd();
            sfxPlayer.PlayStart();
            InvalidateAllVisuals();
        }

        private void SpawnNextPieceOrEnd()
        {
            if (board.HasActivePiece)
            {
                return;
            }

            if (board.SpawnNextPiece(random))
            {
                match.RegisterPieceSpawned();
                boardVisualDirty = true;
                nextVisualDirty = true;
                return;
            }

            boardVisualDirty = true;
            nextVisualDirty = true;
            match.MarkGameOver();
            view.HideCombo();
            view.SetPauseButton(false);
            gameOverEffectPlaying = true;
            gameOverEffectTimer = 0f;
            gameOverOverlayRows = 0;
            sfxPlayer.PlayGameOver();
        }

        private void UpdateGameOverEffect(float deltaTime)
        {
            gameOverEffectTimer += deltaTime;
            int targetRows = Mathf.Min(
                board.CoarseRows,
                Mathf.FloorToInt(gameOverEffectTimer / GameOverRowInterval) + 1);
            if (targetRows != gameOverOverlayRows)
            {
                gameOverOverlayRows = targetRows;
                boardVisualDirty = true;
            }

            if (gameOverOverlayRows < board.CoarseRows)
            {
                return;
            }

            gameOverEffectPlaying = false;
            view.SetOverlay(
                true,
                GameTexts.GameOverTitle,
                GameTexts.GameOverSubtitle,
                GameTexts.FinalScore(match.Score, match.HighScore),
                GameTexts.Restart);
        }

        private void TogglePause()
        {
            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Title || match.Phase == FlowSandMatchCoordinator.GamePhase.GameOver)
            {
                return;
            }

            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Playing)
            {
                softDropHeld = false;
                uiSoftDropHeld = false;
                lockedButtonDirection = 0;
                match.TogglePause();
                view.SetOverlay(
                    true,
                    GameTexts.PausedTitle,
                    GameTexts.PausedSubtitle,
                    GameTexts.PausedInstructions,
                    GameTexts.Resume,
                    true,
                    true);
                view.SetPauseButton(true, GameTexts.Resume);
                return;
            }

            match.TogglePause();
            view.SetOverlay(false);
            view.SetPauseButton(true);
        }

        private void TryMove(int delta)
        {
            if (!match.CanControlPiece)
            {
                return;
            }

            if (board.TryMoveHorizontal(delta))
            {
                match.RegisterHorizontalOrRotationInput();
                sfxPlayer.PlayMove();
                boardVisualDirty = true;
            }
        }

        private void BeginButtonMove(int direction)
        {
            if (lockedButtonDirection != 0)
            {
                return;
            }

            lockedButtonDirection = direction;
            RegisterBottomControlUse();
            TryMove(direction);
        }

        private void RepeatButtonMove(int direction)
        {
            if (lockedButtonDirection == direction)
            {
                TryMove(direction);
            }
        }

        private void EndButtonMove(int direction)
        {
            if (lockedButtonDirection == direction)
            {
                lockedButtonDirection = 0;
            }
        }

        private void OnRotateButtonPressed()
        {
            RegisterBottomControlUse();
            TryRotate();
        }

        private void OnDropButtonPressed()
        {
            RegisterBottomControlUse();
            BeginSoftDrop();
        }

        private void RegisterBottomControlUse()
        {
            if (controlHintsShownInSession >= MaximumControlHintsPerMatch || match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            bottomControlUseCount += 1;
            if (bottomControlUseCount < ControlHintUseThreshold)
            {
                return;
            }

            if (controlHintsShownInSession > 0 && match.ElapsedTime - lastControlHintTime < ControlHintInterval)
            {
                return;
            }

            controlHintsShownInSession += 1;
            bottomControlUseCount = 0;
            lastControlHintTime = match.ElapsedTime;
            view.ShowControlHint(GameTexts.VirtualJoystickHint);
        }

        private void TryRotate()
        {
            if (!match.CanControlPiece)
            {
                return;
            }

            if (board.TryRotate())
            {
                match.RegisterHorizontalOrRotationInput();
                sfxPlayer.PlayRotate();
                boardVisualDirty = true;
            }
        }

        private void TryHardDrop()
        {
            if (!match.CanControlPiece)
            {
                return;
            }

            GameplayUpdate update = match.HardDrop(board);
            ApplyGameplayUpdate(update);
        }

        private void BeginSoftDrop()
        {
            if (!match.CanControlPiece)
            {
                return;
            }

            uiSoftDropHeld = true;
            if (board.TryStepDown())
            {
                boardVisualDirty = true;
            }
        }

        private void ShowTitleScreen()
        {
            match.ShowTitle();
            view.HideCombo();
            view.SetPauseButton(false);
            view.SetOverlay(
                true,
                GameTexts.GameName,
                GameTexts.StartSubtitle,
                GameTexts.StartInstructions,
                GameTexts.Start,
                false,
                true);
        }

        private void InvalidateAllVisuals()
        {
            boardVisualDirty = true;
            nextVisualDirty = true;
            hudVisualDirty = true;
        }

        private void FlushVisuals()
        {
            if (hudVisualDirty)
            {
                view.SetHud(match.Score, match.HighScore, match.GetSpeedLevel());
                hudVisualDirty = false;
            }

            if (nextVisualDirty)
            {
                boardRenderer.RedrawNext();
                nextVisualDirty = false;
            }

            if (boardVisualDirty)
            {
                boardRenderer.RedrawBoard(match.PendingClearMask, match.FlashVisible, gameOverOverlayRows);
                boardVisualDirty = false;
            }
        }

        private void ConfigureCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.backgroundColor = new Color32(7, 9, 20, 255);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.allowHDR = false;
            mainCamera.allowMSAA = false;
            mainCamera.useOcclusionCulling = false;
        }

        private void OnOverlayButtonPressed()
        {
            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Paused)
            {
                TogglePause();
                return;
            }

            StartGame();
        }

        private void RestartFromPause()
        {
            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Paused)
            {
                StartGame();
            }
        }

        private void OnHighScoreChanged(int highScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }

        private void ToggleSound()
        {
            soundEnabled = !soundEnabled;
            sfxPlayer.SetEnabled(soundEnabled);
            SaveSetting(SoundEnabledKey, soundEnabled);
            view.SetSettings(soundEnabled, vibrationEnabled);
        }

        private void ToggleVibration()
        {
            vibrationEnabled = !vibrationEnabled;
            SaveSetting(VibrationEnabledKey, vibrationEnabled);
            view.SetSettings(soundEnabled, vibrationEnabled);
        }

        private static void SaveSetting(string key, bool enabled)
        {
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void VibrateOnClear()
        {
            if (!vibrationEnabled)
            {
                return;
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            WX.VibrateShort(new VibrateShortOption
            {
                type = "medium",
            });
#endif
        }
    }
}

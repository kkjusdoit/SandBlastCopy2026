using System;
using FlowSand.Audio;
using FlowSand.Core;
using UnityEngine;

namespace FlowSand.Runtime
{
    public sealed class FlowSandGameController : MonoBehaviour
    {
        private const int CoarseCols = 10;
        private const int CoarseRows = 20;
        private const int GrainScale = 16;
        private const string HighScoreKey = "FlowSand.HighScore";
        private const float GameOverRowInterval = 0.05f;

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

        private async void Start()
        {
            Application.targetFrameRate = 60;
            keyboard = new PlatformKeyboard();
            random = new System.Random();
            board = new FlowSandBoard(CoarseCols, CoarseRows, GrainScale);
            match = new FlowSandMatchCoordinator(PlayerPrefs.GetInt(HighScoreKey, 0));

            FlowSandRuntimeView.EnsureEventSystem();
            ConfigureCamera();
            view = new FlowSandRuntimeView();
            await view.BuildAsync(
                TogglePause,
                OnOverlayButtonPressed,
                () => TryMove(-1),
                () => TryMove(-1),
                () => TryMove(1),
                () => TryMove(1),
                TryRotate,
                BeginSoftDrop,
                () => uiSoftDropHeld = false);

            sfxPlayer = gameObject.AddComponent<FlowSandSfxPlayer>();
            boardRenderer = new FlowSandBoardRenderer(board, view.BoardImage, view.NextImage, palette, backgroundColor, borderColor);

            ShowTitleScreen();
            initialized = true;
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

            GameplayUpdate update = match.UpdateGameplay(board, random, Time.unscaledDeltaTime, softDropHeld);
            boardVisualDirty |= update.BoardChanged;
            hudVisualDirty |= update.HudChanged;

            if (update.PieceLocked)
            {
                softDropHeld = false;
                uiSoftDropHeld = false;
                sfxPlayer.PlayLock();
            }

            if (update.Cleared)
            {
                sfxPlayer.PlayClear();
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

            if (keyboard.GetKeyDown(KeyCode.UpArrow) || keyboard.GetKeyDown(KeyCode.W) || keyboard.GetKeyDown(KeyCode.Space))
            {
                TryRotate();
            }

            bool keyboardSoftDropHeld = keyboard.GetKey(KeyCode.DownArrow) || keyboard.GetKey(KeyCode.S);
            softDropHeld = uiSoftDropHeld || keyboardSoftDropHeld;
        }

        private void OnDestroy()
        {
            keyboard?.Dispose();
            boardRenderer?.Dispose();
        }

        private void StartGame()
        {
            board.Reset(random);
            softDropHeld = false;
            uiSoftDropHeld = false;
            gameOverEffectPlaying = false;
            gameOverEffectTimer = 0f;
            gameOverOverlayRows = 0;
            match.StartMatch();

            view.SetOverlay(false);
            view.SetPauseButton(true);

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
                boardVisualDirty = true;
                nextVisualDirty = true;
                return;
            }

            boardVisualDirty = true;
            nextVisualDirty = true;
            match.MarkGameOver();
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
                "ROUND OVER",
                "The sand pile blocked the spawn lane.\nTap to rebuild the board.",
                $"FINAL SCORE  {match.Score}     BEST  {match.HighScore}",
                "RESTART");
        }

        private void TogglePause()
        {
            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Title || match.Phase == FlowSandMatchCoordinator.GamePhase.GameOver)
            {
                return;
            }

            if (match.Phase == FlowSandMatchCoordinator.GamePhase.Playing)
            {
                match.TogglePause();
                view.SetOverlay(
                    true,
                    "PAUSED",
                    "The board is holding its shape.",
                    "PRESS P OR TAP RESUME",
                    "RESUME");
                view.SetPauseButton(true, "RESUME");
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
                sfxPlayer.PlayMove();
                boardVisualDirty = true;
            }
        }

        private void TryRotate()
        {
            if (!match.CanControlPiece)
            {
                return;
            }

            if (board.TryRotate())
            {
                sfxPlayer.PlayRotate();
                boardVisualDirty = true;
            }
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
            view.SetPauseButton(false);
            view.SetOverlay(
                true,
                "FLOW SAND",
                "Drop blocks. Let them crumble.\nBridge one color from edge to edge.",
                "MOVE  /  ROTATE  /  SOFT DROP",
                "START RUN");
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

        private void OnHighScoreChanged(int highScore)
        {
            PlayerPrefs.SetInt(HighScoreKey, highScore);
            PlayerPrefs.Save();
        }
    }
}

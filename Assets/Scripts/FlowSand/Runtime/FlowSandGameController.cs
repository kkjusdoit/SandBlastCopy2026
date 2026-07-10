using System;
using System.Collections.Generic;
using FlowSand.Audio;
using FlowSand.Core;
using UnityEngine;

namespace FlowSand.Runtime
{
    public sealed class FlowSandGameController : MonoBehaviour
    {
        private const int CoarseCols = 10;
        private const int CoarseRows = 20;
        private const int GrainScale = 8;
        private const string HighScoreKey = "FlowSand.HighScore";

        private readonly Color32 backgroundColor = new(18, 20, 44, 255);
        private readonly Color32 borderColor = new(62, 201, 255, 255);
        private readonly Dictionary<CellColor, Color32> palette = new()
        {
            { CellColor.Empty, new Color32(18, 20, 44, 255) },
            { CellColor.Coral, new Color32(255, 73, 124, 255) },
            { CellColor.Mint, new Color32(79, 220, 124, 255) },
            { CellColor.Gold, new Color32(255, 202, 64, 255) },
            { CellColor.Sky, new Color32(69, 164, 241, 255) },
            { CellColor.Violet, new Color32(162, 111, 255, 255) },
        };

        private FlowSandBoard board;
        private System.Random random;
        private FlowSandRuntimeView view;
        private FlowSandBoardRenderer boardRenderer;
        private FlowSandMatchCoordinator match;
        private FlowSandSfxPlayer sfxPlayer;
        private bool softDropHeld;
        private bool initialized;

        private async void Start()
        {
            Application.targetFrameRate = 60;
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
                () => softDropHeld = true,
                () => softDropHeld = false);

            sfxPlayer = gameObject.AddComponent<FlowSandSfxPlayer>();
            boardRenderer = new FlowSandBoardRenderer(board, view.BoardImage, view.NextImage, palette, backgroundColor, borderColor);

            ShowTitleScreen();
            RefreshAllVisuals();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            HandleKeyboardShortcuts();
            if (match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            bool needsSpawn = match.UpdateGameplay(
                board,
                random,
                Time.unscaledDeltaTime,
                softDropHeld,
                () => sfxPlayer.PlayLock(),
                () => sfxPlayer.PlayClear(),
                OnHighScoreChanged);

            if (needsSpawn)
            {
                SpawnNextPieceOrEnd();
            }

            RefreshHud();
            boardRenderer.RedrawBoard(match.PendingClearLookup, match.FlashVisible);
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Return) && match.Phase is FlowSandMatchCoordinator.GamePhase.Title or FlowSandMatchCoordinator.GamePhase.GameOver)
            {
                StartGame();
            }

            if (Input.GetKeyDown(KeyCode.P) && match.Phase is FlowSandMatchCoordinator.GamePhase.Playing or FlowSandMatchCoordinator.GamePhase.Paused)
            {
                TogglePause();
            }

            if (match.Phase != FlowSandMatchCoordinator.GamePhase.Playing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                TryMove(-1);
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                TryMove(1);
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
            {
                TryRotate();
            }

            softDropHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        }

        private void StartGame()
        {
            board.Reset(random);
            softDropHeld = false;
            match.StartMatch();

            view.SetOverlay(false);
            view.SetPauseButton(true);

            SpawnNextPieceOrEnd();
            sfxPlayer.PlayStart();
            RefreshAllVisuals();
        }

        private void SpawnNextPieceOrEnd()
        {
            if (board.HasActivePiece)
            {
                return;
            }

            if (board.SpawnNextPiece(random))
            {
                return;
            }

            match.MarkGameOver();
            view.SetPauseButton(false);
            view.SetOverlay(
                true,
                "ROUND OVER",
                "The sand pile blocked the spawn lane.\nTap to rebuild the board.",
                $"FINAL SCORE  {match.Score}     BEST  {match.HighScore}",
                "RESTART");
            sfxPlayer.PlayGameOver();
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
                boardRenderer.RedrawBoard(match.PendingClearLookup, match.FlashVisible);
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
                boardRenderer.RedrawBoard(match.PendingClearLookup, match.FlashVisible);
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

        private void RefreshAllVisuals()
        {
            RefreshHud();
            boardRenderer.RedrawBoard(match.PendingClearLookup, match.FlashVisible);
            boardRenderer.RedrawNext();
        }

        private void RefreshHud()
        {
            view.SetHud(match.Score, match.HighScore, match.GetSpeedLevel());
            boardRenderer.RedrawNext();
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

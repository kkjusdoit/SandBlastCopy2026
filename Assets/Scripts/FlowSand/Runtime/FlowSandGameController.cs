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

        private void Awake()
        {
            Application.targetFrameRate = 60;
            random = new System.Random();
            board = new FlowSandBoard(CoarseCols, CoarseRows, GrainScale);
            match = new FlowSandMatchCoordinator(PlayerPrefs.GetInt(HighScoreKey, 0));

            FlowSandRuntimeView.EnsureEventSystem();
            ConfigureCamera();
            view = new FlowSandRuntimeView(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            view.Build(
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
        }

        private void Update()
        {
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

            view.PauseDimmer.SetActive(false);
            view.OverlayPanel.SetActive(false);
            view.PauseButton.gameObject.SetActive(true);

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
            view.PauseButton.gameObject.SetActive(false);
            view.PauseDimmer.SetActive(true);
            view.OverlayPanel.SetActive(true);
            view.TitleText.text = "ROUND OVER";
            view.SubtitleText.text = "The sand pile blocked the spawn lane.\nTap to rebuild the board.";
            view.StartButtonText.text = "RESTART";
            view.MessageText.text = $"Final Score {match.Score}\nBest {match.HighScore}";
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
                view.PauseDimmer.SetActive(true);
                view.OverlayPanel.SetActive(true);
                view.TitleText.text = "PAUSED";
                view.SubtitleText.text = "Flow keeps its state. Jump back in when ready.";
                view.MessageText.text = "Press P or tap Resume";
                view.StartButtonText.text = "RESUME";
                view.PauseButtonText.text = "Resume";
                return;
            }

            match.TogglePause();
            view.PauseDimmer.SetActive(false);
            view.OverlayPanel.SetActive(false);
            view.PauseButtonText.text = "Pause";
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
            view.PauseDimmer.SetActive(true);
            view.OverlayPanel.SetActive(true);
            view.PauseButton.gameObject.SetActive(false);
            view.TitleText.text = "FLOW SAND\nTETRIS";
            view.SubtitleText.text = "Drop blocks, let them crumble into sand,\nand bridge one color from left to right.";
            view.MessageText.text = "Mobile portrait prototype\nButtons: move, rotate, soft drop";
            view.StartButtonText.text = "START";
        }

        private void RefreshAllVisuals()
        {
            RefreshHud();
            boardRenderer.RedrawBoard(match.PendingClearLookup, match.FlashVisible);
            boardRenderer.RedrawNext();
        }

        private void RefreshHud()
        {
            view.ScoreText.text = $"Score\n{match.Score}";
            view.HighScoreText.text = $"Best\n{match.HighScore}";
            view.LevelText.text = $"Speed\n{match.GetSpeedLevel()}";
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

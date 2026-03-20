using System;
using System.Collections.Generic;
using FlowSand.Core;
using UnityEngine;

namespace FlowSand.Runtime
{
    public sealed class FlowSandMatchCoordinator
    {
        private readonly List<int> pendingClearIndices = new();
        private readonly HashSet<int> pendingClearLookup = new();

        private float pieceFallTimer;
        private float sandTimer;
        private float clearTimer;

        public FlowSandMatchCoordinator(int highScore)
        {
            HighScore = highScore;
            Phase = GamePhase.Title;
            FlashVisible = true;
        }

        public GamePhase Phase { get; private set; }
        public int Score { get; private set; }
        public int HighScore { get; private set; }
        public int Combo { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool FlashVisible { get; private set; }
        public bool HasPendingClear => pendingClearIndices.Count > 0;
        public bool CanControlPiece => Phase == GamePhase.Playing && !HasPendingClear;
        public HashSet<int> PendingClearLookup => pendingClearLookup;

        public void ShowTitle()
        {
            Phase = GamePhase.Title;
        }

        public void StartMatch()
        {
            Score = 0;
            Combo = 0;
            ElapsedTime = 0f;
            pieceFallTimer = 0f;
            sandTimer = 0f;
            clearTimer = 0f;
            pendingClearIndices.Clear();
            pendingClearLookup.Clear();
            FlashVisible = true;
            Phase = GamePhase.Playing;
        }

        public void MarkGameOver()
        {
            Phase = GamePhase.GameOver;
        }

        public void TogglePause()
        {
            if (Phase == GamePhase.Title || Phase == GamePhase.GameOver)
            {
                return;
            }

            Phase = Phase == GamePhase.Playing ? GamePhase.Paused : GamePhase.Playing;
        }

        public bool UpdateGameplay(FlowSandBoard board, System.Random random, float deltaTime, bool softDropHeld, Action onPieceLock, Action onClear, Action<int> onHighScoreChanged)
        {
            if (Phase != GamePhase.Playing)
            {
                return false;
            }

            ElapsedTime += deltaTime;
            UpdatePieceFall(board, deltaTime, softDropHeld, onPieceLock);
            UpdateSand(board, random, deltaTime);
            UpdateClears(board, deltaTime, onClear, onHighScoreChanged);

            return !board.HasActivePiece && !HasPendingClear;
        }

        public float GetCurrentDropInterval()
        {
            float difficulty = Mathf.Clamp01((ElapsedTime / 120f) + (Score / 6000f));
            return Mathf.Lerp(0.7f, 0.17f, difficulty);
        }

        public int GetSpeedLevel()
        {
            return Mathf.RoundToInt(Mathf.Lerp(1f, 10f, 1f - GetCurrentDropInterval() / 0.7f));
        }

        private void UpdatePieceFall(FlowSandBoard board, float deltaTime, bool softDropHeld, Action onPieceLock)
        {
            if (!board.HasActivePiece || HasPendingClear)
            {
                return;
            }

            pieceFallTimer += deltaTime;
            float stepInterval = GetCurrentDropInterval();
            if (softDropHeld)
            {
                stepInterval *= 0.16f;
            }

            while (pieceFallTimer >= stepInterval)
            {
                pieceFallTimer -= stepInterval;
                if (board.TryStepDown())
                {
                    continue;
                }

                board.LockCurrentPiece();
                Combo = 0;
                onPieceLock?.Invoke();
                break;
            }
        }

        private void UpdateSand(FlowSandBoard board, System.Random random, float deltaTime)
        {
            sandTimer += deltaTime;
            const float sandStepInterval = 0.035f;

            while (sandTimer >= sandStepInterval)
            {
                sandTimer -= sandStepInterval;
                board.StepSand(random);
            }
        }

        private void UpdateClears(FlowSandBoard board, float deltaTime, Action onClear, Action<int> onHighScoreChanged)
        {
            if (HasPendingClear)
            {
                clearTimer -= deltaTime;
                FlashVisible = Mathf.FloorToInt(clearTimer / 0.08f) % 2 == 0;
                if (clearTimer > 0f)
                {
                    return;
                }

                board.ClearCells(pendingClearIndices);
                int cleared = pendingClearIndices.Count;
                pendingClearIndices.Clear();
                pendingClearLookup.Clear();
                Combo += 1;
                Score += cleared * 2 * Combo;
                if (Score > HighScore)
                {
                    HighScore = Score;
                    onHighScoreChanged?.Invoke(HighScore);
                }

                onClear?.Invoke();
                return;
            }

            IReadOnlyList<int> clearCells = board.FindBridgeClearCells();
            if (clearCells.Count == 0)
            {
                FlashVisible = true;
                return;
            }

            pendingClearIndices.Clear();
            pendingClearLookup.Clear();
            for (int i = 0; i < clearCells.Count; i++)
            {
                int index = clearCells[i];
                pendingClearIndices.Add(index);
                pendingClearLookup.Add(index);
            }

            clearTimer = 0.28f;
            FlashVisible = true;
        }

        public enum GamePhase
        {
            Title,
            Playing,
            Paused,
            GameOver,
        }
    }
}

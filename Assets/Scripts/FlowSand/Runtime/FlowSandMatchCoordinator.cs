using System;
using System.Collections.Generic;
using FlowSand.Core;
using UnityEngine;

namespace FlowSand.Runtime
{
    public readonly struct GameplayUpdate
    {
        private const int NeedsSpawnFlag = 1 << 0;
        private const int BoardChangedFlag = 1 << 1;
        private const int HudChangedFlag = 1 << 2;
        private const int PieceLockedFlag = 1 << 3;
        private const int ClearedFlag = 1 << 4;
        private const int HighScoreChangedFlag = 1 << 5;

        internal GameplayUpdate(int flags)
        {
            Flags = flags;
        }

        private int Flags { get; }
        public bool NeedsSpawn => (Flags & NeedsSpawnFlag) != 0;
        public bool BoardChanged => (Flags & BoardChangedFlag) != 0;
        public bool HudChanged => (Flags & HudChangedFlag) != 0;
        public bool PieceLocked => (Flags & PieceLockedFlag) != 0;
        public bool Cleared => (Flags & ClearedFlag) != 0;
        public bool HighScoreChanged => (Flags & HighScoreChangedFlag) != 0;
    }

    public sealed class FlowSandMatchCoordinator
    {
        private const float SecondsPerSpeedLevel = 30f;
        private const float InitialDropInterval = 0.7f;
        private const float DropIntervalPerLevel = 0.045f;
        private const int MaximumSpeedLevel = 10;
        private const float SandStepInterval = 0.004f;
        private const int MaximumSandStepsPerFrame = 8;

        private readonly List<int> pendingClearIndices = new();
        private bool[] pendingClearMask = Array.Empty<bool>();

        private float pieceFallTimer;
        private float sandTimer;
        private float clearTimer;
        private bool waitingForSandToSettle;
        private bool bridgeCheckPending;

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
        public bool CanControlPiece => Phase == GamePhase.Playing && !HasPendingClear && !waitingForSandToSettle;
        public bool[] PendingClearMask => pendingClearMask;

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
            waitingForSandToSettle = false;
            bridgeCheckPending = true;
            pendingClearIndices.Clear();
            Array.Clear(pendingClearMask, 0, pendingClearMask.Length);
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

        public GameplayUpdate UpdateGameplay(FlowSandBoard board, System.Random random, float deltaTime, bool softDropHeld)
        {
            if (Phase != GamePhase.Playing)
            {
                return default;
            }

            GameplayEvent events = GameplayEvent.None;
            int previousSpeedLevel = GetSpeedLevel();
            ElapsedTime += deltaTime;
            if (GetSpeedLevel() != previousSpeedLevel)
            {
                events |= GameplayEvent.HudChanged;
            }

            UpdatePieceFall(board, deltaTime, softDropHeld, ref events);
            UpdateSand(board, random, deltaTime, ref events);
            UpdateClears(board, deltaTime, ref events);

            if (!board.HasActivePiece && !HasPendingClear && !waitingForSandToSettle)
            {
                events |= GameplayEvent.NeedsSpawn;
            }

            return new GameplayUpdate((int)events);
        }

        public GameplayUpdate HardDrop(FlowSandBoard board)
        {
            if (Phase != GamePhase.Playing || !board.HasActivePiece || HasPendingClear)
            {
                return default;
            }

            GameplayEvent events = GameplayEvent.None;
            while (board.TryStepDown())
            {
                events |= GameplayEvent.BoardChanged;
            }

            board.LockCurrentPiece();
            waitingForSandToSettle = true;
            bridgeCheckPending = false;
            Combo = 0;
            pieceFallTimer = 0f;
            events |= GameplayEvent.BoardChanged | GameplayEvent.PieceLocked;

            if (!board.HasActivePiece && !HasPendingClear && !waitingForSandToSettle)
            {
                events |= GameplayEvent.NeedsSpawn;
            }

            return new GameplayUpdate((int)events);
        }

        public float GetCurrentDropInterval()
        {
            float interval = InitialDropInterval - ((GetSpeedLevel() - 1) * DropIntervalPerLevel);
            return Mathf.Max(0.3f, interval); // 无论等级多高，正常重力下落的最快速度绝不会快于 0.3 秒/格
        }

        public int GetSpeedLevel()
        {
            return Mathf.Clamp(Mathf.FloorToInt(ElapsedTime / SecondsPerSpeedLevel) + 1, 1, MaximumSpeedLevel);
        }

        private void UpdatePieceFall(FlowSandBoard board, float deltaTime, bool softDropHeld, ref GameplayEvent events)
        {
            if (!board.HasActivePiece || HasPendingClear)
            {
                return;
            }

            pieceFallTimer += deltaTime;
            float stepInterval = GetCurrentDropInterval();
            if (softDropHeld)
            {
                stepInterval *= 0.167f;
            }

            while (pieceFallTimer >= stepInterval)
            {
                pieceFallTimer -= stepInterval;
                if (board.TryStepDown())
                {
                    events |= GameplayEvent.BoardChanged;
                    continue;
                }

                board.LockCurrentPiece();
                waitingForSandToSettle = true;
                bridgeCheckPending = false;
                Combo = 0;
                events |= GameplayEvent.BoardChanged | GameplayEvent.PieceLocked;
                break;
            }
        }

        private void UpdateSand(FlowSandBoard board, System.Random random, float deltaTime, ref GameplayEvent events)
        {
            if (!waitingForSandToSettle)
            {
                return;
            }

            sandTimer += deltaTime;
            int steps = 0;

            while (sandTimer >= SandStepInterval && steps < MaximumSandStepsPerFrame)
            {
                sandTimer -= SandStepInterval;
                steps += 1;
                bool moved = board.StepSand(random);
                if (moved)
                {
                    events |= GameplayEvent.BoardChanged;
                    continue;
                }

                waitingForSandToSettle = false;
                bridgeCheckPending = true;
                sandTimer = 0f;
                break;
            }

            if (steps == MaximumSandStepsPerFrame && sandTimer >= SandStepInterval)
            {
                sandTimer %= SandStepInterval;
            }
        }

        private void UpdateClears(FlowSandBoard board, float deltaTime, ref GameplayEvent events)
        {
            if (HasPendingClear)
            {
                clearTimer -= deltaTime;
                bool nextFlashVisible = Mathf.FloorToInt(clearTimer / 0.08f) % 2 == 0;
                if (nextFlashVisible != FlashVisible)
                {
                    FlashVisible = nextFlashVisible;
                    events |= GameplayEvent.BoardChanged;
                }

                if (clearTimer > 0f)
                {
                    return;
                }

                board.ClearCells(pendingClearIndices);
                waitingForSandToSettle = true;
                bridgeCheckPending = false;
                int cleared = pendingClearIndices.Count;
                ClearPendingMask();
                pendingClearIndices.Clear();
                Combo += 1;
                Score += cleared * 2 * Combo;
                events |= GameplayEvent.BoardChanged | GameplayEvent.HudChanged | GameplayEvent.Cleared;
                if (Score > HighScore)
                {
                    HighScore = Score;
                    events |= GameplayEvent.HighScoreChanged;
                }

                return;
            }

            // A bridge is only valid after the active block has landed, turned into
            // sand, and the resulting grains have reached a stable position.
            if (board.HasActivePiece || waitingForSandToSettle)
            {
                SetFlashVisible(true, ref events);
                return;
            }

            if (!bridgeCheckPending)
            {
                return;
            }

            bridgeCheckPending = false;
            IReadOnlyList<int> clearCells = board.FindBridgeClearCells();
            if (clearCells.Count == 0)
            {
                SetFlashVisible(true, ref events);
                return;
            }

            EnsurePendingMaskCapacity(board.CellCount);
            pendingClearIndices.Clear();
            for (int i = 0; i < clearCells.Count; i++)
            {
                int index = clearCells[i];
                pendingClearIndices.Add(index);
                pendingClearMask[index] = true;
            }

            clearTimer = 0.28f;
            SetFlashVisible(true, ref events);
            events |= GameplayEvent.BoardChanged;
        }

        private void EnsurePendingMaskCapacity(int cellCount)
        {
            if (pendingClearMask.Length != cellCount)
            {
                pendingClearMask = new bool[cellCount];
            }
        }

        private void ClearPendingMask()
        {
            for (int i = 0; i < pendingClearIndices.Count; i++)
            {
                pendingClearMask[pendingClearIndices[i]] = false;
            }
        }

        private void SetFlashVisible(bool visible, ref GameplayEvent events)
        {
            if (FlashVisible == visible)
            {
                return;
            }

            FlashVisible = visible;
            events |= GameplayEvent.BoardChanged;
        }

        public enum GamePhase
        {
            Title,
            Playing,
            Paused,
            GameOver,
        }

        [Flags]
        private enum GameplayEvent
        {
            None = 0,
            NeedsSpawn = 1 << 0,
            BoardChanged = 1 << 1,
            HudChanged = 1 << 2,
            PieceLocked = 1 << 3,
            Cleared = 1 << 4,
            HighScoreChanged = 1 << 5,
        }
    }
}

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
        private const int NextChangedFlag = 1 << 6;
        private const int ColorChallengeFlag = 1 << 7;

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
        public bool NextChanged => (Flags & NextChangedFlag) != 0;
        public bool ColorChallenge => (Flags & ColorChallengeFlag) != 0;
    }

    public sealed class FlowSandMatchCoordinator
    {
        private const float SecondsPerSpeedLevel = 30f;
        private const float InitialDropInterval = 0.7f;
        private const float DropIntervalPerLevel = 0.045f;
        private const int MaximumSpeedLevel = 15;
        private const float SandStepInterval = 0.004f;
        private const int MaximumSandStepsPerFrame = 8;
        private const int ScorePerCoarseCell = 1;
        private const int BombDefuseReward = 20;
        private const int ColorChallengeScoreInterval = 200;
        private const int MixedPieceScoreInterval = 30;

        private readonly List<int> pendingClearIndices = new();
        private bool[] pendingClearMask = Array.Empty<bool>();

        private float pieceFallTimer;
        private float sandTimer;
        private float clearTimer;
        private bool waitingForSandToSettle;
        private bool bridgeCheckPending;
        private bool currentPieceHadStructuralInput;
        private bool resolvingPieceRound;
        private bool resolvingPieceScored;
        private int consecutiveUnattendedScoringRounds;
        private int nextColorChallengeScore;
        private int nextMixedPieceScore;

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
            currentPieceHadStructuralInput = false;
            resolvingPieceRound = false;
            resolvingPieceScored = false;
            consecutiveUnattendedScoringRounds = 0;
            nextColorChallengeScore = ColorChallengeScoreInterval;
            nextMixedPieceScore = MixedPieceScoreInterval;
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
            UpdateClears(board, random, deltaTime, ref events);

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
            BeginPieceResolution();
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

        public void RegisterHorizontalOrRotationInput()
        {
            currentPieceHadStructuralInput = true;
        }

        public void RegisterPieceSpawned()
        {
            currentPieceHadStructuralInput = false;
        }

        // Tick bomb fuses once per newly spawned piece. If any bomb detonates, the
        // resulting crater leaves floating sand, so re-enter the settle loop and
        // re-scan for bridges afterwards. Call right after a successful spawn.
        public GameplayUpdate TickBombsOnSpawn(FlowSandBoard board)
        {
            if (Phase != GamePhase.Playing || !board.HasBombs)
            {
                return default;
            }

            GameplayEvent events = GameplayEvent.None;
            if (board.TickBombFuses())
            {
                waitingForSandToSettle = true;
                bridgeCheckPending = false;
                events |= GameplayEvent.BoardChanged;
            }

            return new GameplayUpdate((int)events);
        }

        public GameplayUpdate TriggerColorChallenge(FlowSandBoard board, System.Random random)
        {
            if (Phase != GamePhase.Playing)
            {
                return default;
            }

            board.QueueSuperMixedPiece(random);
            return new GameplayUpdate((int)(GameplayEvent.NextChanged | GameplayEvent.ColorChallenge));
        }

        public float GetCurrentDropInterval()
        {
            float interval = InitialDropInterval - ((GetSpeedLevel() - 1) * DropIntervalPerLevel);
            return Mathf.Max(0.2f, interval); // 无论等级多高，正常重力下落的最快速度绝不会快于 0.2 秒/格
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
                stepInterval *= 0.33f;
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
                BeginPieceResolution();
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

        private void UpdateClears(FlowSandBoard board, System.Random random, float deltaTime, ref GameplayEvent events)
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

                // Wear down obstacles touching the cleared region before the grains
                // are removed, so erosion is driven by the player's clears.
                board.ErodeObstaclesAround(pendingClearIndices);
                // Bombs adjacent to the clear are defused into a reward instead of
                // being left to detonate.
                int defusedBombs = board.DefuseBombsAround(pendingClearIndices);
                board.ClearCells(pendingClearIndices);
                waitingForSandToSettle = true;
                bridgeCheckPending = false;
                int cleared = pendingClearIndices.Count;
                ClearPendingMask();
                pendingClearIndices.Clear();
                Combo += 1;
                int grainsPerCoarseCell = board.GrainScale * board.GrainScale;
                int clearedCellEquivalents = Mathf.Max(1, cleared / grainsPerCoarseCell);
                Score += clearedCellEquivalents * ScorePerCoarseCell * Combo;
                if (defusedBombs > 0)
                {
                    Score += defusedBombs * BombDefuseReward * Combo;
                }

                resolvingPieceScored = true;
                events |= GameplayEvent.BoardChanged | GameplayEvent.HudChanged | GameplayEvent.Cleared;
                bool superMixedQueued = false;
                if (Score >= nextColorChallengeScore)
                {
                    board.QueueSuperMixedPiece(random);
                    nextColorChallengeScore = ((Score / ColorChallengeScoreInterval) + 1) * ColorChallengeScoreInterval;
                    events |= GameplayEvent.NextChanged | GameplayEvent.ColorChallenge;
                    superMixedQueued = true;
                }

                if (Score >= nextMixedPieceScore)
                {
                    if (!superMixedQueued)
                    {
                        board.QueueMixedPiece(random);
                        events |= GameplayEvent.NextChanged;
                    }
                    nextMixedPieceScore = ((Score / MixedPieceScoreInterval) + 1) * MixedPieceScoreInterval;
                }

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
                FinalizePieceResolution(board, random, ref events);
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

        private void BeginPieceResolution()
        {
            resolvingPieceRound = true;
            resolvingPieceScored = false;
        }

        private void FinalizePieceResolution(FlowSandBoard board, System.Random random, ref GameplayEvent events)
        {
            if (!resolvingPieceRound)
            {
                return;
            }

            if (resolvingPieceScored && !currentPieceHadStructuralInput)
            {
                consecutiveUnattendedScoringRounds += 1;
            }
            else
            {
                consecutiveUnattendedScoringRounds = 0;
            }

#if UNITY_EDITOR
            Debug.Log($"[FlowSand Debug] Round input={currentPieceHadStructuralInput}, scored={resolvingPieceScored}, unattended score streak={consecutiveUnattendedScoringRounds}");
#endif
            if (consecutiveUnattendedScoringRounds >= 2)
            {
                if (board.TryQueueMixedPiece(random))
                {
                    events |= GameplayEvent.NextChanged;
                }

                consecutiveUnattendedScoringRounds = 0;
            }

            resolvingPieceRound = false;
            resolvingPieceScored = false;
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
            NextChanged = 1 << 6,
            ColorChallenge = 1 << 7,
        }
    }
}

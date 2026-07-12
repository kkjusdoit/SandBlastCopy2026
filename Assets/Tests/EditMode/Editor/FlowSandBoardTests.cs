using System.Collections.Generic;
using FlowSand.Core;
using NUnit.Framework;
using UnityEngine;

public class FlowSandBoardTests
{
    [Test]
    public void ActivePieceLocksIntoSandGrid()
    {
        FlowSandBoard board = new(10, 20, 4);
        System.Random random = new(0);
        board.Reset(random);
        Assert.That(board.SpawnNextPiece(random), Is.True);
        int coarseCellCount = TetrominoLibrary.GetCells(board.CurrentPiece.Value.Kind, 0).Length;

        while (board.TryStepDown())
        {
        }

        board.LockCurrentPiece();

        int occupied = 0;
        for (int y = 0; y < board.SandRows; y++)
        {
            for (int x = 0; x < board.SandCols; x++)
            {
                if (board.GetSand(x, y) != CellColor.Empty)
                {
                    occupied += 1;
                }
            }
        }

        Assert.That(occupied, Is.EqualTo(coarseCellCount * 4 * 4));
    }

    [Test]
    public void SmallPieceDefinitionsSupportOneTwoAndThreeCellDrops()
    {
        Assert.That(TetrominoLibrary.GetCells(TetrominoKind.Mono, 0).Length, Is.EqualTo(1));
        Assert.That(TetrominoLibrary.GetCells(TetrominoKind.Domino, 0).Length, Is.EqualTo(2));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Domino, 0).Width, Is.EqualTo(2));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Domino, 1).Height, Is.EqualTo(2));
        Assert.That(TetrominoLibrary.GetCells(TetrominoKind.Triomino, 0).Length, Is.EqualTo(3));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Triomino, 0).Width, Is.EqualTo(3));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Triomino, 1).Height, Is.EqualTo(3));
    }

    [Test]
    public void TwelveColumnBoardCentersPiecesFromTheirBounds()
    {
        FlowSandBoard board = new(12, 20, 1);
        System.Random random = new(3);
        board.Reset(random);
        SetNextPiece(board, new ActivePiece { Kind = TetrominoKind.Mono, Color = CellColor.Coral });

        Assert.That(board.SpawnNextPiece(random), Is.True);
        Assert.That(board.SandCols, Is.EqualTo(12));
        Assert.That(board.CurrentPiece.Value.Col, Is.EqualTo(5));

        SetNextPiece(board, new ActivePiece { Kind = TetrominoKind.Domino, Color = CellColor.Coral });
        Assert.That(board.SpawnNextPiece(random), Is.True);
        Assert.That(board.CurrentPiece.Value.Col, Is.EqualTo(5));
    }

    [Test]
    public void SpawnOffsetBagUsesAllFiveOffsetsPerCycle()
    {
        FlowSandBoard board = new(12, 20, 1);
        System.Random random = new(9);
        board.Reset(random);
        HashSet<int> offsets = new();

        for (int i = 0; i < 5; i++)
        {
            SetNextPiece(board, new ActivePiece { Kind = TetrominoKind.Mono, Color = CellColor.Coral });
            Assert.That(board.SpawnNextPiece(random), Is.True);
            offsets.Add(board.LastSpawnOffset);
        }

        Assert.That(offsets, Is.EquivalentTo(new[] { -2, -1, 0, 1, 2 }));
    }

    [Test]
    public void MixedPieceLocksAsTwoCleanlySeparatedColors()
    {
        FlowSandBoard board = new(6, 8, 4);
        SetCurrentPiece(board, new ActivePiece
        {
            Kind = TetrominoKind.Domino,
            Color = CellColor.Coral,
            Col = 2,
            Row = 0,
            IsMixed = true,
            ColorSeed = 7,
        });

        board.LockCurrentPiece();

        int[] colors = new int[6];
        for (int y = 0; y < board.SandRows; y++)
        {
            for (int x = 0; x < board.SandCols; x++)
            {
                colors[(int)board.GetSand(x, y)] += 1;
            }
        }

        int usedColors = 0;
        for (int color = 1; color < colors.Length; color++)
        {
            if (colors[color] == 0)
            {
                continue;
            }

            usedColors += 1;
            Assert.That(colors[color], Is.EqualTo(16));
        }

        Assert.That(usedColors, Is.EqualTo(2));
    }

    [Test]
    public void MixedPieceColorPatternsAreOrderlyAndAlwaysUseTwoColors()
    {
        ActivePiece leftRight = new()
        {
            Kind = TetrominoKind.Triomino,
            IsMixed = true,
            MixedPattern = MixedColorPattern.LeftRight,
            ColorSeed = 0,
        };
        ActivePiece centered = new()
        {
            Kind = TetrominoKind.Triomino,
            IsMixed = true,
            MixedPattern = MixedColorPattern.CenterSymmetric,
            ColorSeed = 20,
        };

        Assert.That(TetrominoLibrary.GetPieceGrainColor(leftRight, 0, 0, 0, 4), Is.EqualTo(CellColor.Coral));
        Assert.That(TetrominoLibrary.GetPieceGrainColor(leftRight, 2, 3, 0, 4), Is.EqualTo(CellColor.Mint));
        Assert.That(TetrominoLibrary.GetPieceGrainColor(centered, 0, 0, 0, 4), Is.EqualTo(CellColor.Mint));
        Assert.That(TetrominoLibrary.GetPieceGrainColor(centered, 1, 1, 0, 4), Is.EqualTo(CellColor.Coral));
        Assert.That(TetrominoLibrary.GetPieceGrainColor(centered, 2, 3, 0, 4), Is.EqualTo(CellColor.Mint));
    }

    [Test]
    public void PerCellPatternKeepsAdjacentCellsDifferent()
    {
        ActivePiece piece = new()
        {
            Kind = TetrominoKind.T,
            IsMixed = true,
            MixedPattern = MixedColorPattern.PerCell,
            ColorSeed = 13,
        };
        Vector2Int[] cells = TetrominoLibrary.GetCells(piece.Kind, piece.Rotation);

        for (int i = 0; i < cells.Length; i++)
        {
            for (int j = i + 1; j < cells.Length; j++)
            {
                Vector2Int delta = cells[i] - cells[j];
                if (System.Math.Abs(delta.x) + System.Math.Abs(delta.y) != 1)
                {
                    continue;
                }

                Assert.That(
                    TetrominoLibrary.GetPieceGrainColor(piece, i, 0, 0, 4),
                    Is.Not.EqualTo(TetrominoLibrary.GetPieceGrainColor(piece, j, 0, 0, 4)));
            }
        }
    }

    [Test]
    public void MixedPieceSizesAreEquallyWeighted()
    {
        WeightedRandom random = new();
        int[] counts = new int[5];

        for (int roll = 0; roll < 3; roll++)
        {
            FlowSandBoard board = new(12, 20, 1);
            board.Reset(random);
            random.SizeRoll = roll;
            Assert.That(board.TryQueueMixedPiece(random), Is.True);
            counts[TetrominoLibrary.GetCells(board.NextPiece.Kind, 0).Length] += 1;
        }

        Assert.That(counts[2], Is.EqualTo(1));
        Assert.That(counts[3], Is.EqualTo(1));
        Assert.That(counts[4], Is.EqualTo(1));
    }

    [Test]
    public void MixedPiecesAreLimitedToThreeOfTheLastTenSpawns()
    {
        FlowSandBoard board = new(12, 20, 1);
        System.Random random = new(11);
        board.Reset(random);

        Assert.That(board.TryQueueMixedPiece(random), Is.True);
        board.SpawnNextPiece(random);
        Assert.That(board.TryQueueMixedPiece(random), Is.True);
        board.SpawnNextPiece(random);
        Assert.That(board.TryQueueMixedPiece(random), Is.True);
        board.SpawnNextPiece(random);
        Assert.That(board.TryQueueMixedPiece(random), Is.False);

        for (int i = 0; i < 7; i++)
        {
            board.SpawnNextPiece(random);
        }

        Assert.That(board.TryQueueMixedPiece(random), Is.False);
        board.SpawnNextPiece(random);
        Assert.That(board.TryQueueMixedPiece(random), Is.True);
    }

    [Test]
    public void EveryTwentyFourQueuedPiecesUseReducedMonominoMix()
    {
        FlowSandBoard board = new(10, 20, 1);
        System.Random random = new(17);
        board.Reset(random);
        int[] counts = new int[5];

        for (int i = 0; i < 24; i++)
        {
            int cellCount = TetrominoLibrary.GetCells(board.NextPiece.Kind, 0).Length;
            counts[cellCount] += 1;
            board.SpawnNextPiece(random);
        }

        Assert.That(counts[1], Is.EqualTo(3));
        Assert.That(counts[2], Is.EqualTo(7));
        Assert.That(counts[3], Is.EqualTo(7));
        Assert.That(counts[4], Is.EqualTo(7));
    }

    [Test]
    public void LeftToRightBridgeClearsOnlyConnectedColor()
    {
        FlowSandBoard board = new(3, 2, 1);
        List<int> bridge = new() { board.ToIndex(0, 0), board.ToIndex(1, 0), board.ToIndex(2, 0) };
        Set(board, 0, 0, CellColor.Coral);
        Set(board, 1, 0, CellColor.Coral);
        Set(board, 2, 0, CellColor.Coral);
        Set(board, 0, 1, CellColor.Sky);
        Set(board, 1, 1, CellColor.Sky);

        IReadOnlyList<int> clear = board.FindBridgeClearCells();

        Assert.That(clear, Is.EquivalentTo(bridge));
    }

    [Test]
    public void SandFallsDownWhenSpaceExists()
    {
        FlowSandBoard board = new(2, 3, 1);
        Set(board, 0, 2, CellColor.Gold);

        bool moved = board.StepSand(new System.Random(1));

        Assert.That(moved, Is.True);
        Assert.That(
            board.GetSand(0, 0) == CellColor.Gold || board.GetSand(0, 1) == CellColor.Gold,
            Is.True);
        Assert.That(board.GetSand(0, 2), Is.EqualTo(CellColor.Empty));
    }

    [Test]
    public void SandStepDoesNotShuffleEveryRow()
    {
        FlowSandBoard board = new(10, 20, 1);
        CountingRandom random = new();
        Set(board, 5, 19, CellColor.Gold);

        board.StepSand(random);

        Assert.That(random.NextCalls, Is.EqualTo(0));
    }

    [Test]
    public void FreeFallingRowMovesContinuouslyWithoutHorizontalGaps()
    {
        FlowSandBoard board = new(16, 12, 1);
        for (int x = 0; x < board.SandCols; x++)
        {
            Set(board, x, 10, CellColor.Sky);
        }

        board.StepSand(new System.Random(1));

        int waitingGrains = CountOccupiedInRow(board, 10);
        int fallenGrains = CountOccupiedInRow(board, 9);
        Assert.That(waitingGrains, Is.EqualTo(0));
        Assert.That(waitingGrains + fallenGrains, Is.EqualTo(board.SandCols));
    }

    [Test]
    public void LockSnapsPieceDownToFineSandContactBeforeSandifying()
    {
        FlowSandBoard board = new(4, 6, 4);
        Set(board, 0, 9, CellColor.Gold);
        SetCurrentPiece(board, new ActivePiece
        {
            Kind = TetrominoKind.I,
            Color = CellColor.Coral,
            Rotation = 0,
            Col = 0,
            Row = 2,
        });

        board.LockCurrentPiece();

        Assert.That(board.GetSand(0, 10), Is.EqualTo(CellColor.Coral));
        Assert.That(board.GetSand(0, 15), Is.EqualTo(CellColor.Empty));
    }

    [Test]
    public void SurfaceGrainSlidesOnlyIntoAnAdjacentDiagonalCell()
    {
        FlowSandBoard board = new(5, 3, 1);
        for (int x = 0; x < board.SandCols; x++)
        {
            Set(board, x, 0, CellColor.Gold);
        }

        Set(board, 2, 1, CellColor.Gold);
        Set(board, 2, 2, CellColor.Sky);

        bool moved = board.StepSand(new System.Random(1));

        Assert.That(moved, Is.True);
        Assert.That(board.GetSand(2, 2), Is.EqualTo(CellColor.Empty));
        Assert.That(
            board.GetSand(1, 1) == CellColor.Sky || board.GetSand(3, 1) == CellColor.Sky,
            Is.True);
    }

    [Test]
    public void PieceCanSpawnWhenOnlyTheRowBelowTheCeilingIsOccupied()
    {
        FlowSandBoard board = CreateBoardWithNextPiece(TetrominoKind.O);
        Set(board, 4, board.SandRows - 2, CellColor.Coral);

        Assert.That(board.SpawnNextPiece(new System.Random(1)), Is.True);
    }

    [Test]
    public void PieceCannotSpawnWhenTheCeilingEntryIsOccupied()
    {
        FlowSandBoard board = CreateBoardWithNextPiece(TetrominoKind.O);
        Set(board, 4, board.SandRows - 1, CellColor.Coral);

        Assert.That(board.SpawnNextPiece(new System.Random(1)), Is.False);
    }

    private static FlowSandBoard CreateBoardWithNextPiece(TetrominoKind kind)
    {
        FlowSandBoard board = new(10, 20, 1);
        board.Reset(new System.Random(0));
        var field = typeof(FlowSandBoard).GetField("<NextPiece>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(board, new ActivePiece { Kind = kind, Color = CellColor.Coral });
        return board;
    }

    private static void SetNextPiece(FlowSandBoard board, ActivePiece piece)
    {
        var field = typeof(FlowSandBoard).GetField("<NextPiece>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(board, piece);
    }

    private static void Set(FlowSandBoard board, int x, int y, CellColor color)
    {
        var field = typeof(FlowSandBoard).GetField("sandGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        CellColor[] grid = (CellColor[])field.GetValue(board);
        grid[board.ToIndex(x, y)] = color;
    }

    private static int CountOccupiedInRow(FlowSandBoard board, int y)
    {
        int occupied = 0;
        for (int x = 0; x < board.SandCols; x++)
        {
            if (board.GetSand(x, y) != CellColor.Empty)
            {
                occupied += 1;
            }
        }

        return occupied;
    }

    private static void SetCurrentPiece(FlowSandBoard board, ActivePiece piece)
    {
        var field = typeof(FlowSandBoard).GetField("<CurrentPiece>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(board, (ActivePiece?)piece);
    }

    private sealed class CountingRandom : System.Random
    {
        public int NextCalls { get; private set; }

        public override int Next(int maxValue)
        {
            NextCalls += 1;
            return base.Next(maxValue);
        }
    }

    private sealed class WeightedRandom : System.Random
    {
        public int SizeRoll { get; set; }

        public override int Next(int maxValue)
        {
            return maxValue == 3 ? SizeRoll : 0;
        }
    }
}

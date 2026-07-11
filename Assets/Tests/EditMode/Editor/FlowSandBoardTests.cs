using System.Collections.Generic;
using FlowSand.Core;
using NUnit.Framework;

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
    public void SmallPieceDefinitionsSupportOneAndTwoCellDrops()
    {
        Assert.That(TetrominoLibrary.GetCells(TetrominoKind.Mono, 0).Length, Is.EqualTo(1));
        Assert.That(TetrominoLibrary.GetCells(TetrominoKind.Domino, 0).Length, Is.EqualTo(2));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Domino, 0).Width, Is.EqualTo(2));
        Assert.That(TetrominoLibrary.GetBounds(TetrominoKind.Domino, 1).Height, Is.EqualTo(2));
    }

    [Test]
    public void EveryTenQueuedPiecesUseSixtyThirtyTenSizeMix()
    {
        FlowSandBoard board = new(10, 20, 1);
        System.Random random = new(17);
        board.Reset(random);
        int[] counts = new int[5];

        for (int i = 0; i < 10; i++)
        {
            int cellCount = TetrominoLibrary.GetCells(board.NextPiece.Kind, 0).Length;
            counts[cellCount] += 1;
            board.SpawnNextPiece(random);
        }

        Assert.That(counts[4], Is.EqualTo(6));
        Assert.That(counts[2], Is.EqualTo(3));
        Assert.That(counts[1], Is.EqualTo(1));
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
}

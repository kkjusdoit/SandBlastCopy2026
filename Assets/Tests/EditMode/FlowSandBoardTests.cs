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

        Assert.That(occupied, Is.EqualTo(4 * 4 * 4));
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
        Assert.That(board.GetSand(0, 1), Is.EqualTo(CellColor.Gold));
        Assert.That(board.GetSand(0, 2), Is.EqualTo(CellColor.Empty));
    }

    private static void Set(FlowSandBoard board, int x, int y, CellColor color)
    {
        var field = typeof(FlowSandBoard).GetField("sandGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        CellColor[] grid = (CellColor[])field.GetValue(board);
        grid[board.ToIndex(x, y)] = color;
    }
}

using System.Collections.Generic;
using System.Reflection;
using FlowSand.Core;
using FlowSand.Runtime;
using NUnit.Framework;

public class FlowSandMatchCoordinatorTests
{
    [Test]
    public void SpeedIncreasesOnceEveryThirtySecondsAndStopsAtLevelTen()
    {
        FlowSandMatchCoordinator match = new(0);
        FlowSandBoard board = new(10, 20, 1);
        System.Random random = new(0);
        board.Reset(random);
        match.StartMatch();

        Assert.That(match.GetSpeedLevel(), Is.EqualTo(1));
        Assert.That(match.GetCurrentDropInterval(), Is.EqualTo(0.7f).Within(0.001f));

        match.UpdateGameplay(board, random, 30f, false);
        Assert.That(match.GetSpeedLevel(), Is.EqualTo(2));
        Assert.That(match.GetCurrentDropInterval(), Is.EqualTo(0.655f).Within(0.001f));

        match.UpdateGameplay(board, random, 300f, false);
        Assert.That(match.GetSpeedLevel(), Is.EqualTo(10));
        Assert.That(match.GetCurrentDropInterval(), Is.EqualTo(0.295f).Within(0.001f));
    }

    [Test]
    public void BridgeIsNotDetectedWhileActivePieceIsStillFalling()
    {
        FlowSandMatchCoordinator match = new(0);
        FlowSandBoard board = new(4, 6, 1);
        System.Random random = new(0);
        board.Reset(random);
        SetNextPiece(board, TetrominoKind.O);
        SetBridge(board, 0, CellColor.Coral);
        Assert.That(board.SpawnNextPiece(random), Is.True);
        match.StartMatch();

        match.UpdateGameplay(board, random, 0.01f, false);

        Assert.That(board.HasActivePiece, Is.True);
        Assert.That(match.HasPendingClear, Is.False);
    }

    [Test]
    public void LockedBridgeWaitsForSandSimulationBeforeClearDetection()
    {
        FlowSandMatchCoordinator match = new(0);
        FlowSandBoard board = new(4, 5, 1);
        System.Random random = new(0);
        board.Reset(random);
        SetSand(board, 0, 0, CellColor.Sky);
        SetSand(board, 1, 0, CellColor.Sky);
        SetSand(board, 0, 1, CellColor.Sky);
        SetCurrentPiece(board, new ActivePiece
        {
            Kind = TetrominoKind.I,
            Color = CellColor.Coral,
            Rotation = 0,
            Col = 0,
            Row = 1,
        });
        match.StartMatch();
        SetPrivateField(match, "pieceFallTimer", 0.69f);

        GameplayUpdate updateBeforeSandStep = match.UpdateGameplay(board, random, 0.01f, false);

        Assert.That(board.HasActivePiece, Is.False);
        Assert.That(board.FindBridgeClearCells().Count, Is.EqualTo(4));
        Assert.That(match.HasPendingClear, Is.False);
        Assert.That(updateBeforeSandStep.NeedsSpawn, Is.False);
        Assert.That(updateBeforeSandStep.PieceLocked, Is.True);
        Assert.That(updateBeforeSandStep.BoardChanged, Is.True);
        Assert.That(match.CanControlPiece, Is.False);

        match.UpdateGameplay(board, random, 0.025f, false);

        Assert.That(match.HasPendingClear, Is.False);
    }

    [Test]
    public void StableBoardIsNotRescannedUntilSomethingChanges()
    {
        FlowSandMatchCoordinator match = new(0);
        FlowSandBoard board = new(4, 6, 1);
        System.Random random = new(0);
        board.Reset(random);
        match.StartMatch();

        GameplayUpdate first = match.UpdateGameplay(board, random, 0.01f, false);
        GameplayUpdate second = match.UpdateGameplay(board, random, 0.01f, false);

        Assert.That(first.NeedsSpawn, Is.True);
        Assert.That(second.NeedsSpawn, Is.True);
        Assert.That(board.BridgeScanCount, Is.EqualTo(1));
    }

    [Test]
    public void ComboIncrementsForChainedClearsAndResetsWhenAPieceLocks()
    {
        FlowSandMatchCoordinator match = new(0);
        FlowSandBoard board = new(4, 6, 1);
        System.Random random = new(0);
        board.Reset(random);
        match.StartMatch();

        QueueClear(match, board, 0);
        match.UpdateGameplay(board, random, 0.01f, false);
        Assert.That(match.Combo, Is.EqualTo(1));

        QueueClear(match, board, 1);
        match.UpdateGameplay(board, random, 0.01f, false);
        Assert.That(match.Combo, Is.EqualTo(2));

        SetCurrentPiece(board, new ActivePiece
        {
            Kind = TetrominoKind.O,
            Color = CellColor.Coral,
            Rotation = 0,
            Col = 0,
            Row = 0,
        });
        SetPrivateField(match, "waitingForSandToSettle", false);
        SetPrivateField(match, "pieceFallTimer", match.GetCurrentDropInterval());
        match.UpdateGameplay(board, random, 0f, false);

        Assert.That(match.Combo, Is.EqualTo(0));
    }

    private static void SetBridge(FlowSandBoard board, int y, CellColor color)
    {
        for (int x = 0; x < board.SandCols; x++)
        {
            SetSand(board, x, y, color);
        }
    }

    private static void SetSand(FlowSandBoard board, int x, int y, CellColor color)
    {
        FieldInfo field = typeof(FlowSandBoard).GetField("sandGrid", BindingFlags.NonPublic | BindingFlags.Instance);
        CellColor[] grid = (CellColor[])field.GetValue(board);
        grid[board.ToIndex(x, y)] = color;
    }

    private static void QueueClear(FlowSandMatchCoordinator match, FlowSandBoard board, int x)
    {
        SetSand(board, x, 0, CellColor.Coral);
        List<int> pending = (List<int>)GetPrivateField(match, "pendingClearIndices");
        pending.Add(board.ToIndex(x, 0));
        SetPrivateField(match, "pendingClearMask", new bool[board.CellCount]);
        SetPrivateField(match, "clearTimer", 0f);
    }

    private static void SetNextPiece(FlowSandBoard board, TetrominoKind kind)
    {
        FieldInfo field = typeof(FlowSandBoard).GetField("<NextPiece>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(board, new ActivePiece { Kind = kind, Color = CellColor.Coral });
    }

    private static void SetCurrentPiece(FlowSandBoard board, ActivePiece piece)
    {
        SetPrivateField(board, "<CurrentPiece>k__BackingField", (ActivePiece?)piece);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        return field.GetValue(target);
    }
}

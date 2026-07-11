using FlowSand.Online;
using FlowSand.Runtime;
using NUnit.Framework;

public class FlowSandLeaderboardFormattingTests
{
    [Test]
    public void FormatsOnlyTheRequestedNumberOfRows()
    {
        OnlinePlayer[] players =
        {
            new() { rank = 1, displayName = "玩家A001", bestScore = 300 },
            new() { rank = 2, displayName = "玩家B002", bestScore = 200 },
            new() { rank = 3, displayName = "玩家C003", bestScore = 100 },
        };

        string result = FlowSandRuntimeView.FormatLeaderboardRows(players, 2);

        Assert.That(result, Is.EqualTo("1.  玩家A001    300\n2.  玩家B002    200"));
    }

    [Test]
    public void EmptyLeaderboardProducesNoRows()
    {
        Assert.That(FlowSandRuntimeView.FormatLeaderboardRows(null, 10), Is.Empty);
    }
}

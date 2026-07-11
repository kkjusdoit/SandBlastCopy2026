using System;

namespace FlowSand.Online
{
    [Serializable]
    public sealed class AuthRequest
    {
        public string playerId;
        public string code;
    }

    [Serializable]
    public sealed class AuthResponse
    {
        public string token;
        public OnlinePlayer player;
    }

    [Serializable]
    public sealed class OnlinePlayer
    {
        public string id;
        public string displayName;
        public int bestScore;
        public int rank;
    }

    [Serializable]
    public sealed class ScoreSubmission
    {
        public string runId;
        public int score;
        public int durationSeconds;
        public int clears;
        public int maxCombo;
    }

    [Serializable]
    public sealed class ScoreResponse
    {
        public bool accepted;
        public bool duplicate;
        public OnlinePlayer player;
    }

    [Serializable]
    public sealed class LeaderboardResponse
    {
        public OnlinePlayer[] players;
        public OnlinePlayer me;
    }

    [Serializable]
    internal sealed class PendingSubmissionList
    {
        public ScoreSubmission[] items = Array.Empty<ScoreSubmission>();
    }
}

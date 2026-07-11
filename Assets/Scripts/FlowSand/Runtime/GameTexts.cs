namespace FlowSand.Runtime
{
    internal static class GameTexts
    {
        public const string GameName = "七彩流沙方块";

        public const string Score = "分数";
        public const string Next = "下一个";
        public const string Speed = "速度";
        public const string Best = "最高";
        public const string Pause = "暂停";
        public const string Drop = "加速";
        public const string Leaderboard = "排行";
        public const string LeaderboardTitle = "全服排行";
        public const string LeaderboardLoading = "正在加载排行...";
        public const string LeaderboardUnavailable = "排行榜暂时无法连接\n请稍后重试";
        public const string LeaderboardEmpty = "还没有玩家上榜";
        public const string Close = "关闭";

        public const string StartSubtitle = "同色流沙连接左右两侧即可消除\n一个方块1分，连消再×2、×3";
        public const string StartInstructions = "在棋盘上滑动虚拟摇杆\n左右移动，上滑旋转，下滑加速";
        public const string Start = "开始游戏";
        public const string VirtualJoystickHint = "在棋盘上滑动虚拟摇杆\n左右移动，上滑旋转，下滑加速";

        public const string PausedTitle = "游戏暂停";
        public const string PausedSubtitle = "当前状态已保持";
        public const string PausedInstructions = "点击继续游戏";
        public const string Resume = "继续游戏";
        public const string RestartNow = "重新开始";

        public const string GameOverTitle = "本局结束";
        public const string GameOverSubtitle = "流沙堆积已挡住出生区域\n点击重新开始";
        public const string Restart = "再来一局";

        public static string FinalScore(int score, int best) => $"最终分数：{score}\n最高分：{best}";

        public static string LeaderboardEntry(int rank, string name, int score) => $"{rank}.  {name}    {score}";

        public static string MyRank(int rank, int score) => rank > 0
            ? $"我的排名：{rank}    最高分：{score}"
            : $"尚未上榜    最高分：{score}";
    }
}

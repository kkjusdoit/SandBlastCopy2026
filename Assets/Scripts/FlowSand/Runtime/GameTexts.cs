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

        public const string StartSubtitle = "放置方块，让它们散落成流沙。\n连接同色流沙的左右两侧即可消除。";
        public const string StartInstructions = "在棋盘上滑动虚拟摇杆：\n左右移动，上滑旋转，下滑加速";
        public const string Start = "开始游戏";

        public const string PausedTitle = "游戏暂停";
        public const string PausedSubtitle = "当前状态已保持";
        public const string PausedInstructions = "按 P 键或点击继续游戏";
        public const string Resume = "继续游戏";

        public const string GameOverTitle = "本局结束";
        public const string GameOverSubtitle = "流沙堆积已挡住出生区域\n点击重新开始";
        public const string Restart = "再来一局";

        public static string FinalScore(int score, int best) => $"最终分数  {score}     最高  {best}";
    }
}

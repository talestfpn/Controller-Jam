/// <summary>
/// Snapshot imutável usado pela tela de resultado depois que uma batalha termina.
/// </summary>
public sealed class BattleResultData
{
    public BattleResultData(
        string enemyName,
        int perfectCount,
        int goodCount,
        int okCount,
        int missCount,
        int highestCombo,
        int successfulExecutions,
        BattleRewardBreakdown reward)
    {
        EnemyName = string.IsNullOrWhiteSpace(enemyName) ? "ENEMY" : enemyName;
        PerfectCount = perfectCount;
        GoodCount = goodCount;
        OkCount = okCount;
        MissCount = missCount;
        HighestCombo = highestCombo;
        SuccessfulExecutions = successfulExecutions;
        Reward = reward;
    }

    public string EnemyName { get; }
    public int PerfectCount { get; }
    public int GoodCount { get; }
    public int OkCount { get; }
    public int MissCount { get; }
    public int HighestCombo { get; }
    public int SuccessfulExecutions { get; }
    public BattleRewardBreakdown Reward { get; }
}

using System;

/// <summary>
/// Estatísticas da batalha atual. Não conhece UI, cenas ou regras de dano.
/// </summary>
public sealed class BattlePerformance
{
    public int PerfectCount { get; private set; }
    public int GoodCount { get; private set; }
    public int OkCount { get; private set; }
    public int MissCount { get; private set; }
    public int HighestCombo { get; private set; }
    public int SuccessfulExecutions { get; private set; }

    public void Reset()
    {
        PerfectCount = 0;
        GoodCount = 0;
        OkCount = 0;
        MissCount = 0;
        HighestCombo = 0;
        SuccessfulExecutions = 0;
    }

    public void RegisterTimingResult(
        TimingResult result,
        int currentCombo,
        int highestCombo)
    {
        switch (result)
        {
            case TimingResult.Perfect:
                PerfectCount++;
                break;
            case TimingResult.Good:
                GoodCount++;
                break;
            case TimingResult.Ok:
                OkCount++;
                break;
            default:
                MissCount++;
                break;
        }

        HighestCombo = Math.Max(
            HighestCombo,
            Math.Max(0, Math.Max(currentCombo, highestCombo)));
    }

    public void RegisterSuccessfulExecution()
    {
        SuccessfulExecutions++;
    }
}

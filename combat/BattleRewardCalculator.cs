using System;

/// <summary>
/// Resultado detalhado do cálculo de Essence de uma batalha.
/// </summary>
public sealed class BattleRewardBreakdown
{
    public BattleRewardBreakdown(
        int baseReward,
        int perfectBonus,
        int executionBonus,
        int subtotal,
        float comboMultiplier,
        int totalReward)
    {
        BaseReward = baseReward;
        PerfectBonus = perfectBonus;
        ExecutionBonus = executionBonus;
        Subtotal = subtotal;
        ComboMultiplier = comboMultiplier;
        TotalReward = totalReward;
    }

    public int BaseReward { get; }
    public int PerfectBonus { get; }
    public int ExecutionBonus { get; }
    public int Subtotal { get; }
    public float ComboMultiplier { get; }
    public int TotalReward { get; }
}

/// <summary>
/// Fórmula pura de recompensa. Todas as regras numéricas ficam fora da UI.
/// </summary>
public static class BattleRewardCalculator
{
    public static BattleRewardBreakdown Calculate(
        int baseReward,
        BattlePerformance performance,
        BattleRewardConfig config,
        float perfectBonusMultiplier = 1.0f,
        float baseRewardMultiplier = 1.0f)
    {
        if (performance == null)
        {
            throw new ArgumentNullException(nameof(performance));
        }

        config ??= new BattleRewardConfig();

        var safeBaseReward = Math.Max(0, baseReward);
        var safeBaseMultiplier = Math.Max(0.0f, baseRewardMultiplier);
        var safePerfectMultiplier = Math.Max(0.0f, perfectBonusMultiplier);
        var adjustedBaseReward = (int)Math.Round(
            safeBaseReward * safeBaseMultiplier,
            MidpointRounding.AwayFromZero);
        var perfectBonus = (int)Math.Round(
            performance.PerfectCount * Math.Max(
            0,
            config.PerfectBonusPerCount) * safePerfectMultiplier,
            MidpointRounding.AwayFromZero);
        var executionBonus = performance.SuccessfulExecutions * Math.Max(
            0,
            config.ExecutionPerfectBonus);
        var subtotal = adjustedBaseReward + perfectBonus + executionBonus;
        var comboMultiplier = Math.Max(
            0.0f,
            config.GetComboRewardMultiplier(performance.HighestCombo));
        var totalReward = (int)Math.Round(
            subtotal * comboMultiplier,
            MidpointRounding.AwayFromZero);

        return new BattleRewardBreakdown(
            adjustedBaseReward,
            perfectBonus,
            executionBonus,
            subtotal,
            comboMultiplier,
            Math.Max(0, totalReward));
    }
}

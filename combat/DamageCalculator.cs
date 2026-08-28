using System;

/// <summary>
/// Cálculo puro de dano direto, dano recebido, postura e execução. Não conhece
/// cenas, nós ou interface.
/// </summary>
public static class DamageCalculator
{
    public static float CalculatePlayerAttackDamage(
        float baseDamage,
        TimingResult timingResult,
        TimingConfig? timingConfig,
        float comboMultiplier = 1.0f)
    {
        if (timingResult != TimingResult.Perfect)
        {
            return 0.0f;
        }

        var safeBaseDamage = Math.Max(0.0f, baseDamage);
        var safeComboMultiplier = Math.Max(0.0f, comboMultiplier);
        var safeConfig = timingConfig ?? new TimingConfig();
        var multiplier = TimingJudge.GetDamageMultiplier(timingResult, safeConfig);
        return safeBaseDamage * (float)multiplier * safeComboMultiplier;
    }

    public static float CalculateDamage(
        float baseDamage,
        TimingResult timingResult,
        TimingConfig? timingConfig,
        float comboMultiplier = 1.0f)
    {
        return CalculatePlayerAttackDamage(
            baseDamage,
            timingResult,
            timingConfig,
            comboMultiplier);
    }

    public static float CalculateIncomingDamage(
        float baseDamage,
        TimingResult timingResult,
        TimingConfig? timingConfig)
    {
        var safeBaseDamage = Math.Max(0.0f, baseDamage);
        var safeConfig = timingConfig ?? new TimingConfig();
        var multiplier = timingResult switch
        {
            TimingResult.Perfect => safeConfig.PerfectIncomingDamageMultiplier,
            TimingResult.Good => safeConfig.GoodIncomingDamageMultiplier,
            TimingResult.Ok => safeConfig.OkIncomingDamageMultiplier,
            _ => safeConfig.MissIncomingDamageMultiplier,
        };

        return safeBaseDamage * multiplier;
    }

    public static float CalculatePostureDamage(
        RhythmPromptType promptType,
        TimingResult timingResult,
        TimingConfig? timingConfig)
    {
        var safeConfig = timingConfig ?? new TimingConfig();

        if (promptType == RhythmPromptType.PlayerAttack)
        {
            return timingResult switch
            {
                TimingResult.Perfect => safeConfig.PlayerPerfectPostureDamage,
                TimingResult.Good => safeConfig.PlayerGoodPostureDamage,
                TimingResult.Ok => safeConfig.PlayerOkPostureDamage,
                _ => safeConfig.PlayerMissPostureDamage,
            };
        }

        if (promptType == RhythmPromptType.EnemyAttack)
        {
            return timingResult switch
            {
                TimingResult.Perfect => safeConfig.ParryPostureDamage,
                TimingResult.Good => safeConfig.BlockPostureDamage,
                TimingResult.Ok => safeConfig.PartialBlockPostureDamage,
                _ => safeConfig.HitPostureDamage,
            };
        }

        return 0.0f;
    }

    public static float CalculateExecutionDamage(
        TimingResult timingResult,
        TimingConfig? timingConfig)
    {
        if (timingResult != TimingResult.Perfect)
        {
            return 0.0f;
        }

        var safeConfig = timingConfig ?? new TimingConfig();
        return Math.Max(0.0f, safeConfig.PerfectExecutionDamage);
    }
}

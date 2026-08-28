using Godot;

/// <summary>
/// Estado independente de Combo durante um combate. Não conhece a UI nem
/// aplica dano; comunica alterações por sinais para o CombatController.
/// </summary>
public partial class ComboManager : Node
{
    [Signal]
    public delegate void ComboChangedEventHandler(int currentCombo, int highestCombo);

    [Signal]
    public delegate void PerfectStreakChangedEventHandler(int perfectStreak);

    [Signal]
    public delegate void DamageMultiplierChangedEventHandler(
        float damageMultiplier,
        int comboTier);

    [Signal]
    public delegate void ComboMilestoneReachedEventHandler(
        int combo,
        string milestoneText);

    [Signal]
    public delegate void ComboBrokenEventHandler(int previousCombo);

    [Signal]
    public delegate void TimingResultRegisteredEventHandler(
        int result,
        int currentCombo,
        int perfectStreak);

    [Export]
    public ComboConfig Config { get; set; } = null!;

    public int CurrentCombo { get; private set; }
    public int HighestCombo { get; private set; }
    public int PerfectStreak { get; private set; }
    public float CurrentDamageMultiplier { get; private set; } = 1.0f;
    public int CurrentComboTier { get; private set; }
    public TimingResult? LastComboResult { get; private set; }

    public override void _Ready()
    {
        ResetForCombat();
    }

    public void RegisterTimingResult(TimingResult result)
    {
        var previousCombo = CurrentCombo;
        var previousHighestCombo = HighestCombo;
        var previousPerfectStreak = PerfectStreak;
        var previousTier = CurrentComboTier;
        var previousMultiplier = CurrentDamageMultiplier;

        switch (result)
        {
            case TimingResult.Perfect:
                CurrentCombo += 1;
                PerfectStreak += 1;
                break;
            case TimingResult.Good:
                PerfectStreak = 0;
                break;
            case TimingResult.Ok:
                CurrentCombo /= 2;
                PerfectStreak = 0;
                break;
            default:
                CurrentCombo = 0;
                PerfectStreak = 0;
                break;
        }

        if (CurrentCombo > HighestCombo)
        {
            HighestCombo = CurrentCombo;
        }

        LastComboResult = result;
        RecalculateTier();

        if (CurrentCombo != previousCombo || HighestCombo != previousHighestCombo)
        {
            EmitSignal(SignalName.ComboChanged, CurrentCombo, HighestCombo);
        }

        if (PerfectStreak != previousPerfectStreak)
        {
            EmitSignal(SignalName.PerfectStreakChanged, PerfectStreak);
        }

        if (CurrentComboTier != previousTier ||
            !Mathf.IsEqualApprox(CurrentDamageMultiplier, previousMultiplier))
        {
            EmitSignal(
                SignalName.DamageMultiplierChanged,
                CurrentDamageMultiplier,
                CurrentComboTier);
        }

        if (result == TimingResult.Miss && previousCombo > 0)
        {
            EmitSignal(SignalName.ComboBroken, previousCombo);
        }

        if (result == TimingResult.Perfect)
        {
            EmitMilestones(previousCombo);
        }

        EmitSignal(
            SignalName.TimingResultRegistered,
            (int)result,
            CurrentCombo,
            PerfectStreak);
    }

    public void ResetCombo()
    {
        ResetForCombat();
    }

    public void ResetForCombat()
    {
        CurrentCombo = 0;
        HighestCombo = 0;
        PerfectStreak = 0;
        CurrentDamageMultiplier = 1.0f;
        CurrentComboTier = 0;
        LastComboResult = null;
        RecalculateTier();

        EmitSignal(SignalName.ComboChanged, CurrentCombo, HighestCombo);
        EmitSignal(SignalName.PerfectStreakChanged, PerfectStreak);
        EmitSignal(
            SignalName.DamageMultiplierChanged,
            CurrentDamageMultiplier,
            CurrentComboTier);
    }

    public float GetDamageMultiplier()
    {
        return CurrentDamageMultiplier;
    }

    public int GetCurrentTier()
    {
        return CurrentComboTier;
    }

    private void RecalculateTier()
    {
        if (Config == null)
        {
            CurrentComboTier = 0;
            CurrentDamageMultiplier = 1.0f;
            return;
        }

        CurrentComboTier = Config.GetTierIndex(CurrentCombo);
        CurrentDamageMultiplier = Config.GetDamageMultiplier(CurrentCombo);
    }

    private void EmitMilestones(int previousCombo)
    {
        if (Config == null)
        {
            return;
        }

        foreach (var tier in Config.GetOrderedTiers())
        {
            if (tier.RequiredCombo <= 0 ||
                tier.RequiredCombo <= previousCombo ||
                tier.RequiredCombo > CurrentCombo)
            {
                continue;
            }

            var milestoneText = string.IsNullOrWhiteSpace(tier.MilestoneText)
                ? "FLOW UP!"
                : tier.MilestoneText;
            EmitSignal(
                SignalName.ComboMilestoneReached,
                tier.RequiredCombo,
                milestoneText);
        }
    }
}

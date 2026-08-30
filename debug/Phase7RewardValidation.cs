using Godot;

/// <summary>
/// Validação headless da fórmula da Fase 7. É uma cena de debug isolada e não
/// participa do fluxo normal do jogador.
/// </summary>
public partial class Phase7RewardValidation : Node
{
    [Export]
    public BattleRewardConfig Config { get; set; } = null!;

    public override void _Ready()
    {
        var errors = 0;
        errors += ValidateReward("A", 55, CreatePerformance(0), 55);
        errors += ValidateReward("B", 55, CreatePerformance(10), 94);
        errors += ValidateReward("C", 55, CreatePerformance(20), 143);

        var executionPerformance = CreatePerformance(4);
        executionPerformance.RegisterSuccessfulExecution();
        errors += ValidateReward("D", 55, executionPerformance, 73);

        var runManager = GetNode<RunManager>("/root/RunManager");
        runManager.BeginRun(100.0f);
        runManager.AddEssence(55);
        if (!runManager.CanAfford(55) || !runManager.SpendEssence(20) ||
            runManager.CurrentEssence != 35)
        {
            GD.PrintErr("PHASE7_RUN_MANAGER_VALIDATION failed");
            errors++;
        }

        GD.Print($"PHASE7_REWARD_VALIDATION_RESULT errors={errors}");
        GetTree().Quit(errors == 0 ? 0 : 1);
    }

    private int ValidateReward(
        string caseName,
        int baseReward,
        BattlePerformance performance,
        int expectedTotal)
    {
        var reward = BattleRewardCalculator.Calculate(baseReward, performance, Config);
        if (reward.TotalReward == expectedTotal)
        {
            return 0;
        }

        GD.PrintErr(
            $"PHASE7_REWARD_CASE_{caseName} failed " +
            $"expected={expectedTotal} actual={reward.TotalReward}");
        return 1;
    }

    private static BattlePerformance CreatePerformance(int perfectCount)
    {
        var performance = new BattlePerformance();
        for (var index = 0; index < perfectCount; index++)
        {
            var combo = index + 1;
            performance.RegisterTimingResult(
                TimingResult.Perfect,
                combo,
                combo);
        }

        return performance;
    }
}

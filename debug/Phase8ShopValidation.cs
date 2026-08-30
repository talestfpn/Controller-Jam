using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Smoke test headless da Fase 8. Não participa da execução normal do jogo.
/// </summary>
public partial class Phase8ShopValidation : Node
{
    [Export]
    public Godot.Collections.Array<UpgradeDataResource> TestPool { get; set; } = new();

    [Export]
    public UpgradeDataResource ArcaneEdge { get; set; } = null!;

    [Export]
    public UpgradeDataResource Vitality { get; set; } = null!;

    [Export]
    public UpgradeDataResource GlassArcana { get; set; } = null!;

    [Export]
    public UpgradeDataResource ArcaneRebirth { get; set; } = null!;

    public override void _Ready()
    {
        var errors = new List<string>();
        var runManager = GetNode<RunManager>("/root/RunManager");
        runManager.BeginRun(100.0f);

        if (TestPool.Count < 3)
        {
            errors.Add("pool de teste menor que três cartas");
        }

        var candidates = ShopCandidateGenerator.Generate(
            TestPool,
            runManager.Build,
            12345,
            60.0f,
            30.0f,
            10.0f);
        var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!candidateIds.Add(candidate.Id))
            {
                errors.Add("candidatos duplicados");
            }
        }

        if (candidates.Count != Math.Min(3, TestPool.Count))
        {
            errors.Add($"quantidade de candidatos inesperada: {candidates.Count}");
        }

        runManager.AddEssence(1000);
        runManager.StartShop(12345);
        if (!runManager.TryPurchaseUpgrade(ArcaneEdge))
        {
            errors.Add("compra válida foi recusada");
        }

        if (runManager.TryPurchaseUpgrade(Vitality))
        {
            errors.Add("segunda compra na mesma Shop foi aceita");
        }

        if (!runManager.ShopPurchaseMade ||
            runManager.GetUpgradeStacks(UpgradeIds.ArcaneEdge) != 1)
        {
            errors.Add("estado da compra/build não foi persistido");
        }

        if (!runManager.TryUseShopReroll(0) ||
            runManager.TryUseShopReroll(0) ||
            !runManager.TryUseShopReroll(1) ||
            runManager.TryUseShopReroll(1) ||
            runManager.ShopRerollsRemaining != 1)
        {
            errors.Add("reroll por slot não respeitou uso único");
        }

        if (!runManager.AdvanceToNextEncounter(7) ||
            runManager.CurrentEncounterIndex != 1)
        {
            errors.Add("avanço para o próximo encontro falhou");
        }

        runManager.StartShop(12345);
        if (runManager.ShopRerollsRemaining != 3)
        {
            errors.Add("rerolls por slot não resetaram na nova Shop");
        }

        var build = new RunBuild();
        for (var index = 0; index < ArcaneEdge.MaxStacks; index++)
        {
            build.AddUpgrade(ArcaneEdge);
        }

        if (build.CanAcquire(ArcaneEdge))
        {
            errors.Add("MaxStacks não bloqueou o upgrade");
        }

        build.AddUpgrade(Vitality);
        build.AddUpgrade(GlassArcana);
        if (build.GetEffectiveMaxHealth(100.0f) <= 1.0f)
        {
            errors.Add("cálculo de Max HP inválido");
        }

        runManager.BeginRun(100.0f);
        runManager.AddEssence(500);
        runManager.SetPlayerCurrentHP(20.0f);
        runManager.StartShop(54321);
        if (!runManager.TryPurchaseUpgrade(ArcaneRebirth) ||
            Mathf.Abs(runManager.PlayerCurrentHP - 70.0f) > 0.01f)
        {
            errors.Add("carta de cura não aplicou 50% do HP máximo");
        }

        if (runManager.Build.HasUpgrade(UpgradeIds.ArcaneRebirth))
        {
            errors.Add("carta consumível entrou na RunBuild");
        }

        runManager.SetCurrentEncounter(5);
        runManager.SetPlayerCurrentHP(1.0f);
        runManager.BeginRun(100.0f);
        if (runManager.CurrentEncounterIndex != 0 ||
            runManager.CurrentEssence != 0 ||
            runManager.Build.UniqueUpgradeCount != 0 ||
            Mathf.Abs(runManager.PlayerCurrentHP - 100.0f) > 0.01f)
        {
            errors.Add("reinício da run não voltou ao primeiro encontro");
        }

        if (errors.Count == 0)
        {
            GD.Print("PHASE8_SHOP_VALIDATION_RESULT errors=0");
        }
        else
        {
            GD.PrintErr(
                $"PHASE8_SHOP_VALIDATION_RESULT errors={errors.Count} " +
                string.Join(" | ", errors));
        }

        GetTree().Quit(errors.Count == 0 ? 0 : 1);
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Valida os Resources e as regras de fairness da Fase 5.5 sem iniciar uma
/// batalha longa. Os padrões são gerados novamente a partir do chart-base e o
/// chart original é comparado por assinatura antes/depois.
/// </summary>
public partial class Phase55PatternValidation : Node
{
    [Export]
    public RhythmPatternResource BasePattern { get; set; } = null!;

    [Export]
    public RhythmFairnessConfigResource FairnessConfig { get; set; } = null!;

    [Export]
    public PackedScene PromptScene { get; set; } = null!;

    [Export]
    public EnemyDataResource FoolData { get; set; } = null!;

    [Export]
    public EnemyDataResource TowerData { get; set; } = null!;

    [Export]
    public EnemyDataResource JusticeData { get; set; } = null!;

    [Export]
    public EnemyDataResource KnightOfSwordsData { get; set; } = null!;

    [Export]
    public EnemyDataResource MagicianData { get; set; } = null!;

    [Export]
    public EnemyDataResource KnightOfPentaclesData { get; set; } = null!;

    [Export]
    public EnemyDataResource KingOfWandsData { get; set; } = null!;

    [Export]
    public int Seed { get; set; } = 12345;

    private int _errors;
    private int _finishFramesRemaining = -1;

    public override void _Ready()
    {
        if (BasePattern == null || FairnessConfig == null)
        {
            Require(false, "BasePattern e FairnessConfig são obrigatórios.");
            Finish();
            return;
        }

        var baseSignature = GetSignature(BasePattern);
        var fool = ValidateData("THE FOOL", FoolData);
        var tower = ValidateData("THE TOWER", TowerData);
        var justice = ValidateData("JUSTICE", JusticeData);
        var knightOfSwords = ValidateData("KNIGHT OF SWORDS", KnightOfSwordsData);
        var magician = ValidateData("THE MAGICIAN", MagicianData);
        var knightOfPentacles = ValidateData("KNIGHT OF PENTACLES", KnightOfPentaclesData);
        var kingOfWandsPhase1 = ValidatePhase("KING OF WANDS_P1", KingOfWandsData, 0);
        var kingOfWandsPhase2 = ValidatePhase("KING OF WANDS_P2", KingOfWandsData, 1);
        var kingOfWandsPhase3 = ValidatePhase("KING OF WANDS_P3", KingOfWandsData, 2);

        Require(justice.EventCount > fool.EventCount,
            "Justice deve ser mais densa que The Fool.");
        Require(knightOfSwords.EventCount > justice.EventCount,
            "Knight of Swords deve ser mais denso que Justice.");
        Require(knightOfSwords.EnemyAttackCount > justice.EnemyAttackCount,
            "Knight of Swords deve possuir mais pressão de EnemyAttack que Justice.");
        Require(magician.FeintCount > 0 && magician.FadeCount > 0 && magician.DecoyCount > 0,
            "The Magician precisa gerar Feint, Fade e Decoy.");
        Require(knightOfPentacles.DecoyCount == 0 && knightOfPentacles.FeintCount == 0 && knightOfPentacles.FadeCount == 0,
            "Knight of Pentacles não deve possuir modifiers visuais.");
        Require(kingOfWandsPhase3.EventCount > kingOfWandsPhase1.EventCount,
            "King of Wands Phase 3 deve ser mais densa que Phase 1.");
        Require(kingOfWandsPhase3.EnemyAttackCount > kingOfWandsPhase1.EnemyAttackCount,
            "King of Wands Phase 3 deve possuir mais EnemyAttack que Phase 1.");
        Require(kingOfWandsPhase3.BurstCount >= kingOfWandsPhase1.BurstCount,
            "King of Wands Phase 3 não deve perder identidade de burst.");

        ValidatePromptBehaviors();
        ValidateEnemyData(FoolData);
        ValidateEnemyData(TowerData);
        ValidateEnemyData(JusticeData);
        ValidateEnemyData(KnightOfSwordsData);
        ValidateEnemyData(MagicianData);
        ValidateEnemyData(KnightOfPentaclesData);
        ValidateEnemyData(KingOfWandsData);
        ValidateBerserkerPhases();

        Require(KnightOfPentaclesData.Armored, "Knight of Pentacles precisa estar marcado como Armored.");
        Require(Math.Abs(KnightOfPentaclesData.HealthDamageMultiplierWhilePostureActive - 0.25f) < 0.001f,
            "Knight of Pentacles precisa usar Armor multiplier 0.25.");
        var armoredDamage = DamageCalculator.ApplyArmorToHealthDamage(15.0f, true, 0.25f);
        Require(Math.Abs(armoredDamage - 3.75f) < 0.001f,
            "Armor não reduziu o dano de HP conforme configurado.");
        var executionDamage = DamageCalculator.CalculateExecutionDamage(
            TimingResult.Perfect,
            new TimingConfig());
        Require(Math.Abs(executionDamage - 50.0f) < 0.001f,
            "Dano base de Execution foi alterado.");

        Require(baseSignature == GetSignature(BasePattern),
            "jogo_generated_pattern.tres foi mutado durante a validação.");
        GD.Print($"PHASE55_VALIDATION_RESULT errors={_errors}");
        Finish();
    }

    public override void _Process(double delta)
    {
        if (_finishFramesRemaining < 0)
        {
            return;
        }

        _finishFramesRemaining--;
        if (_finishFramesRemaining <= 0)
        {
            GetTree().Quit(_errors == 0 ? 0 : 1);
        }
    }

    private EncounterPatternStats ValidateData(string label, EnemyDataResource data)
    {
        if (data == null || data.RhythmProfile == null)
        {
            Require(false, $"{label} não possui EnemyData/Profile.");
            return new EncounterPatternStats();
        }

        return ValidateProfile(label, data, data.RhythmProfile, Seed);
    }

    private EncounterPatternStats ValidatePhase(
        string label,
        EnemyDataResource data,
        int phaseIndex)
    {
        if (data == null)
        {
            Require(false, $"{label} não foi configurada.");
            return new EncounterPatternStats();
        }

        var phase = data.GetRhythmPhase(phaseIndex);
        if (phase == null || phase.RhythmProfile == null)
        {
            Require(false, $"{label} não foi configurada.");
            return new EncounterPatternStats();
        }

        return ValidateProfile(label, data, phase.RhythmProfile, Seed + phaseIndex + 1);
    }

    private EncounterPatternStats ValidateProfile(
        string label,
        EnemyDataResource data,
        EnemyRhythmProfileResource profile,
        int seed)
    {
        var first = EncounterPatternGenerator.Generate(
            BasePattern,
            profile,
            unchecked((ulong)Math.Max(0, seed)),
            10.0f,
            data?.BaseAttackDamage ?? 20.0f,
            FairnessConfig);
        var second = EncounterPatternGenerator.Generate(
            BasePattern,
            profile,
            unchecked((ulong)Math.Max(0, seed)),
            10.0f,
            data?.BaseAttackDamage ?? 20.0f,
            FairnessConfig);
        var stats = first.Stats;
        var expectedSpacing = FairnessConfig.GetEffectiveMinimumTargetSpacingBeats();
        var expectedBurstSpacing = Math.Max(
            expectedSpacing,
            Math.Max(0.5f, FairnessConfig.MinimumBurstSpacingBeats));

        Require(stats.EventCount > 0, $"{label} não gerou eventos.");
        Require(stats.MinimumTargetSpacingBeats + 0.001f >= expectedSpacing,
            $"{label} possui target spacing abaixo do fairness global.");
        Require(stats.MaximumActiveLogicalPrompts <=
                FairnessConfig.MaximumActiveLogicalPrompts,
            $"{label} excedeu prompts lógicos ativos.");
        Require(stats.MaximumBurstLength <= FairnessConfig.GetEffectiveMaximumBurstLength(),
            $"{label} excedeu o tamanho máximo de burst.");
        Require(stats.MinimumBurstSpacingBeats == 0.0f ||
                stats.MinimumBurstSpacingBeats + 0.001f >= expectedBurstSpacing,
            $"{label} possui burst spacing abaixo do fairness global.");
        Require(GetSignature(first.Pattern) == GetSignature(second.Pattern),
            $"{label} não é determinístico com a mesma seed.");

        foreach (var rhythmEvent in first.Pattern.GetEventsSorted())
        {
            if (rhythmEvent.ChainId > 0)
            {
                Require(rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.Normal,
                    $"{label} colocou modifier visual dentro de burst.");
            }

            if (rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.Decoy)
            {
                Require(rhythmEvent.GetPromptType() == RhythmPromptType.PlayerAttack,
                    $"{label} colocou Decoy em EnemyAttack.");
            }

            if (rhythmEvent.GetPromptType() == RhythmPromptType.Execution)
            {
                Require(rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.Normal,
                    $"{label} colocou modifier em Execution.");
            }
        }

        GD.Print(
            $"PHASE55_PROFILE name={label} events={stats.EventCount} " +
            $"player={stats.PlayerAttackCount} enemy={stats.EnemyAttackCount} " +
            $"half={stats.HalfBeatCount} bursts={stats.BurstCount} " +
            $"burst_max={stats.MaximumBurstLength} " +
            $"feints={stats.FeintCount} fades={stats.FadeCount} " +
            $"decoys={stats.DecoyCount} " +
            $"min_spacing={stats.MinimumTargetSpacingBeats:0.00} " +
            $"avg_spacing={stats.AverageTargetSpacingBeats:0.00}");
        return stats;
    }

    private void ValidatePromptBehaviors()
    {
        if (PromptScene == null)
        {
            Require(false, "PromptScene não foi configurada.");
            return;
        }

        const double targetTime = 10.0d;
        const double beatDuration = 60.0d / 135.0d;
        var feintPrompt = PromptScene.Instantiate<RhythmPrompt>();
        AddChild(feintPrompt);
        feintPrompt.Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.Feint,
            1.25f,
            0.5f,
            0.1f);
        Require(Math.Abs(
                    feintPrompt.GetJudgementCircleSizePercentAt(targetTime) - 100.0f) < 0.01f,
            "Feint alterou o target real.");
        Require(Math.Abs(
                    feintPrompt.GetCircleSizePercentAt(targetTime - beatDuration * 0.28d) -
                    feintPrompt.GetJudgementCircleSizePercentAt(targetTime - beatDuration * 0.28d)) >
                0.01f,
            "Feint não alterou apenas a interpolação visual.");
        feintPrompt.QueueFree();

        var fadePrompt = PromptScene.Instantiate<RhythmPrompt>();
        AddChild(fadePrompt);
        fadePrompt.Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.FadeBeforeTarget,
            1.25f,
            0.5f,
            0.1f);
        Require(!fadePrompt.IsOuterRingVisibleAt(targetTime - beatDuration * 0.4d),
            "Fade não escondeu o círculo no momento configurado.");
        Require(Math.Abs(
                    fadePrompt.GetJudgementCircleSizePercentAt(targetTime) - 100.0f) < 0.01f,
            "Fade alterou o target real.");
        fadePrompt.QueueFree();

        var decoyPrompt = PromptScene.Instantiate<RhythmPrompt>();
        AddChild(decoyPrompt);
        decoyPrompt.Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.Decoy,
            1.25f,
            0.5f,
            0.1f);
        Require(decoyPrompt.IsDecoyVisibleAt(targetTime - beatDuration * 0.2d),
            "Decoy visual não apareceu antes do target.");
        Require(!decoyPrompt.IsDecoyVisibleAt(targetTime),
            "Decoy visual continuou visível no target.");
        Require(Math.Abs(
                    decoyPrompt.GetJudgementCircleSizePercentAt(targetTime) - 100.0f) < 0.01f,
            "Decoy alterou o target real.");
        decoyPrompt.QueueFree();

        GD.Print("PHASE55_PROMPT_BEHAVIORS feint_target=preserved fade_target=preserved decoy_logical=1");
    }

    private void ValidateEnemyData(EnemyDataResource data)
    {
        if (data == null || data.EnemyScene == null)
        {
            Require(false, "EnemyData precisa referenciar EnemyScene.");
            return;
        }

        var enemy = data.EnemyScene.Instantiate<EnemyBase>();
        enemy.Configure(data);
        AddChild(enemy);
        Require(enemy.EnemyName == data.EnemyName,
            $"Cena de {data.EnemyName} não recebeu o nome do Resource.");
        Require(Math.Abs(enemy.MaxHealth - data.MaxHealth) < 0.01f,
            $"Cena de {data.EnemyName} carregou HP incorreto.");
        Require(Math.Abs(enemy.MaxPosture - data.MaxPosture) < 0.01f,
            $"Cena de {data.EnemyName} carregou postura incorreta.");
        if (data.EnemyName == "THE TOWER")
        {
            var towerArt = enemy.GetNode<Sprite2D>("VisualArt");
            Require(towerArt.Visible && towerArt.Texture != null,
                "The Tower precisa carregar seu Sprite2D de arte pela cena.");
        }
        enemy.QueueFree();
    }

    private void ValidateBerserkerPhases()
    {
        Require(KingOfWandsData != null && KingOfWandsData.RhythmPhases.Count == 3,
            "King of Wands precisa possuir exatamente três fases.");
        if (KingOfWandsData == null || KingOfWandsData.RhythmPhases.Count != 3)
        {
            return;
        }

        var phaseSamples = new[] { 1.0f, 0.66f, 0.33f, 0.1f };
        var expectedIndices = new[] { 0, 1, 2, 2 };
        for (var index = 0; index < phaseSamples.Length; index++)
        {
            var profile = KingOfWandsData.GetRhythmProfileForHealthPercent(
                phaseSamples[index],
                out var phaseIndex);
            Require(profile != null && phaseIndex == expectedIndices[index],
                $"King of Wands selecionou fase incorreta em HP {phaseSamples[index]:P0}.");
        }
    }

    private static string GetSignature(RhythmPatternResource pattern)
    {
        var signature = new StringBuilder();
        foreach (var rhythmEvent in pattern.GetEventsSorted())
        {
            signature.Append(
                $"{rhythmEvent.BeatOffset:0.00}:" +
                $"{rhythmEvent.PromptType}:" +
                $"{rhythmEvent.PromptBehavior}:" +
                $"{rhythmEvent.ChainId}:" +
                $"{rhythmEvent.ChainIndex}:" +
                $"{rhythmEvent.ChainLength}|");
        }

        return signature.ToString();
    }

    private void Require(bool condition, string message)
    {
        if (condition)
        {
            return;
        }

        _errors++;
        GD.PrintErr($"PHASE55_VALIDATION_ERROR {message}");
    }

    private void Finish()
    {
        _finishFramesRemaining = 3;
    }
}

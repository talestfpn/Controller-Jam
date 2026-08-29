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
    public EnemyDataResource InitiateData { get; set; } = null!;

    [Export]
    public EnemyDataResource BruteData { get; set; } = null!;

    [Export]
    public EnemyDataResource DuelistData { get; set; } = null!;

    [Export]
    public EnemyDataResource AssassinData { get; set; } = null!;

    [Export]
    public EnemyDataResource MageData { get; set; } = null!;

    [Export]
    public EnemyDataResource KnightData { get; set; } = null!;

    [Export]
    public EnemyDataResource BerserkerData { get; set; } = null!;

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
        var initiate = ValidateData("INITIATE", InitiateData);
        var brute = ValidateData("BRUTE", BruteData);
        var duelist = ValidateData("DUELIST", DuelistData);
        var assassin = ValidateData("ASSASSIN", AssassinData);
        var mage = ValidateData("MAGE", MageData);
        var knight = ValidateData("KNIGHT", KnightData);
        var berserkerPhase1 = ValidatePhase("BERSERKER_P1", BerserkerData, 0);
        var berserkerPhase2 = ValidatePhase("BERSERKER_P2", BerserkerData, 1);
        var berserkerPhase3 = ValidatePhase("BERSERKER_P3", BerserkerData, 2);

        Require(duelist.EventCount > initiate.EventCount,
            "Duelist deve ser mais denso que Initiate.");
        Require(assassin.EventCount > duelist.EventCount,
            "Assassin deve ser mais denso que Duelist.");
        Require(assassin.EnemyAttackCount > duelist.EnemyAttackCount,
            "Assassin deve possuir mais pressão de EnemyAttack que Duelist.");
        Require(mage.FeintCount > 0 && mage.FadeCount > 0 && mage.DecoyCount > 0,
            "Mage precisa gerar Feint, Fade e Decoy.");
        Require(knight.DecoyCount == 0 && knight.FeintCount == 0 && knight.FadeCount == 0,
            "Knight não deve possuir modifiers visuais.");
        Require(berserkerPhase3.EventCount > berserkerPhase1.EventCount,
            "Berserker Phase 3 deve ser mais densa que Phase 1.");
        Require(berserkerPhase3.EnemyAttackCount > berserkerPhase1.EnemyAttackCount,
            "Berserker Phase 3 deve possuir mais EnemyAttack que Phase 1.");
        Require(berserkerPhase3.BurstCount >= berserkerPhase1.BurstCount,
            "Berserker Phase 3 não deve perder identidade de burst.");

        ValidatePromptBehaviors();
        ValidateEnemyData(InitiateData);
        ValidateEnemyData(BruteData);
        ValidateEnemyData(DuelistData);
        ValidateEnemyData(AssassinData);
        ValidateEnemyData(MageData);
        ValidateEnemyData(KnightData);
        ValidateEnemyData(BerserkerData);
        ValidateBerserkerPhases();

        Require(KnightData.Armored, "Knight precisa estar marcado como Armored.");
        Require(Math.Abs(KnightData.HealthDamageMultiplierWhilePostureActive - 0.25f) < 0.001f,
            "Knight precisa usar Armor multiplier 0.25.");
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
        enemy.QueueFree();
    }

    private void ValidateBerserkerPhases()
    {
        Require(BerserkerData != null && BerserkerData.RhythmPhases.Count == 3,
            "Berserker precisa possuir exatamente três fases.");
        if (BerserkerData == null || BerserkerData.RhythmPhases.Count != 3)
        {
            return;
        }

        var phaseSamples = new[] { 1.0f, 0.66f, 0.33f, 0.1f };
        var expectedIndices = new[] { 0, 1, 2, 2 };
        for (var index = 0; index < phaseSamples.Length; index++)
        {
            var profile = BerserkerData.GetRhythmProfileForHealthPercent(
                phaseSamples[index],
                out var phaseIndex);
            Require(profile != null && phaseIndex == expectedIndices[index],
                $"Berserker selecionou fase incorreta em HP {phaseSamples[index]:P0}.");
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

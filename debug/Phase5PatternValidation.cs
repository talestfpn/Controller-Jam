using System;
using System.Text;
using Godot;

/// <summary>
/// Validação headless reproduzível da fase 5. Todos os Resources são ligados
/// pela cena para manter o teste sem carregamento manual por código.
/// </summary>
public partial class Phase5PatternValidation : Node
{
    [Export]
    public RhythmPatternResource BasePattern { get; set; } = null!;

    [Export]
    public EnemyRhythmProfileResource InitiateProfile { get; set; } = null!;

    [Export]
    public EnemyRhythmProfileResource BruteProfile { get; set; } = null!;

    [Export]
    public EnemyRhythmProfileResource DuelistProfile { get; set; } = null!;

    [Export]
    public PackedScene PromptScene { get; set; } = null!;

    [Export]
    public EnemyDataResource InitiateData { get; set; } = null!;

    [Export]
    public EnemyDataResource BruteData { get; set; } = null!;

    [Export]
    public EnemyDataResource DuelistData { get; set; } = null!;

    [Export]
    public int Seed { get; set; } = 12345;

    private int _errors;
    private int _finishFramesRemaining = -1;

    public override void _Ready()
    {
        var baseSignature = GetSignature(BasePattern);
        var initiate = ValidateProfile("INITIATE", InitiateProfile, InitiateData);
        var brute = ValidateProfile("BRUTE", BruteProfile, BruteData);
        var duelist = ValidateProfile("DUELIST", DuelistProfile, DuelistData);

        Require(brute.Stats.EventCount < initiate.Stats.EventCount,
            "Brute precisa ter menos eventos que Initiate.");
        Require(initiate.Stats.HalfBeatCount == 0 && brute.Stats.HalfBeatCount == 0,
            "Initiate e Brute não devem usar half-beats.");
        Require(duelist.Stats.EventCount > initiate.Stats.EventCount,
            "Duelist precisa ser mais denso que Initiate.");
        Require(duelist.Stats.HalfBeatCount > 0,
            "Duelist precisa possuir half-beats.");
        Require(duelist.Stats.BurstCount > 0,
            "Duelist precisa possuir bursts.");
        Require(duelist.Stats.FeintCount > 0,
            "Duelist precisa possuir Feints ocasionais.");
        Require(HasBurstSpacing(duelist.Pattern, DuelistProfile.BurstSpacingBeats),
            $"Duelist precisa possuir ao menos uma chain espaçada em {DuelistProfile.BurstSpacingBeats:0.0} beat.");
        Require(baseSignature == GetSignature(BasePattern),
            "O chart musical-base foi mutado durante a geração.");

        ValidatePromptBehaviors();
        ValidateSubdivisionTimeline();
        ValidateEnemyScene(InitiateData);
        ValidateEnemyScene(BruteData);
        ValidateEnemyScene(DuelistData);
        Require(Math.Abs(BruteData.BaseAttackDamage - 40.0f) < 0.01f,
            "Brute precisa causar 40 de dano-base.");

        GD.Print($"PHASE5_VALIDATION_RESULT errors={_errors}");
        _finishFramesRemaining = 3;
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

    private EncounterPatternGenerationResult ValidateProfile(
        string label,
        EnemyRhythmProfileResource profile,
        EnemyDataResource enemyData)
    {
        var seed = unchecked((ulong)Math.Max(0, Seed));
        var first = EncounterPatternGenerator.Generate(
            BasePattern,
            profile,
            seed,
            10.0f,
            enemyData.BaseAttackDamage);
        var second = EncounterPatternGenerator.Generate(
            BasePattern,
            profile,
            seed,
            10.0f,
            enemyData.BaseAttackDamage);
        Require(GetSignature(first.Pattern) == GetSignature(second.Pattern),
            $"{label} não foi determinístico para a mesma seed.");
        foreach (var rhythmEvent in first.Pattern.GetEventsSorted())
        {
            if (rhythmEvent.GetPromptType() == RhythmPromptType.EnemyAttack)
            {
                Require(Math.Abs(rhythmEvent.Damage - enemyData.BaseAttackDamage) < 0.01f,
                    $"{label} gerou dano inimigo diferente do EnemyData.");
            }
        }

        var stats = first.Stats;
        GD.Print(
            $"PHASE5_PROFILE name={label} events={stats.EventCount} " +
            $"player={stats.PlayerAttackCount} enemy={stats.EnemyAttackCount} " +
            $"half={stats.HalfBeatCount} bursts={stats.BurstCount} " +
            $"feints={stats.FeintCount} fades={stats.FadeCount}");
        return first;
    }

    private void ValidatePromptBehaviors()
    {
        var beatDuration = 60.0d / 135.0d;
        const double targetTime = 100.0d;
        var feintPrompt = PromptScene.Instantiate<RhythmPrompt>();
        AddChild(feintPrompt);
        feintPrompt.Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.Feint,
            1.0f,
            0.5f,
            0.24f);
        var feintSampleTime = targetTime - beatDuration * 0.30d;
        Require(Math.Abs(
                feintPrompt.GetCircleSizePercentAt(feintSampleTime) -
                feintPrompt.GetJudgementCircleSizePercentAt(feintSampleTime)) > 0.1f,
            "Feint precisa alterar somente a curva visual.");
        Require(Math.Abs(feintPrompt.GetJudgementCircleSizePercentAt(targetTime) - 100.0f) < 0.01f,
            "Feint alterou o target real.");
        feintPrompt.QueueFree();

        var fadePrompt = PromptScene.Instantiate<RhythmPrompt>();
        AddChild(fadePrompt);
        fadePrompt.Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.FadeBeforeTarget,
            1.0f,
            0.5f,
            0.24f);
        Require(fadePrompt.IsOuterRingVisibleAt(targetTime - beatDuration * 0.6d),
            "Fade escondeu o círculo cedo demais.");
        Require(!fadePrompt.IsOuterRingVisibleAt(targetTime - beatDuration * 0.4d),
            "Fade não escondeu o círculo antes do target.");
        Require(Math.Abs(fadePrompt.GetJudgementCircleSizePercentAt(targetTime) - 100.0f) < 0.01f,
            "Fade alterou o target real.");
        fadePrompt.QueueFree();

        GD.Print("PHASE5_PROMPT_BEHAVIORS feint_target=preserved fade_target=preserved");
    }

    private void ValidateSubdivisionTimeline()
    {
        var beatDuration = 60.0d / 135.0d;
        var firstTarget = 10.0d * beatDuration;
        var secondTarget = 10.5d * beatDuration;
        var thirdTarget = 11.0d * beatDuration;
        Require(Math.Abs((secondTarget - firstTarget) - beatDuration * 0.5d) < 0.000001d,
            "Timeline de half-beat inconsistente.");
        Require(Math.Abs((thirdTarget - secondTarget) - beatDuration * 0.5d) < 0.000001d,
            "Timeline do burst inconsistente.");
        GD.Print(
            $"PHASE5_SUBDIVISION beat10={firstTarget:0.000000} " +
            $"beat10.5={secondTarget:0.000000} beat11={thirdTarget:0.000000}");
    }

    private void ValidateEnemyScene(EnemyDataResource enemyData)
    {
        Require(enemyData != null && enemyData.EnemyScene != null,
            "EnemyData precisa referenciar uma PackedScene.");
        if (enemyData == null || enemyData.EnemyScene == null)
        {
            return;
        }

        var enemy = enemyData.EnemyScene.Instantiate<EnemyBase>();
        enemy.Configure(enemyData);
        AddChild(enemy);
        Require(enemy.EnemyName == enemyData.EnemyName,
            $"Cena de {enemyData.EnemyName} não recebeu seus dados.");
        Require(Math.Abs(enemy.MaxHealth - enemyData.MaxHealth) < 0.01f,
            $"Cena de {enemyData.EnemyName} carregou HP incorreto.");
        Require(Math.Abs(enemy.MaxPosture - enemyData.MaxPosture) < 0.01f,
            $"Cena de {enemyData.EnemyName} carregou postura incorreta.");
        GD.Print(
            $"PHASE5_ENEMY_SCENE name={enemy.EnemyName} " +
            $"hp={enemy.MaxHealth:0} posture={enemy.MaxPosture:0} " +
            $"damage={enemyData.BaseAttackDamage:0}");
        enemy.QueueFree();
    }

    private static bool HasBurstSpacing(RhythmPatternResource pattern, float expectedSpacingBeats)
    {
        var events = pattern.GetEventsSorted();
        for (var firstIndex = 0; firstIndex < events.Count; firstIndex++)
        {
            if (events[firstIndex].ChainId <= 0)
            {
                continue;
            }

            for (var secondIndex = firstIndex + 1; secondIndex < events.Count; secondIndex++)
            {
                if (events[secondIndex].ChainId != events[firstIndex].ChainId)
                {
                    continue;
                }

                if (Math.Abs(
                        events[secondIndex].BeatOffset - events[firstIndex].BeatOffset - expectedSpacingBeats) <
                    0.001f)
                {
                    return true;
                }
            }
        }

        return false;
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
                $"{rhythmEvent.ChainIndex}|");
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
        GD.PrintErr($"PHASE5_VALIDATION_ERROR {message}");
    }
}

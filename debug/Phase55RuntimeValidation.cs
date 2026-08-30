using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Executa um encontro real com a música como clock e resolve os prompts no
/// target musical. A mesma rotina valida spacing, telegraph, modifiers e a
/// garantia de um único prompt lógico ativo.
/// </summary>
public partial class Phase55RuntimeValidation : Node
{
    [Export]
    public PackedScene CombatScene { get; set; } = null!;

    [Export(PropertyHint.Range, "10.0,180.0,1.0")]
    public float ValidationDurationSeconds { get; set; } = 60.0f;

    [Export(PropertyHint.Range, "0,20,1")]
    public int EnemyIndex { get; set; }

    [Export]
    public string ExpectedEnemyName { get; set; } = "ENEMY";

    [Export]
    public bool RequireHalfBeats { get; set; }

    [Export]
    public bool RequireBursts { get; set; }

    [Export]
    public bool RequireFeints { get; set; }

    [Export]
    public bool RequireFades { get; set; }

    [Export]
    public bool RequireDecoys { get; set; }

    [Export]
    public bool RequireArmor { get; set; }

    private readonly HashSet<ulong> _seenPrompts = new();
    private readonly Dictionary<int, int> _runtimeChainLengths = new();
    private readonly Dictionary<int, double> _lastChainBeats = new();
    private RhythmManager _rhythmManager = null!;
    private CombatController _combat = null!;
    private Node2D _promptContainer = null!;
    private double _maximumGridErrorSeconds;
    private double _maximumSchedulingLatenessSeconds;
    private float _maximumTargetScaleError;
    private double _minimumTargetSpacingBeats = double.PositiveInfinity;
    private double _minimumTelegraphBeats = double.PositiveInfinity;
    private double _minimumBurstSpacingBeats = double.PositiveInfinity;
    private double _previousTargetBeat = double.NaN;
    private int _promptCount;
    private int _halfBeatCount;
    private int _halfBeatTransitions;
    private int _feintCount;
    private int _fadeCount;
    private int _decoyCount;
    private int _maximumSimultaneousPrompts;
    private bool _started;
    private bool _finishing;
    private int _finishFramesRemaining;
    private int _finishExitCode;
    private bool _armorWasLoaded;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _combat = CombatScene.Instantiate<CombatController>();
        _combat.StartingEnemyIndex = EnemyIndex;
        _combat.DebugRhythm = false;
        _combat.DebugComboTrainingDummy = true;
        _combat.DebugComboDummyHealth = 9999.0f;
        AddChild(_combat);
        _promptContainer = _combat.GetNode<Node2D>("PromptContainer");
        if (RequireArmor)
        {
            _armorWasLoaded = _combat.GetNode<EnemyBase>(
                "EnemyContainer/KnightOfPentacles").IsArmored;
        }
    }

    public override void _Process(double delta)
    {
        if (_finishing)
        {
            _finishFramesRemaining--;
            if (_finishFramesRemaining <= 0)
            {
                GetTree().Quit(_finishExitCode);
            }

            return;
        }

        var musicPosition = _rhythmManager.GetMusicPosition();
        if (!_started)
        {
            if (musicPosition < 0.0d)
            {
                return;
            }

            _started = true;
            GD.Print(
                $"PHASE55_RUNTIME_START enemy={ExpectedEnemyName} " +
                $"duration={ValidationDurationSeconds:0}s");
        }

        var activePromptCount = 0;
        foreach (var child in _promptContainer.GetChildren())
        {
            if (child is not RhythmPrompt prompt || prompt.IsResolved)
            {
                continue;
            }

            activePromptCount++;
            RegisterPrompt(prompt);
            if (musicPosition >= prompt.TargetTime)
            {
                _maximumSchedulingLatenessSeconds = Math.Max(
                    _maximumSchedulingLatenessSeconds,
                    musicPosition - prompt.TargetTime);
                prompt.ResolveInputAt(prompt.TargetTime);
            }
        }

        _maximumSimultaneousPrompts = Math.Max(
            _maximumSimultaneousPrompts,
            activePromptCount);

        if (musicPosition < ValidationDurationSeconds)
        {
            return;
        }

        FinishValidation();
    }

    private void RegisterPrompt(RhythmPrompt prompt)
    {
        if (!_seenPrompts.Add(prompt.GetInstanceId()))
        {
            return;
        }

        _promptCount++;
        var targetBeat = prompt.TargetTime / _rhythmManager.BeatDuration;
        var nearestHalfBeat = Math.Round(targetBeat * 2.0d) / 2.0d;
        _maximumGridErrorSeconds = Math.Max(
            _maximumGridErrorSeconds,
            Math.Abs(targetBeat - nearestHalfBeat) * _rhythmManager.BeatDuration);
        _maximumTargetScaleError = Math.Max(
            _maximumTargetScaleError,
            Math.Abs(prompt.GetJudgementCircleSizePercentAt(prompt.TargetTime) - 100.0f));
        _minimumTelegraphBeats = Math.Min(
            _minimumTelegraphBeats,
            (prompt.TargetTime - prompt.StartTime) / _rhythmManager.BeatDuration);

        var fraction = targetBeat - Math.Floor(targetBeat);
        if (Math.Abs(fraction - 0.5d) < 0.001d)
        {
            _halfBeatCount++;
        }

        if (!double.IsNaN(_previousTargetBeat))
        {
            var targetSpacing = targetBeat - _previousTargetBeat;
            _minimumTargetSpacingBeats = Math.Min(
                _minimumTargetSpacingBeats,
                targetSpacing);
            if (Math.Abs(targetSpacing - 0.5d) < 0.001d)
            {
                _halfBeatTransitions++;
            }
        }

        _previousTargetBeat = targetBeat;
        if (prompt.Behavior == RhythmPromptBehavior.Feint)
        {
            _feintCount++;
        }
        else if (prompt.Behavior == RhythmPromptBehavior.FadeBeforeTarget)
        {
            _fadeCount++;
        }
        else if (prompt.Behavior == RhythmPromptBehavior.Decoy)
        {
            _decoyCount++;
        }

        var patternEvent = _combat.ActivePatternEvent;
        if (patternEvent == null || patternEvent.ChainId <= 0)
        {
            return;
        }

        if (!_runtimeChainLengths.TryGetValue(patternEvent.ChainId, out var chainLength))
        {
            chainLength = 0;
        }

        _runtimeChainLengths[patternEvent.ChainId] = chainLength + 1;
        if (_lastChainBeats.TryGetValue(patternEvent.ChainId, out var lastChainBeat))
        {
            _minimumBurstSpacingBeats = Math.Min(
                _minimumBurstSpacingBeats,
                targetBeat - lastChainBeat);
        }

        _lastChainBeats[patternEvent.ChainId] = targetBeat;
    }

    private void FinishValidation()
    {
        var errors = 0;
        var fairness = _combat.FairnessConfig ?? new RhythmFairnessConfigResource();
        var expectedSpacing = fairness.GetEffectiveMinimumTargetSpacingBeats();
        var expectedBurstSpacing = Math.Max(
            expectedSpacing,
            Math.Max(0.5f, fairness.MinimumBurstSpacingBeats));
        var observedMinimumSpacing = double.IsPositiveInfinity(_minimumTargetSpacingBeats)
            ? expectedSpacing
            : _minimumTargetSpacingBeats;
        var observedMinimumTelegraph = double.IsPositiveInfinity(_minimumTelegraphBeats)
            ? 0.0d
            : _minimumTelegraphBeats;
        var observedMinimumBurstSpacing = double.IsPositiveInfinity(_minimumBurstSpacingBeats)
            ? expectedBurstSpacing
            : _minimumBurstSpacingBeats;

        errors += Require(_promptCount > 0, "nenhum prompt foi executado");
        if (RequireHalfBeats)
        {
            errors += Require(_halfBeatCount > 0, "nenhum half-beat foi executado");
        }

        if (RequireBursts)
        {
            errors += Require(_runtimeChainLengths.Count > 0, "nenhum burst foi executado");
        }

        if (RequireFeints)
        {
            errors += Require(_feintCount > 0, "nenhum Feint foi executado");
        }

        if (RequireFades)
        {
            errors += Require(_fadeCount > 0, "nenhum Fade foi executado");
        }

        if (RequireDecoys)
        {
            errors += Require(_decoyCount > 0, "nenhum Decoy foi executado");
        }

        if (RequireArmor)
        {
            errors += Require(_armorWasLoaded, "Knight of Pentacles não carregou Armor no início do combate");
        }

        errors += Require(
            _maximumSimultaneousPrompts <= fairness.MaximumActiveLogicalPrompts,
            "mais de um prompt lógico ficou ativo");
        errors += Require(
            observedMinimumSpacing + 0.01d >= expectedSpacing,
            "target spacing observado abaixo do fairness global");
        errors += Require(
            observedMinimumTelegraph + 0.01d >= fairness.MinimumTelegraphBeats,
            "telegraph observado abaixo do fairness global");
        errors += Require(
            observedMinimumBurstSpacing + 0.01d >= expectedBurstSpacing,
            "burst spacing observado abaixo do fairness global");
        errors += Require(_maximumGridErrorSeconds < 0.000001d, "target saiu da grid musical");
        errors += Require(_maximumTargetScaleError < 0.01f, "target visual/julgamento saiu de 100%");

        var musicPosition = _rhythmManager.GetMusicPosition();
        GD.Print(
            $"PHASE55_RUNTIME_RESULT enemy={ExpectedEnemyName} duration={musicPosition:0.000}s " +
            $"prompts={_promptCount} half={_halfBeatCount} " +
            $"half_transitions={_halfBeatTransitions} bursts={_runtimeChainLengths.Count} " +
            $"feints={_feintCount} fades={_fadeCount} decoys={_decoyCount} " +
            $"min_spacing={observedMinimumSpacing:0.000} " +
            $"min_telegraph={observedMinimumTelegraph:0.000} " +
            $"min_burst={observedMinimumBurstSpacing:0.000} " +
            $"max_active={_maximumSimultaneousPrompts} " +
            $"grid_error_ms={_maximumGridErrorSeconds * 1000.0d:0.000} " +
            $"target_scale_error={_maximumTargetScaleError:0.000} " +
            $"schedule_lateness_ms={_maximumSchedulingLatenessSeconds * 1000.0d:0.000} " +
            $"errors={errors}");
        _finishExitCode = errors == 0 ? 0 : 1;
        _finishFramesRemaining = 3;
        _finishing = true;
        _rhythmManager.Stop();
        _combat.QueueFree();
    }

    private static int Require(bool condition, string message)
    {
        if (condition)
        {
            return 0;
        }

        GD.PrintErr($"PHASE55_RUNTIME_ERROR {message}");
        return 1;
    }
}

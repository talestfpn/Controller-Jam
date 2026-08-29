using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Executa o combate real do Duelist por 60 segundos usando a timeline da
/// música e acerta cada prompt no target para validar fila, chains e drift.
/// </summary>
public partial class Phase5DuelistRuntimeValidation : Node
{
    [Export]
    public PackedScene CombatScene { get; set; } = null!;

    [Export(PropertyHint.Range, "10.0,180.0,1.0")]
    public float ValidationDurationSeconds { get; set; } = 60.0f;

    [Export(PropertyHint.Range, "0,20,1")]
    public int EnemyIndex { get; set; } = 2;

    [Export]
    public string ExpectedEnemyName { get; set; } = "THE DUELIST";

    [Export]
    public bool RequireHalfBeats { get; set; } = true;

    [Export]
    public bool RequireHalfBeatTransitions { get; set; } = true;

    [Export]
    public bool RequireFeints { get; set; } = true;

    private readonly HashSet<ulong> _seenPrompts = new();
    private RhythmManager _rhythmManager = null!;
    private CombatController _combat = null!;
    private Node2D _promptContainer = null!;
    private double _maximumGridErrorSeconds;
    private double _maximumSchedulingLatenessSeconds;
    private float _maximumTargetScaleError;
    private double _previousTargetBeat = double.NaN;
    private int _promptCount;
    private int _halfBeatCount;
    private int _halfBeatTransitions;
    private int _feintCount;
    private int _fadeCount;
    private int _maximumSimultaneousPrompts;
    private bool _started;
    private bool _finishing;
    private int _finishFramesRemaining;
    private int _finishExitCode;

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
                $"PHASE5_RUNTIME_START enemy={ExpectedEnemyName} " +
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

        var errors = 0;
        errors += Require(_promptCount > 0, "nenhum prompt foi executado");
        if (RequireHalfBeats)
        {
            errors += Require(_halfBeatCount > 0, "nenhum half-beat foi executado");
        }

        if (RequireHalfBeatTransitions)
        {
            errors += Require(_halfBeatTransitions > 0, "nenhuma chain de 0.5 beat foi executada");
        }

        if (RequireFeints)
        {
            errors += Require(_feintCount > 0, "nenhum Feint foi executado");
        }
        errors += Require(_maximumSimultaneousPrompts <= 1, "mais de um prompt ficou ativo");
        errors += Require(_maximumGridErrorSeconds < 0.000001d, "target saiu da grid musical");
        errors += Require(_maximumTargetScaleError < 0.01f, "target visual/julgamento saiu de 100%");

        GD.Print(
            $"PHASE5_RUNTIME_RESULT enemy={ExpectedEnemyName} duration={musicPosition:0.000}s " +
            $"prompts={_promptCount} half={_halfBeatCount} " +
            $"half_transitions={_halfBeatTransitions} feints={_feintCount} fades={_fadeCount} " +
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

        var fraction = targetBeat - Math.Floor(targetBeat);
        if (Math.Abs(fraction - 0.5d) < 0.001d)
        {
            _halfBeatCount++;
        }

        if (!double.IsNaN(_previousTargetBeat) &&
            Math.Abs(targetBeat - _previousTargetBeat - 0.5d) < 0.001d)
        {
            _halfBeatTransitions++;
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
    }

    private static int Require(bool condition, string message)
    {
        if (condition)
        {
            return 0;
        }

        GD.PrintErr($"PHASE5_RUNTIME_ERROR {message}");
        return 1;
    }
}

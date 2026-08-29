using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Valida a troca de fase do Berserker em runtime: o dano é aplicado enquanto
/// prompts estão ativos, os prompts são resolvidos no target e a nova fase só
/// pode assumir o próximo trecho futuro da música.
/// </summary>
public partial class Phase55BerserkerPhaseValidation : Node
{
    [Export]
    public PackedScene CombatScene { get; set; } = null!;

    private RhythmManager _rhythmManager = null!;
    private CombatController _combat = null!;
    private EnemyBase _enemy = null!;
    private Node2D _promptContainer = null!;
    private double _initialMusicPosition;
    private float _initialBpm;
    private double _previousTargetTime = double.NaN;
    private readonly HashSet<ulong> _seenPrompts = new();
    private int _maximumActivePrompts;
    private int _stage;
    private int _errors;
    private int _finishFramesRemaining = -1;
    private bool _phaseTwoSeen;
    private bool _phaseThreeSeen;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _combat = CombatScene.Instantiate<CombatController>();
        _combat.StartingEnemyIndex = 6;
        _combat.DebugRhythm = false;
        AddChild(_combat);
        _enemy = _combat.GetNode<EnemyBase>("EnemyContainer/TheBerserker");
        _promptContainer = _combat.GetNode<Node2D>("PromptContainer");
        _initialBpm = _rhythmManager.Bpm;
    }

    public override void _Process(double delta)
    {
        if (_finishFramesRemaining >= 0)
        {
            _finishFramesRemaining--;
            if (_finishFramesRemaining <= 0)
            {
                GetTree().Quit(_errors == 0 ? 0 : 1);
            }

            return;
        }

        var musicPosition = _rhythmManager.GetMusicPosition();
        var activePrompt = GetActivePrompt();
        _maximumActivePrompts = Math.Max(
            _maximumActivePrompts,
            activePrompt == null ? 0 : 1);

        if (activePrompt != null)
        {
            if (_seenPrompts.Add(activePrompt.GetInstanceId()))
            {
                if (double.IsNaN(_previousTargetTime))
                {
                    _initialMusicPosition = musicPosition;
                }

                Require(activePrompt.TargetTime > _previousTargetTime ||
                        double.IsNaN(_previousTargetTime),
                    "Berserker gerou target retroativo.");
                _previousTargetTime = activePrompt.TargetTime;
            }

            if (musicPosition >= activePrompt.TargetTime)
            {
                activePrompt.ResolveInputAt(activePrompt.TargetTime);
            }
        }

        if (_stage == 0 && activePrompt != null)
        {
            // 230 -> 150 HP: entra na Phase 2 enquanto há prompt ativo.
            _enemy.TakeDamage(80.0f);
            _stage = 1;
        }
        else if (_stage == 1 && _combat.CurrentRhythmPhaseIndex == 1)
        {
            _phaseTwoSeen = true;
            if (activePrompt != null)
            {
                // 150 -> 70 HP: entra na Phase 3 sem reiniciar a música.
                _enemy.TakeDamage(80.0f);
                _stage = 2;
            }
        }
        else if (_stage == 2 && _combat.CurrentRhythmPhaseIndex == 2)
        {
            _phaseThreeSeen = true;
            FinishValidation();
        }

        if (musicPosition - _initialMusicPosition > 15.0d && _stage < 2)
        {
            Require(false, "Berserker não trocou de fase no tempo esperado.");
            FinishValidation();
        }
    }

    private RhythmPrompt? GetActivePrompt()
    {
        foreach (var child in _promptContainer.GetChildren())
        {
            if (child is RhythmPrompt prompt && !prompt.IsResolved)
            {
                return prompt;
            }
        }

        return null;
    }

    private void FinishValidation()
    {
        Require(_phaseTwoSeen, "Phase 2 do Berserker não foi observada.");
        Require(_phaseThreeSeen, "Phase 3 do Berserker não foi observada.");
        Require(_combat.CurrentRhythmPhaseIndex == 2,
            "Berserker não terminou na Phase 3.");
        Require(!_combat.IsRhythmPhaseChangePending,
            "Berserker terminou com troca de fase pendente.");
        Require(_maximumActivePrompts <= 1,
            "Berserker criou mais de um prompt ativo.");
        Require(Math.Abs(_rhythmManager.Bpm - _initialBpm) < 0.001f,
            "Berserker alterou o BPM durante a troca de fase.");
        Require(_rhythmManager.GetMusicPosition() > _initialMusicPosition,
            "Berserker reiniciou a posição da música.");

        GD.Print(
            $"PHASE55_BERSERKER_PHASE_RESULT phase2={_phaseTwoSeen} " +
            $"phase3={_phaseThreeSeen} max_active={_maximumActivePrompts} " +
            $"bpm={_rhythmManager.Bpm:0.0} music_position={_rhythmManager.GetMusicPosition():0.000} " +
            $"errors={_errors}");
        _finishFramesRemaining = 3;
        _rhythmManager.Stop();
        _combat.QueueFree();
    }

    private void Require(bool condition, string message)
    {
        if (condition)
        {
            return;
        }

        _errors++;
        GD.PrintErr($"PHASE55_BERSERKER_PHASE_ERROR {message}");
    }
}

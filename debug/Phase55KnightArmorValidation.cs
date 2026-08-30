using System;
using Godot;

/// <summary>
/// Resolve um PERFECT ofensivo real contra o Knight e confirma a separação
/// entre dano bruto de HP, dano final com Armor e dano de postura.
/// </summary>
public partial class Phase55KnightArmorValidation : Node
{
    [Export]
    public PackedScene CombatScene { get; set; } = null!;

    private RhythmManager _rhythmManager = null!;
    private CombatController _combat = null!;
    private EnemyBase _enemy = null!;
    private Node2D _promptContainer = null!;
    private bool _started;
    private int _errors;
    private double _timeoutAt;
    private int _finishFramesRemaining = -1;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _combat = CombatScene.Instantiate<CombatController>();
        _combat.StartingEnemyIndex = 5;
        _combat.DebugRhythm = false;
        AddChild(_combat);
        _enemy = _combat.GetNode<EnemyBase>("EnemyContainer/KnightOfPentacles");
        _promptContainer = _combat.GetNode<Node2D>("PromptContainer");
        _timeoutAt = _rhythmManager.GetMusicPosition() + 30.0d;
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

        if (!_started)
        {
            _started = true;
            Require(_enemy.IsArmored, "Knight of Pentacles não está Armored.");
            Require(_enemy.IsArmorActive, "Armor do Knight of Pentacles não iniciou ativa.");
        }

        foreach (var child in _promptContainer.GetChildren())
        {
            if (child is not RhythmPrompt prompt ||
                prompt.IsResolved ||
                _combat.ActivePatternEvent == null)
            {
                continue;
            }

            if (_combat.ActivePatternEvent.GetPromptType() != RhythmPromptType.PlayerAttack)
            {
                if (_rhythmManager.GetMusicPosition() >= prompt.TargetTime)
                {
                    prompt.ResolveInputAt(prompt.TargetTime);
                }

                continue;
            }

            if (_rhythmManager.GetMusicPosition() < prompt.TargetTime)
            {
                continue;
            }

            var healthBefore = _enemy.CurrentHealth;
            var postureBefore = _enemy.CurrentPosture;
            prompt.ResolveInputAt(prompt.TargetTime);
            var healthDamage = healthBefore - _enemy.CurrentHealth;
            var postureDamage = postureBefore - _enemy.CurrentPosture;
            var expectedRawDamage = _combat.BaseAttackDamage *
                (_combat.TimingConfig?.PerfectDamageMultiplier ?? 1.25f);

            Require(Math.Abs(_combat.LastRawHealthDamage - expectedRawDamage) < 0.01f,
                "Dano bruto do PERFECT não corresponde ao dano base.");
            Require(Math.Abs(_combat.LastFinalHealthDamage -
                             _combat.LastRawHealthDamage * 0.25f) < 0.01f,
                "Armor não reduziu o dano de HP para 25%.");
            Require(Math.Abs(healthDamage - _combat.LastFinalHealthDamage) < 0.01f,
                "Dano de HP aplicado diverge do dano final calculado.");
            Require(postureDamage > 0.0f,
                "Armor reduziu ou removeu o dano de postura.");
            FinishValidation();
            return;
        }

        if (_rhythmManager.GetMusicPosition() >= _timeoutAt)
        {
            Require(false, "Não foi encontrado um PLAYER_ATTACK para testar Armor.");
            FinishValidation();
        }
    }

    private void FinishValidation()
    {
        GD.Print(
            $"PHASE55_KNIGHT_ARMOR_RESULT raw={_combat.LastRawHealthDamage:0.000} " +
            $"final={_combat.LastFinalHealthDamage:0.000} " +
            $"armor_multiplier={_enemy.ArmorHealthDamageMultiplier:0.00} errors={_errors}");
        _finishFramesRemaining = 10;
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
        GD.PrintErr($"PHASE55_KNIGHT_ARMOR_ERROR {message}");
    }
}

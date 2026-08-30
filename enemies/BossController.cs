using System;
using Godot;

/// <summary>
/// Comportamento específico do boss final: fases, limite seguro de dano,
/// armadura por fase e regeneração controlada da fase 7. A cena fornece o
/// nó; este componente não cria elementos visuais.
/// </summary>
public partial class BossController : Node
{
    [Signal]
    public delegate void PhaseActivatedEventHandler(int phaseIndex, string phaseName);

    [Signal]
    public delegate void RegenerationStartedEventHandler();

    [Signal]
    public delegate void RegenerationStoppedEventHandler();

    [Signal]
    public delegate void RegenerationTickEventHandler(float amount);

    [Export]
    public BossDataResource BossData { get; set; } = null!;

    private EnemyBase _enemy = null!;
    private BossDataResource? _configuredData;
    private BossPhaseResource? _activePhase;
    private int _activePhaseIndex = -1;
    private double _timeSinceLastPerfect;
    private float _regeneratedHealth;
    private bool _regenerationActive;
    private bool _regenerationExhausted;

    public BossDataResource? ConfiguredData => _configuredData;
    public BossPhaseResource? ActivePhase => _activePhase;
    public int ActivePhaseIndex => _activePhaseIndex;
    public bool IsRegenerating => _regenerationActive;
    public float RegeneratedHealth => _regeneratedHealth;
    public double TimeSinceLastPerfect => _timeSinceLastPerfect;

    public override void _Ready()
    {
        _enemy = GetParentOrNull<EnemyBase>()!;
        if (BossData != null)
        {
            _configuredData = BossData;
        }
    }

    public void Configure(BossDataResource bossData)
    {
        _configuredData = bossData;
        _activePhase = null;
        _activePhaseIndex = -1;
        ResetRegeneration();
    }

    public bool ActivatePhase(int phaseIndex)
    {
        var phase = _configuredData?.GetBossPhase(phaseIndex);
        if (phase == null || phase.RhythmProfile == null || _enemy == null)
        {
            return false;
        }

        _activePhaseIndex = phaseIndex;
        _activePhase = phase;
        ResetRegeneration();
        _enemy.ApplyBossPhase(phase);
        EmitSignal(SignalName.PhaseActivated, phaseIndex, phase.PhaseName);
        return true;
    }

    public bool TryGetNextPhaseIndex(float healthPercent, out int nextPhaseIndex)
    {
        nextPhaseIndex = -1;
        if (_configuredData == null || _activePhaseIndex < 0)
        {
            return false;
        }

        _configuredData.GetBossPhaseForHealthPercent(
            healthPercent,
            out var desiredPhaseIndex);
        if (desiredPhaseIndex <= _activePhaseIndex)
        {
            return false;
        }

        // Uma aplicação só pode avançar uma fase. O limite de dano abaixo
        // garante que a próxima chamada encontrará a fase seguinte.
        nextPhaseIndex = Math.Min(
            _activePhaseIndex + 1,
            desiredPhaseIndex);
        return nextPhaseIndex >= 0 && nextPhaseIndex < _configuredData.PhaseCount;
    }

    /// <summary>
    /// Impede que um golpe atravesse o próximo gate de HP. Assim cada fase é
    /// sempre ativada e nenhuma fase pode ser pulada por dano alto.
    /// </summary>
    public float ClampHealthDamage(float requestedDamage)
    {
        if (_enemy == null || requestedDamage <= 0.0f || _configuredData == null ||
            _activePhaseIndex < 0 || _activePhaseIndex >= _configuredData.PhaseCount - 1)
        {
            return Math.Max(0.0f, requestedDamage);
        }

        var nextPhaseStart = _configuredData.GetPhaseStartHealthPercent(
            _activePhaseIndex + 1);
        var protectedHealth = _enemy.MaxHealth * nextPhaseStart;
        var availableDamage = Mathf.Max(0.0f, _enemy.CurrentHealth - protectedHealth);
        return Mathf.Min(requestedDamage, availableDamage);
    }

    public void NotifyPerfect()
    {
        if (_activePhase == null || !_activePhase.EnableRegeneration)
        {
            return;
        }

        var wasRegenerating = _regenerationActive;
        ResetRegeneration();
        if (wasRegenerating)
        {
            EmitSignal(SignalName.RegenerationStopped);
        }
    }

    public void ProcessRegeneration(double delta, bool canCountTime)
    {
        if (_activePhase == null || !_activePhase.EnableRegeneration ||
            _enemy == null || _enemy.IsDead || !canCountTime ||
            _regenerationExhausted)
        {
            return;
        }

        if (_regenerationActive)
        {
            return;
        }

        _timeSinceLastPerfect += Math.Max(0.0d, delta);
        if (_timeSinceLastPerfect < Math.Max(0.0f, _activePhase.RegenerationDelaySeconds))
        {
            return;
        }

        _regenerationActive = true;
        EmitSignal(SignalName.RegenerationStarted);
    }

    public void ProcessBeat(bool canRegenerate)
    {
        if (_activePhase == null || !_activePhase.EnableRegeneration ||
            !_regenerationActive || !canRegenerate || _enemy == null || _enemy.IsDead)
        {
            return;
        }

        var maximumRegeneration = _enemy.MaxHealth * Mathf.Clamp(
            _activePhase.MaxRegeneratedPercent,
            0.0f,
            1.0f);
        var remainingRegeneration = Mathf.Max(
            0.0f,
            maximumRegeneration - _regeneratedHealth);
        var amount = Mathf.Min(
            remainingRegeneration,
            _enemy.MaxHealth * Mathf.Max(
                0.0f,
                _activePhase.RegenerationPercentPerBeat));
        if (amount <= 0.0f)
        {
            StopRegenerationAtCap();
            return;
        }

        _enemy.RestoreHealth(amount);
        _regeneratedHealth += amount;
        EmitSignal(SignalName.RegenerationTick, amount);
        if (_regeneratedHealth >= maximumRegeneration - 0.001f)
        {
            StopRegenerationAtCap();
        }
    }

    private void StopRegenerationAtCap()
    {
        if (!_regenerationActive)
        {
            return;
        }

        _regenerationActive = false;
        _regenerationExhausted = true;
        EmitSignal(SignalName.RegenerationStopped);
    }

    private void ResetRegeneration()
    {
        _timeSinceLastPerfect = 0.0d;
        _regeneratedHealth = 0.0f;
        _regenerationActive = false;
        _regenerationExhausted = false;
    }
}

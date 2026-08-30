using System;
using Godot;

/// <summary>
/// Base comportamental do inimigo. A cena fornece placeholders, barras, estado
/// visual, hitbox e timers; este script controla HP, postura e comunicação.
/// </summary>
public partial class EnemyBase : Node2D
{
    [Signal]
    public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

    [Signal]
    public delegate void PostureChangedEventHandler(float currentPosture, float maxPosture);

    [Signal]
    public delegate void PostureBrokenEventHandler();

    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void DefeatedEventHandler();

    [Export]
    public string EnemyName { get; set; } = "THE FOOL";

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxHealth { get; set; } = 60.0f;

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxPosture { get; set; } = 40.0f;

    private ProgressBar _healthBar = null!;
    private ProgressBar _postureBar = null!;
    private Label _nameLabel = null!;
    private Label _healthLabel = null!;
    private Label _postureLabel = null!;
    private Label _stateLabel = null!;
    private Label _armorLabel = null!;
    private Polygon2D _bodyPlaceholder = null!;
    private Polygon2D _corePlaceholder = null!;
    private Sprite2D _visualArt = null!;
    private Vector2 _visualArtBaseScale = Vector2.One;
    private Vector2 _basePosition;
    private Tween? _damageTween;
    private Tween? _entranceTween;
    private Timer _attackFeedbackTimer = null!;
    private Timer _staggerFeedbackTimer = null!;
    private float _currentHealth;
    private float _currentPosture;
    private EnemyState _enemyState = EnemyState.Normal;
    private EnemyDataResource? _enemyData;
    private bool _hasBossPhaseConfiguration;
    private bool _bossPhaseArmored;
    private float _bossPhaseArmorMultiplier = 1.0f;

    public float CurrentHealth => _currentHealth;
    public float CurrentPosture => _currentPosture;
    public EnemyState State => _enemyState;
    public bool IsDead => _enemyState == EnemyState.Dead;
    public bool IsStaggered => _enemyState == EnemyState.Staggered;
    public bool IsDefeated => IsDead;
    public EnemyDataResource? EnemyData => _enemyData;
    public bool IsArmored => _hasBossPhaseConfiguration
        ? _bossPhaseArmored
        : _enemyData?.Armored ?? false;
    public bool IsArmorActive => IsArmored && !IsDead && _currentPosture > 0.0f;
    public float ArmorHealthDamageMultiplier =>
        Mathf.Clamp(
            _hasBossPhaseConfiguration
                ? _bossPhaseArmorMultiplier
                : _enemyData?.HealthDamageMultiplierWhilePostureActive ?? 1.0f,
            0.0f,
            1.0f);

    public void Configure(EnemyDataResource enemyData)
    {
        _enemyData = enemyData;
        _hasBossPhaseConfiguration = false;
        _bossPhaseArmored = false;
        _bossPhaseArmorMultiplier = 1.0f;
        EnemyName = string.IsNullOrWhiteSpace(enemyData.EnemyName)
            ? EnemyName
            : enemyData.EnemyName;
        MaxHealth = Math.Max(1.0f, enemyData.MaxHealth);
        MaxPosture = Math.Max(1.0f, enemyData.MaxPosture);

        if (IsNodeReady())
        {
            _nameLabel.Text = EnemyName;
            RestoreFullHealth();
        }
    }

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("HealthBar");
        _postureBar = GetNode<ProgressBar>("PostureBar");
        _nameLabel = GetNode<Label>("NameLabel");
        _healthLabel = GetNode<Label>("HealthLabel");
        _postureLabel = GetNode<Label>("PostureLabel");
        _stateLabel = GetNode<Label>("StateLabel");
        _armorLabel = GetNode<Label>("ArmorLabel");
        _bodyPlaceholder = GetNode<Polygon2D>("BodyPlaceholder");
        _corePlaceholder = GetNode<Polygon2D>("CorePlaceholder");
        _visualArt = GetNode<Sprite2D>("VisualArt");
        _visualArtBaseScale = _visualArt.Scale;
        _basePosition = Position;
        _attackFeedbackTimer = GetNode<Timer>("AttackFeedbackTimer");
        _staggerFeedbackTimer = GetNode<Timer>("StaggerFeedbackTimer");
        _attackFeedbackTimer.Timeout += OnAttackFeedbackTimeout;
        _staggerFeedbackTimer.Timeout += OnStaggerFeedbackTimeout;

        _currentHealth = Math.Max(1.0f, MaxHealth);
        _currentPosture = Math.Max(1.0f, MaxPosture);
        _enemyState = EnemyState.Normal;
        _nameLabel.Text = EnemyName;
        UpdateVitalsVisuals();
        PlayEntranceFeedback();
    }

    public override void _ExitTree()
    {
        if (_attackFeedbackTimer != null)
        {
            _attackFeedbackTimer.Timeout -= OnAttackFeedbackTimeout;
        }

        if (_staggerFeedbackTimer != null)
        {
            _staggerFeedbackTimer.Timeout -= OnStaggerFeedbackTimeout;
        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0.0f)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth - damage, 0.0f, MaxHealth);
        UpdateVitalsVisuals();
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        PlayDamageReceivedFeedback(damage);

        if (_currentHealth <= 0.0f)
        {
            _enemyState = EnemyState.Dead;
            _staggerFeedbackTimer.Stop();
            UpdateStateVisuals();
            EmitSignal(SignalName.Died);
            EmitSignal(SignalName.Defeated);
        }
    }

    public void ApplyDamage(float damage)
    {
        TakeDamage(damage);
    }

    public void TakePostureDamage(float amount)
    {
        if (IsDead || IsStaggered || amount <= 0.0f)
        {
            return;
        }

        _currentPosture = Mathf.Clamp(
            _currentPosture - amount,
            0.0f,
            Math.Max(1.0f, MaxPosture));
        UpdatePostureVisuals();
        if (_postureBar is PercentageMeter resistanceMeter)
        {
            resistanceMeter.PulseDamage();
        }
        EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);

        if (_currentPosture <= 0.0f)
        {
            _enemyState = EnemyState.Staggered;
            UpdateStateVisuals();
            PlayStaggerFeedback();
            EmitSignal(SignalName.PostureBroken);
        }
    }

    public void RestorePosture()
    {
        if (IsDead)
        {
            return;
        }

        _enemyState = EnemyState.Normal;
        _currentPosture = Math.Max(1.0f, MaxPosture);
        ResetPlaceholderFeedback();
        UpdatePostureVisuals();
        UpdateStateVisuals();
        EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
    }

    /// <summary>
    /// Aplica os valores visuais e de resistência da fase atual sem alterar o
    /// HP do inimigo. O BossController chama este método durante a transição.
    /// </summary>
    public void ApplyBossPhase(BossPhaseResource phase)
    {
        if (phase == null || IsDead)
        {
            return;
        }

        _hasBossPhaseConfiguration = true;
        _bossPhaseArmored = phase.Armored;
        _bossPhaseArmorMultiplier = Mathf.Clamp(
            phase.ArmorHealthDamageMultiplier,
            0.0f,
            1.0f);
        MaxPosture = Math.Max(1.0f, phase.MaxPosture);
        _currentPosture = MaxPosture;
        _enemyState = EnemyState.Normal;
        ResetPlaceholderFeedback();
        UpdateVitalsVisuals();
        UpdateStateVisuals();
        EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
    }

    public void RestoreHealth(float amount)
    {
        if (IsDead || amount <= 0.0f || _currentHealth >= MaxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0.0f, MaxHealth);
        UpdateVitalsVisuals();
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
    }

    /// <summary>
    /// Utilizado apenas pelas opções de debug para iniciar uma fase já em sua
    /// faixa de HP, sem simular golpes artificiais.
    /// </summary>
    public void SetCurrentHealth(float health)
    {
        _currentHealth = Mathf.Clamp(health, 0.0f, MaxHealth);
        if (_currentHealth > 0.0f && _enemyState == EnemyState.Dead)
        {
            _enemyState = EnemyState.Normal;
        }

        ResetPlaceholderFeedback();
        UpdateVitalsVisuals();
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
    }

    public void RestoreFullHealth()
    {
        _enemyState = EnemyState.Normal;
        _currentHealth = Math.Max(1.0f, MaxHealth);
        _currentPosture = Math.Max(1.0f, MaxPosture);
        ResetPlaceholderFeedback();
        UpdateVitalsVisuals();
        UpdateStateVisuals();
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        EmitSignal(SignalName.PostureChanged, _currentPosture, MaxPosture);
    }

    public void PlayAttackFeedback()
    {
        if (IsDead || IsStaggered)
        {
            return;
        }

        FinishEntranceFeedback();
        _bodyPlaceholder.Modulate = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.75f, 0.75f, 1.0f);
        _visualArt.Modulate = new Color(1.0f, 0.82f, 0.82f, 1.0f);
        _visualArt.Scale = _visualArtBaseScale * 1.05f;
        Scale = Vector2.One * 1.05f;
        _attackFeedbackTimer.Start();
    }

    public void PlayEntranceFeedback()
    {
        _entranceTween?.Kill();
        Position = _basePosition + new Vector2(-26.0f, 8.0f);
        Scale = Vector2.One * 0.82f;
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

        _entranceTween = CreateTween().SetParallel(true);
        _entranceTween.SetEase(Tween.EaseType.Out);
        _entranceTween.SetTrans(Tween.TransitionType.Back);
        _entranceTween.TweenProperty(this, "position", _basePosition, 0.52d);
        _entranceTween.TweenProperty(this, "scale", Vector2.One, 0.52d);
        _entranceTween.SetTrans(Tween.TransitionType.Cubic);
        _entranceTween.TweenProperty(this, "modulate:a", 1.0f, 0.34d);
    }

    private void PlayDamageReceivedFeedback(float damage)
    {
        FinishEntranceFeedback();
        _damageTween?.Kill();
        _attackFeedbackTimer.Stop();

        var impactStrength = Mathf.Clamp(damage / Math.Max(1.0f, MaxHealth), 0.08f, 0.32f);
        _bodyPlaceholder.Modulate = new Color(1.0f, 0.32f, 0.52f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.88f, 0.95f, 1.0f);
        _visualArt.Modulate = new Color(1.0f, 0.58f, 0.72f, 1.0f);
        _visualArt.Scale = _visualArtBaseScale * (1.04f + impactStrength);
        Position = _basePosition + new Vector2(12.0f + impactStrength * 22.0f, -2.0f);
        Rotation = 0.025f + impactStrength * 0.08f;
        Scale = new Vector2(1.06f, 0.94f);

        _damageTween = CreateTween().SetParallel(true);
        _damageTween.SetEase(Tween.EaseType.Out);
        _damageTween.SetTrans(Tween.TransitionType.Back);
        _damageTween.TweenProperty(this, "position", _basePosition, 0.28d);
        _damageTween.TweenProperty(this, "rotation", 0.0f, 0.25d);
        _damageTween.TweenProperty(this, "scale", Vector2.One, 0.28d);

        if (_healthBar is PercentageMeter healthMeter)
        {
            healthMeter.PulseDamage();
        }

        _attackFeedbackTimer.Start(0.3d);
    }

    public void PlayStaggerFeedback()
    {
        if (IsDead)
        {
            return;
        }

        FinishEntranceFeedback();
        _bodyPlaceholder.Modulate = new Color(0.7f, 0.36f, 1.0f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.82f, 0.3f, 1.0f);
        _visualArt.Modulate = new Color(0.82f, 0.7f, 1.0f, 1.0f);
        _visualArt.Scale = _visualArtBaseScale * 1.1f;
        Scale = Vector2.One * 1.1f;
        _staggerFeedbackTimer.Start();
    }

    public void PlayExecutionFeedback()
    {
        if (IsDead)
        {
            return;
        }

        FinishEntranceFeedback();
        _bodyPlaceholder.Modulate = new Color(1.0f, 0.78f, 0.3f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.95f, 0.7f, 1.0f);
        _visualArt.Modulate = new Color(1.0f, 0.9f, 0.58f, 1.0f);
        _visualArt.Scale = _visualArtBaseScale * 1.14f;
        Scale = Vector2.One * 1.14f;
        _staggerFeedbackTimer.Start();
    }

    private void UpdateVitalsVisuals()
    {
        if (_healthBar == null || _healthLabel == null || _postureBar == null ||
            _postureLabel == null)
        {
            return;
        }

        _healthBar.MaxValue = MaxHealth;
        _healthBar.Value = _currentHealth;
        _healthLabel.Text = $"HP: {_currentHealth:0}/{MaxHealth:0}";
        UpdatePostureVisuals();
        UpdateStateVisuals();
    }

    private void UpdatePostureVisuals()
    {
        if (_postureBar == null || _postureLabel == null)
        {
            return;
        }

        _postureBar.MaxValue = MaxPosture;
        _postureBar.Value = _currentPosture;
        _postureLabel.Text = $"RESISTÊNCIA: {_currentPosture:0}/{MaxPosture:0}";
        UpdateArmorVisuals();
    }

    private void UpdateArmorVisuals()
    {
        if (_armorLabel == null)
        {
            return;
        }

        _armorLabel.Visible = IsArmorActive;
        _armorLabel.Text = "ARMORED";
    }

    private void UpdateStateVisuals()
    {
        if (_stateLabel == null)
        {
            return;
        }

        _stateLabel.Text = _enemyState switch
        {
            EnemyState.Staggered => "STAGGERED",
            EnemyState.Dead => "DEAD",
            _ => "NORMAL",
        };
        _stateLabel.Modulate = _enemyState switch
        {
            EnemyState.Staggered => new Color(1.0f, 0.78f, 0.3f, 1.0f),
            EnemyState.Dead => new Color(1.0f, 0.38f, 0.38f, 1.0f),
            _ => new Color(0.72f, 0.78f, 0.9f, 1.0f),
        };
    }

    private void OnAttackFeedbackTimeout()
    {
        if (!IsStaggered)
        {
            ResetPlaceholderFeedback();
        }
    }

    private void OnStaggerFeedbackTimeout()
    {
        ResetPlaceholderFeedback();
    }

    private void ResetPlaceholderFeedback()
    {
        _bodyPlaceholder.Modulate = Colors.White;
        _corePlaceholder.Modulate = Colors.White;
        _visualArt.Modulate = Colors.White;
        _visualArt.Scale = _visualArtBaseScale;
        Position = _basePosition;
        Rotation = 0.0f;
        Scale = Vector2.One;
    }

    private void FinishEntranceFeedback()
    {
        if (_entranceTween == null || !_entranceTween.IsValid())
        {
            return;
        }

        _entranceTween.Kill();
        _entranceTween = null;
        Modulate = Colors.White;
        Position = _basePosition;
        Scale = Vector2.One;
    }
}

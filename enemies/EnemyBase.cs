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
    public string EnemyName { get; set; } = "THE INITIATE";

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
    private Polygon2D _bodyPlaceholder = null!;
    private Polygon2D _corePlaceholder = null!;
    private Timer _attackFeedbackTimer = null!;
    private Timer _staggerFeedbackTimer = null!;
    private float _currentHealth;
    private float _currentPosture;
    private EnemyState _enemyState = EnemyState.Normal;

    public float CurrentHealth => _currentHealth;
    public float CurrentPosture => _currentPosture;
    public EnemyState State => _enemyState;
    public bool IsDead => _enemyState == EnemyState.Dead;
    public bool IsStaggered => _enemyState == EnemyState.Staggered;
    public bool IsDefeated => IsDead;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("HealthBar");
        _postureBar = GetNode<ProgressBar>("PostureBar");
        _nameLabel = GetNode<Label>("NameLabel");
        _healthLabel = GetNode<Label>("HealthLabel");
        _postureLabel = GetNode<Label>("PostureLabel");
        _stateLabel = GetNode<Label>("StateLabel");
        _bodyPlaceholder = GetNode<Polygon2D>("BodyPlaceholder");
        _corePlaceholder = GetNode<Polygon2D>("CorePlaceholder");
        _attackFeedbackTimer = GetNode<Timer>("AttackFeedbackTimer");
        _staggerFeedbackTimer = GetNode<Timer>("StaggerFeedbackTimer");
        _attackFeedbackTimer.Timeout += OnAttackFeedbackTimeout;
        _staggerFeedbackTimer.Timeout += OnStaggerFeedbackTimeout;

        _currentHealth = Math.Max(1.0f, MaxHealth);
        _currentPosture = Math.Max(1.0f, MaxPosture);
        _enemyState = EnemyState.Normal;
        _nameLabel.Text = EnemyName;
        UpdateVitalsVisuals();
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

        if (_currentHealth <= 0.0f)
        {
            _enemyState = EnemyState.Dead;
            _staggerFeedbackTimer.Stop();
            ResetPlaceholderFeedback();
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

        _bodyPlaceholder.Modulate = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.75f, 0.75f, 1.0f);
        Scale = Vector2.One * 1.05f;
        _attackFeedbackTimer.Start();
    }

    public void PlayStaggerFeedback()
    {
        if (IsDead)
        {
            return;
        }

        _bodyPlaceholder.Modulate = new Color(0.7f, 0.36f, 1.0f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.82f, 0.3f, 1.0f);
        Scale = Vector2.One * 1.1f;
        _staggerFeedbackTimer.Start();
    }

    public void PlayExecutionFeedback()
    {
        if (IsDead)
        {
            return;
        }

        _bodyPlaceholder.Modulate = new Color(1.0f, 0.78f, 0.3f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.95f, 0.7f, 1.0f);
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
        _postureLabel.Text = $"POSTURE: {_currentPosture:0}/{MaxPosture:0}";
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
        Scale = Vector2.One;
    }
}

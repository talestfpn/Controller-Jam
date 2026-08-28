using System;
using Godot;

/// <summary>
/// Estado e comportamento básico do jogador. A cena fornece os placeholders,
/// hitbox e timer de feedback; este script não cria a árvore visual.
/// </summary>
public partial class PlayerBase : Node2D
{
    [Signal]
    public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

    [Signal]
    public delegate void DiedEventHandler();

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxHealth { get; set; } = 100.0f;

    private Polygon2D _bodyPlaceholder = null!;
    private Polygon2D _corePlaceholder = null!;
    private Timer _damageFlashTimer = null!;
    private float _currentHealth;
    private bool _isDead;

    public float CurrentHealth => _currentHealth;
    public bool IsDead => _isDead;

    public override void _Ready()
    {
        _bodyPlaceholder = GetNode<Polygon2D>("BodyPlaceholder");
        _corePlaceholder = GetNode<Polygon2D>("CorePlaceholder");
        _damageFlashTimer = GetNode<Timer>("DamageFlashTimer");
        _damageFlashTimer.Timeout += OnDamageFlashTimeout;

        ResetHealth();
    }

    public override void _ExitTree()
    {
        if (_damageFlashTimer != null)
        {
            _damageFlashTimer.Timeout -= OnDamageFlashTimeout;
        }
    }

    public void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0.0f)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth - amount, 0.0f, MaxHealth);
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
        PlayDamageFeedback();

        if (_currentHealth <= 0.0f)
        {
            _isDead = true;
            EmitSignal(SignalName.Died);
        }
    }

    public void Heal(float amount)
    {
        if (_isDead || amount <= 0.0f)
        {
            return;
        }

        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0.0f, MaxHealth);
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
    }

    public void ResetHealth()
    {
        _isDead = false;
        _currentHealth = Math.Max(1.0f, MaxHealth);
        ResetDamageFeedback();
        EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
    }

    private void PlayDamageFeedback()
    {
        _bodyPlaceholder.Modulate = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        _corePlaceholder.Modulate = new Color(1.0f, 0.75f, 0.75f, 1.0f);
        Scale = Vector2.One * 1.06f;
        _damageFlashTimer.Start();
    }

    private void ResetDamageFeedback()
    {
        _bodyPlaceholder.Modulate = Colors.White;
        _corePlaceholder.Modulate = Colors.White;
        Scale = Vector2.One;
    }

    private void OnDamageFlashTimeout()
    {
        ResetDamageFeedback();
    }
}

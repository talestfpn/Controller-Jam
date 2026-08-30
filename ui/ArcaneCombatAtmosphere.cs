using System;
using Godot;

/// <summary>
/// Controla somente a resposta visual da atmosfera arcana configurada na cena.
/// Não calcula dano, timing, HP, Resistência ou combo.
/// </summary>
public partial class ArcaneCombatAtmosphere : Control
{
    private ShaderMaterial _material = null!;
    private RhythmManager? _rhythmManager;
    private float _healthRatio = 1.0f;
    private float _resistanceRatio = 1.0f;
    private float _telegraphStrength;
    private float _telegraphTarget;
    private float _telegraphProgress;
    private float _telegraphSpeed = 1.5f;
    private float _hitProgress = -1.0f;
    private float _hitStrength;
    private float _staggerFlash;

    public override void _Ready()
    {
        var visualField = GetNode<ColorRect>("VisualField");
        _material = visualField.Material as ShaderMaterial ??
            throw new InvalidOperationException(
                $"{visualField.GetPath()} precisa de ShaderMaterial configurado na cena.");
        _rhythmManager = GetNodeOrNull<RhythmManager>("/root/RhythmManager");
        PushStateToMaterial();
    }

    public override void _Process(double delta)
    {
        var deltaSeconds = (float)delta;
        _telegraphStrength = Mathf.MoveToward(
            _telegraphStrength,
            _telegraphTarget,
            deltaSeconds * (_telegraphTarget > _telegraphStrength ? 4.5f : 2.8f));

        if (_telegraphTarget > 0.0f)
        {
            _telegraphProgress = Mathf.Clamp(
                _telegraphProgress + deltaSeconds * _telegraphSpeed,
                0.0f,
                1.0f);
        }

        if (_hitProgress >= 0.0f)
        {
            _hitProgress += deltaSeconds * 1.85f;
            if (_hitProgress > 1.15f)
            {
                _hitProgress = -1.0f;
            }
        }

        _hitStrength = Mathf.MoveToward(_hitStrength, 0.0f, deltaSeconds * 1.15f);
        _staggerFlash = Mathf.MoveToward(_staggerFlash, 0.0f, deltaSeconds * 1.8f);
        PushStateToMaterial();
    }

    public void ConfigureEnemy(string enemyName)
    {
        var normalizedName = (enemyName ?? string.Empty).Trim().ToUpperInvariant();
        var seed = GetStableSeed(normalizedName);
        var style = normalizedName switch
        {
            "THE FOOL" => 0.0f,
            "THE TOWER" => 1.0f,
            "JUSTICE" => 2.0f,
            "KNIGHT OF SWORDS" => 3.0f,
            "THE MAGICIAN" => 4.0f,
            "KNIGHT OF PENTACLES" => 5.0f,
            "KING OF WANDS" => 6.0f,
            "THE RESISTANCE" => 7.0f,
            _ => Mathf.Floor(seed * 7.0f),
        };
        var palette = GetEnemyPalette(normalizedName);
        _material.SetShaderParameter("enemy_seed", seed);
        _material.SetShaderParameter("ornament_style", style);
        _material.SetShaderParameter("enemy_primary", palette.Primary);
        _material.SetShaderParameter("enemy_secondary", palette.Secondary);
        _telegraphTarget = 0.0f;
        _telegraphStrength = 0.0f;
        _hitProgress = -1.0f;
    }

    public void SetEnemyHealth(float currentHealth, float maxHealth)
    {
        _healthRatio = maxHealth > 0.0f
            ? Mathf.Clamp(currentHealth / maxHealth, 0.0f, 1.0f)
            : 0.0f;
    }

    public void SetEnemyResistance(float currentResistance, float maxResistance)
    {
        _resistanceRatio = maxResistance > 0.0f
            ? Mathf.Clamp(currentResistance / maxResistance, 0.0f, 1.0f)
            : 0.0f;
    }

    public void SetComboState(int combo, int tier, int perfectStreak)
    {
        var safeTier = Mathf.Clamp(tier, 0, 3);
        var energy = safeTier switch
        {
            1 => 0.38f,
            2 => 0.66f,
            3 => 1.0f,
            _ => combo > 0 ? 0.2f : 0.06f,
        };
        energy = Mathf.Clamp(energy + perfectStreak * 0.018f, 0.0f, 1.0f);
        _material.SetShaderParameter("combo_energy", energy);
        _material.SetShaderParameter("combo_tier", (float)safeTier);
        var palette = GetComboPalette(safeTier);
        _material.SetShaderParameter("combo_primary", palette.Primary);
        _material.SetShaderParameter("combo_secondary", palette.Secondary);
    }

    public void PlayEnemyTelegraph(float durationSeconds)
    {
        _telegraphProgress = 0.0f;
        _telegraphTarget = 1.0f;
        _telegraphSpeed = 1.0f / Math.Max(0.2f, durationSeconds);
    }

    public void StopEnemyTelegraph()
    {
        _telegraphTarget = 0.0f;
    }

    public void PlayPlayerImpact(
        float healthDamage,
        float resistanceDamage,
        TimingResult timingResult)
    {
        if (healthDamage <= 0.0f && resistanceDamage <= 0.0f)
        {
            return;
        }

        _hitProgress = 0.0f;
        _hitStrength = timingResult == TimingResult.Perfect ? 1.0f : 0.72f;
        _material.SetShaderParameter("impact_color", GetImpactColor(timingResult));
    }

    public void PlayPostureBreak()
    {
        _staggerFlash = 1.0f;
        _hitProgress = 0.68f;
        _hitStrength = 1.0f;
        _material.SetShaderParameter(
            "impact_color",
            new Color(0.82f, 0.42f, 1.0f, 1.0f));
    }

    private void PushStateToMaterial()
    {
        _material.SetShaderParameter(
            "beat_phase",
            (float)(_rhythmManager?.BeatPhase ?? 0.0d));
        _material.SetShaderParameter("health_ratio", _healthRatio);
        _material.SetShaderParameter("resistance_ratio", _resistanceRatio);
        _material.SetShaderParameter("telegraph_strength", _telegraphStrength);
        _material.SetShaderParameter("telegraph_progress", _telegraphProgress);
        _material.SetShaderParameter("hit_progress", _hitProgress);
        _material.SetShaderParameter("hit_strength", _hitStrength);
        _material.SetShaderParameter("stagger_flash", _staggerFlash);
    }

    private static float GetStableSeed(string text)
    {
        uint hash = 2166136261;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 16777619;
        }

        return (hash % 10000u) / 9999.0f;
    }

    private static (Color Primary, Color Secondary) GetEnemyPalette(string enemyName)
    {
        return enemyName switch
        {
            "THE FOOL" => (
                new Color(0.72f, 0.24f, 1.0f, 1.0f),
                new Color(1.0f, 0.72f, 0.18f, 1.0f)),
            "THE TOWER" => (
                new Color(1.0f, 0.2f, 0.28f, 1.0f),
                new Color(1.0f, 0.58f, 0.12f, 1.0f)),
            "JUSTICE" => (
                new Color(0.52f, 0.38f, 1.0f, 1.0f),
                new Color(0.28f, 0.86f, 1.0f, 1.0f)),
            "KNIGHT OF SWORDS" => (
                new Color(0.22f, 0.66f, 1.0f, 1.0f),
                new Color(0.82f, 0.92f, 1.0f, 1.0f)),
            "THE MAGICIAN" => (
                new Color(1.0f, 0.2f, 0.78f, 1.0f),
                new Color(0.54f, 0.28f, 1.0f, 1.0f)),
            "KNIGHT OF PENTACLES" => (
                new Color(0.34f, 0.92f, 0.48f, 1.0f),
                new Color(1.0f, 0.76f, 0.18f, 1.0f)),
            "KING OF WANDS" => (
                new Color(1.0f, 0.26f, 0.14f, 1.0f),
                new Color(1.0f, 0.78f, 0.16f, 1.0f)),
            "THE RESISTANCE" => (
                new Color(0.84f, 0.42f, 1.0f, 1.0f),
                new Color(1.0f, 0.88f, 0.38f, 1.0f)),
            _ => (
                new Color(0.72f, 0.28f, 1.0f, 1.0f),
                new Color(0.3f, 0.8f, 1.0f, 1.0f)),
        };
    }

    private static (Color Primary, Color Secondary) GetComboPalette(int tier)
    {
        return tier switch
        {
            1 => (
                new Color(0.2f, 0.82f, 1.0f, 1.0f),
                new Color(0.62f, 0.28f, 1.0f, 1.0f)),
            2 => (
                new Color(1.0f, 0.3f, 0.78f, 1.0f),
                new Color(1.0f, 0.72f, 0.16f, 1.0f)),
            3 => (
                new Color(0.62f, 1.0f, 0.22f, 1.0f),
                new Color(1.0f, 0.16f, 0.78f, 1.0f)),
            _ => (
                new Color(0.34f, 0.62f, 1.0f, 1.0f),
                new Color(0.7f, 0.22f, 1.0f, 1.0f)),
        };
    }

    private static Color GetImpactColor(TimingResult timingResult)
    {
        return timingResult switch
        {
            TimingResult.Perfect => new Color(0.55f, 1.0f, 0.24f, 1.0f),
            TimingResult.Good => new Color(0.25f, 0.78f, 1.0f, 1.0f),
            TimingResult.Ok => new Color(1.0f, 0.68f, 0.16f, 1.0f),
            _ => new Color(1.0f, 0.22f, 0.28f, 1.0f),
        };
    }
}

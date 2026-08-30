using Godot;

/// <summary>
/// Configuração de uma fase do boss. A fase só descreve dados: faixa de HP,
/// identidade rítmica, dano, resistência e regras especiais.
/// </summary>
[GlobalClass]
public partial class BossPhaseResource : Resource
{
    [Export(PropertyHint.Range, "0.0,1.0,0.001")]
    public float MinHealthPercent { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.001")]
    public float MaxHealthPercent { get; set; } = 1.0f;

    [Export]
    public string PhaseName { get; set; } = "BOSS PHASE";

    [Export]
    public string ThemeLabel { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string SpecialRules { get; set; } = string.Empty;

    [Export]
    public EnemyRhythmProfileResource RhythmProfile { get; set; } = null!;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float AttackDamage { get; set; } = 20.0f;

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxPosture { get; set; } = 40.0f;

    [Export]
    public bool Armored { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float ArmorHealthDamageMultiplier { get; set; } = 1.0f;

    [Export]
    public bool EnableRegeneration { get; set; }

    [Export(PropertyHint.Range, "0.0,30.0,0.1")]
    public float RegenerationDelaySeconds { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "0.0,0.1,0.001")]
    public float RegenerationPercentPerBeat { get; set; } = 0.005f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MaxRegeneratedPercent { get; set; } = 0.05f;

    public float GetMinimumHealthPercent()
    {
        return Mathf.Clamp(Mathf.Min(MinHealthPercent, MaxHealthPercent), 0.0f, 1.0f);
    }

    public float GetMaximumHealthPercent()
    {
        return Mathf.Clamp(Mathf.Max(MinHealthPercent, MaxHealthPercent), 0.0f, 1.0f);
    }

    public bool ContainsHealthPercent(float healthPercent)
    {
        var normalizedHealth = Mathf.Clamp(healthPercent, 0.0f, 1.0f);
        return normalizedHealth >= GetMinimumHealthPercent() - 0.00001f &&
            normalizedHealth <= GetMaximumHealthPercent() + 0.00001f;
    }
}

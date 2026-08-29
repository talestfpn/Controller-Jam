using Godot;

/// <summary>
/// Faixa de HP que seleciona um RhythmProfile. Mantém a progressão de fases em
/// dados e permite que outros inimigos futuros reutilizem a mesma estrutura.
/// </summary>
[GlobalClass]
public partial class EnemyRhythmPhaseResource : Resource
{
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MinHealthPercent { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MaxHealthPercent { get; set; } = 1.0f;

    [Export]
    public string PhaseName { get; set; } = "PHASE";

    [Export]
    public EnemyRhythmProfileResource RhythmProfile { get; set; } = null!;

    public bool ContainsHealthPercent(float healthPercent)
    {
        var normalizedHealth = Mathf.Clamp(healthPercent, 0.0f, 1.0f);
        var minimum = Mathf.Min(MinHealthPercent, MaxHealthPercent);
        var maximum = Mathf.Max(MinHealthPercent, MaxHealthPercent);
        return normalizedHealth >= minimum - 0.0001f &&
            normalizedHealth <= maximum + 0.0001f;
    }
}

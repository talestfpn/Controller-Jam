using Godot;

/// <summary>
/// Dados de gameplay e referências configuráveis de um inimigo.
/// </summary>
[GlobalClass]
public partial class EnemyDataResource : Resource
{
    [Export]
    public string EnemyName { get; set; } = "ENEMY";

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxHealth { get; set; } = 60.0f;

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float MaxPosture { get; set; } = 40.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float BaseAttackDamage { get; set; } = 20.0f;

    [Export]
    public bool Armored { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float HealthDamageMultiplierWhilePostureActive { get; set; } = 1.0f;

    [Export]
    public PackedScene EnemyScene { get; set; } = null!;

    [Export]
    public EnemyRhythmProfileResource RhythmProfile { get; set; } = null!;

    [Export]
    public Godot.Collections.Array<EnemyRhythmPhaseResource> RhythmPhases { get; set; } = new();

    public bool HasRhythmPhases => RhythmPhases != null && RhythmPhases.Count > 0;

    public EnemyRhythmProfileResource? GetRhythmProfileForHealthPercent(
        float healthPercent,
        out int phaseIndex)
    {
        phaseIndex = -1;
        if (HasRhythmPhases)
        {
            for (var index = 0; index < RhythmPhases.Count; index++)
            {
                var phase = RhythmPhases[index];
                if (phase == null ||
                    phase.RhythmProfile == null ||
                    !phase.ContainsHealthPercent(healthPercent))
                {
                    continue;
                }

                phaseIndex = index;
                return phase.RhythmProfile;
            }
        }

        return RhythmProfile;
    }

    public EnemyRhythmPhaseResource? GetRhythmPhase(int phaseIndex)
    {
        if (!HasRhythmPhases || phaseIndex < 0 || phaseIndex >= RhythmPhases.Count)
        {
            return null;
        }

        return RhythmPhases[phaseIndex];
    }
}

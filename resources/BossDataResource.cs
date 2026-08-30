using Godot;

/// <summary>
/// Dados do boss final e sua sequência de sete fases.
/// </summary>
[GlobalClass]
public partial class BossDataResource : EnemyDataResource
{
    [Export]
    public Godot.Collections.Array<BossPhaseResource> BossPhases { get; set; } = new();

    [Export(PropertyHint.Range, "0.25,3.0,0.05")]
    public float PhaseTransitionDuration { get; set; } = 1.0f;

    public int PhaseCount => BossPhases?.Count ?? 0;

    public BossPhaseResource? GetBossPhase(int phaseIndex)
    {
        if (BossPhases == null || phaseIndex < 0 || phaseIndex >= BossPhases.Count)
        {
            return null;
        }

        return BossPhases[phaseIndex];
    }

    /// <summary>
    /// Procura de baixo para cima para que a fronteira exata entre duas fases
    /// pertença à fase mais avançada. Isso evita uma fase fantasma na troca.
    /// </summary>
    public BossPhaseResource? GetBossPhaseForHealthPercent(
        float healthPercent,
        out int phaseIndex)
    {
        phaseIndex = -1;
        if (BossPhases == null || BossPhases.Count == 0)
        {
            return null;
        }

        var normalizedHealth = Mathf.Clamp(healthPercent, 0.0f, 1.0f);
        for (var index = BossPhases.Count - 1; index >= 0; index--)
        {
            var phase = BossPhases[index];
            if (phase == null || !phase.ContainsHealthPercent(normalizedHealth))
            {
                continue;
            }

            phaseIndex = index;
            return phase;
        }

        return null;
    }

    public float GetPhaseStartHealthPercent(int phaseIndex)
    {
        var phase = GetBossPhase(phaseIndex);
        return phase == null ? 0.0f : phase.GetMaximumHealthPercent();
    }
}

using System;
using Godot;

/// <summary>
/// Limites globais de legibilidade para qualquer encounter rítmico. Perfis podem
/// ser mais lentos ou mais espaçados, mas o gerador nunca deve produzir um
/// prompt abaixo destes limites.
/// </summary>
[GlobalClass]
public partial class RhythmFairnessConfigResource : Resource
{
    [Export(PropertyHint.Range, "1.0,4.0,0.25")]
    public float MinimumLogicalPromptSpacingBeats { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "1.0,4.0,0.25")]
    public float MinimumTelegraphBeats { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "1,3,1")]
    public int MaximumAllowedBurstLength { get; set; } = 3;

    [Export(PropertyHint.Range, "1.0,4.0,0.25")]
    public float MinimumBurstSpacingBeats { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "1,1,1")]
    public int MaximumActiveLogicalPrompts { get; set; } = 1;

    [Export(PropertyHint.Range, "0,1,1")]
    public int MaximumDecoysPerPrompt { get; set; } = 1;

    /// <summary>
    /// Com um único prompt visual/lógico ativo, o alvo seguinte precisa caber
    /// depois do telegraph para não aparecer atrasado após um MISS.
    /// </summary>
    public float GetEffectiveMinimumTargetSpacingBeats()
    {
        return Math.Max(
            Math.Max(0.5f, MinimumLogicalPromptSpacingBeats),
            Math.Max(0.5f, MinimumTelegraphBeats));
    }

    public int GetEffectiveMaximumBurstLength()
    {
        return Math.Clamp(MaximumAllowedBurstLength, 2, 3);
    }
}

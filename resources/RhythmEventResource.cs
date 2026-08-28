using System;
using Godot;

/// <summary>
/// Um evento de um padrão rítmico. O Resource mantém os dados do padrão fora
/// do controller, permitindo ajustar o combate pelo Inspector.
/// </summary>
[GlobalClass]
public partial class RhythmEventResource : Resource
{
    [Export(PropertyHint.Range, "0.0,4096.0,0.25")]
    public float BeatOffset { get; set; } = 1.0f;

    [Export(PropertyHint.Enum, "PlayerAttack,EnemyAttack,Execution")]
    public int PromptType { get; set; } = (int)RhythmPromptType.PlayerAttack;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float Damage { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PostureDamage { get; set; }

    public RhythmPromptType GetPromptType()
    {
        return Enum.IsDefined(typeof(RhythmPromptType), PromptType)
            ? (RhythmPromptType)PromptType
            : RhythmPromptType.PlayerAttack;
    }
}

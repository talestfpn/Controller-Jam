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

    [Export(PropertyHint.Enum, "Normal,Feint,FadeBeforeTarget,Decoy")]
    public int PromptBehavior { get; set; } = (int)RhythmPromptBehavior.Normal;

    [Export(PropertyHint.Range, "0,9999,1")]
    public int ChainId { get; set; }

    [Export(PropertyHint.Range, "0,4,1")]
    public int ChainIndex { get; set; }

    [Export(PropertyHint.Range, "1,4,1")]
    public int ChainLength { get; set; } = 1;

    [Export(PropertyHint.Range, "0.0,4096.0,0.25")]
    public float SourceBeatOffset { get; set; }

    public RhythmPromptType GetPromptType()
    {
        return Enum.IsDefined(typeof(RhythmPromptType), PromptType)
            ? (RhythmPromptType)PromptType
            : RhythmPromptType.PlayerAttack;
    }

    public RhythmPromptBehavior GetPromptBehavior()
    {
        return Enum.IsDefined(typeof(RhythmPromptBehavior), PromptBehavior)
            ? (RhythmPromptBehavior)PromptBehavior
            : RhythmPromptBehavior.Normal;
    }
}

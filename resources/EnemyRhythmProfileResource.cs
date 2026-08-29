using Godot;

/// <summary>
/// Identidade rítmica de um inimigo. Estes dados orientam a transformação do
/// chart musical-base sem colocar decisões específicas no CombatController.
/// </summary>
[GlobalClass]
public partial class EnemyRhythmProfileResource : Resource
{
    [Export]
    public string ProfileName { get; set; } = "Enemy Rhythm Profile";

    [Export(PropertyHint.Range, "1,10,1")]
    public int DifficultyLevel { get; set; } = 1;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float EventDensity { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float PlayerAttackWeight { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "0.0,10.0,0.1")]
    public float EnemyAttackWeight { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0.5,16.0,0.5")]
    public float MinimumSpacingBeats { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0.5,16.0,0.5")]
    public float PreferredSpacingBeats { get; set; } = 4.0f;

    [Export]
    public bool AllowHalfBeats { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float HalfBeatChance { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float BurstChance { get; set; }

    [Export(PropertyHint.Range, "0,3,1")]
    public int MinBurstLength { get; set; } = 2;

    [Export(PropertyHint.Range, "0,3,1")]
    public int MaxBurstLength { get; set; } = 3;

    [Export(PropertyHint.Range, "1.0,2.0,0.25")]
    public float BurstSpacingBeats { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float AlternationChance { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float DefenseChainChance { get; set; } = 0.10f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float BaseActionBias { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float StrongBeatPreference { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float HeavyEventPreference { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float FeintChance { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float VisualFadeChance { get; set; }

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float DecoyChance { get; set; }

    [Export(PropertyHint.Range, "0.1,1.0,0.05")]
    public float FadeBeatsBeforeTarget { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.05,0.6,0.01")]
    public float FeintStrength { get; set; } = 0.24f;

    [Export(PropertyHint.Range, "0.5,3.0,0.05")]
    public float TelegraphBeats { get; set; } = 1.0f;

}

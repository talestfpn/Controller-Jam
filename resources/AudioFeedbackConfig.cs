using Godot;

/// <summary>
/// Slots de áudio substituíveis pelo Inspector. Quando um slot está vazio,
/// o AudioManager usa seu fallback procedural temporário.
/// </summary>
[GlobalClass]
public partial class AudioFeedbackConfig : Resource
{
    [Export] public AudioStream PerfectAttackSfx { get; set; } = null!;
    [Export] public AudioStream GoodAttackSfx { get; set; } = null!;
    [Export] public AudioStream OkAttackSfx { get; set; } = null!;
    [Export] public AudioStream MissSfx { get; set; } = null!;

    [Export] public AudioStream ParrySfx { get; set; } = null!;
    [Export] public AudioStream BlockSfx { get; set; } = null!;
    [Export] public AudioStream PartialBlockSfx { get; set; } = null!;
    [Export] public AudioStream PlayerHitSfx { get; set; } = null!;

    [Export] public AudioStream PostureBreakSfx { get; set; } = null!;
    [Export] public AudioStream ExecutionReadySfx { get; set; } = null!;
    [Export] public AudioStream ExecutionSuccessSfx { get; set; } = null!;
    [Export] public AudioStream ExecutionFailSfx { get; set; } = null!;

    [Export] public AudioStream ComboMilestoneSfx { get; set; } = null!;
    [Export] public AudioStream ComboBreakSfx { get; set; } = null!;
    [Export] public AudioStream PerfectStreakSfx { get; set; } = null!;
    [Export] public AudioStream BossRegenerationSfx { get; set; } = null!;
    [Export] public AudioStream BossPhaseTransitionSfx { get; set; } = null!;
    [Export] public AudioStream MetronomeSfx { get; set; } = null!;
}

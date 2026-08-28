using Godot;

/// <summary>
/// Dados de uma faixa e da timeline musical. O stream pode permanecer nulo
/// durante o protótipo, fazendo o RhythmManager usar o relógio monotônico.
/// </summary>
[GlobalClass]
public partial class MusicTrackResource : Resource
{
    [Export]
    public AudioStream AudioStream { get; set; } = null!;

    [Export(PropertyHint.Range, "20.0,300.0,1.0")]
    public float Bpm { get; set; } = 120.0f;

    [Export(PropertyHint.Range, "-5000.0,5000.0,1.0")]
    public float MusicOffsetMs { get; set; }

    [Export(PropertyHint.Range, "1,16,1")]
    public int TimeSignatureNumerator { get; set; } = 4;

    [Export(PropertyHint.Range, "1,16,1")]
    public int TimeSignatureDenominator { get; set; } = 4;

    [Export]
    public bool LoopEnabled { get; set; } = true;

    [Export]
    public string TrackName { get; set; } = "Prototype Battle";
}

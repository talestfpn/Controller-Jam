using System;
using Godot;

/// <summary>
/// Alimenta o material visual do fundo com o relógio musical central.
/// A estrutura e o ShaderMaterial permanecem configurados na cena.
/// </summary>
public partial class BeatSyncedBackground : ColorRect
{
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float BeatPulseStrength { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "1.0,16.0,0.1")]
    public float BeatPulseDecay { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MeasurePulseStrength { get; set; } = 0.12f;

    private RhythmManager _rhythmManager = null!;
    private ShaderMaterial _backgroundMaterial = null!;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _backgroundMaterial = Material as ShaderMaterial ??
            throw new InvalidOperationException(
                $"{GetPath()} precisa possuir ShaderMaterial configurado na cena.");
    }

    public override void _Process(double delta)
    {
        var musicPosition = _rhythmManager.GetMusicPosition();
        if (!double.IsFinite(musicPosition) || musicPosition < 0.0d)
        {
            return;
        }

        var beatDuration = Math.Max(_rhythmManager.BeatDuration, 0.0001d);
        var rhythmTime = musicPosition / beatDuration;
        var currentBeat = Math.Max(0, (int)Math.Floor(rhythmTime));
        var beatPhase = rhythmTime - Math.Floor(rhythmTime);
        var beatPulse = Math.Exp(-BeatPulseDecay * beatPhase) * BeatPulseStrength;

        var beatsPerMeasure = Math.Max(1, _rhythmManager.BeatsPerMeasure);
        var isMeasureStart = currentBeat % beatsPerMeasure == 0;
        var measurePulse = isMeasureStart
            ? Math.Exp(-BeatPulseDecay * 0.72d * beatPhase) * MeasurePulseStrength
            : 0.0d;

        _backgroundMaterial.SetShaderParameter("rhythm_time", (float)rhythmTime);
        _backgroundMaterial.SetShaderParameter("beat_phase", (float)beatPhase);
        _backgroundMaterial.SetShaderParameter("beat_pulse", (float)beatPulse);
        _backgroundMaterial.SetShaderParameter("measure_pulse", (float)measurePulse);
    }
}

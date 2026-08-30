using System;
using Godot;

/// <summary>
/// Apresentação visual do combo. A cena fornece painel, aura, material e labels;
/// este script recebe o estado já calculado e anima apenas a resposta visual.
/// </summary>
public partial class ArcaneComboDisplay : Panel
{
    private ShaderMaterial _auraMaterial = null!;
    private RhythmManager? _rhythmManager;
    private float _targetEnergy = 0.14f;
    private float _displayEnergy = 0.14f;
    private float _impact;
    private float _tierFlash;
    private float _breakAmount;
    private float _scalePunch;
    private float _floatTime;
    private Vector2 _basePosition;

    public override void _Ready()
    {
        var aura = GetNode<ColorRect>("ArcaneAura");
        _auraMaterial = aura.Material as ShaderMaterial ??
            throw new InvalidOperationException(
                $"{aura.GetPath()} precisa possuir ShaderMaterial configurado na cena.");
        _rhythmManager = GetNodeOrNull<RhythmManager>("/root/RhythmManager");
        _basePosition = Position;
        ApplyTierPalette(0);
        UpdateMaterial();
    }

    public override void _Process(double delta)
    {
        var deltaSeconds = (float)delta;
        _displayEnergy = Mathf.MoveToward(
            _displayEnergy,
            _targetEnergy,
            deltaSeconds * (_targetEnergy < _displayEnergy ? 1.25f : 2.4f));
        _impact = Mathf.MoveToward(_impact, 0.0f, deltaSeconds * 5.8f);
        _tierFlash = Mathf.MoveToward(_tierFlash, 0.0f, deltaSeconds * 2.7f);
        _breakAmount = Mathf.MoveToward(_breakAmount, 0.0f, deltaSeconds * 1.9f);
        _scalePunch = Mathf.MoveToward(_scalePunch, 0.0f, deltaSeconds * 5.5f);
        _floatTime += deltaSeconds;

        Scale = Vector2.One * (1.0f + _scalePunch * 0.055f);
        Position = _basePosition + new Vector2(
            Mathf.Sin(_floatTime * 0.72f) * _displayEnergy * 2.5f,
            Mathf.Cos(_floatTime * 0.58f) * _displayEnergy * 3.5f);
        UpdateMaterial();
    }

    public void SetComboState(int combo, int tier, int perfectStreak)
    {
        var safeTier = Mathf.Clamp(tier, 0, 3);
        var tierEnergy = safeTier switch
        {
            1 => 0.42f,
            2 => 0.68f,
            3 => 1.0f,
            _ => combo > 0 ? 0.28f : 0.14f,
        };
        var streakEnergy = Mathf.Clamp(perfectStreak * 0.025f, 0.0f, 0.18f);
        _targetEnergy = Mathf.Clamp(tierEnergy + streakEnergy, 0.0f, 1.0f);
        ApplyTierPalette(safeTier);
    }

    public void PlayComboIncrease()
    {
        _impact = 1.0f;
        _scalePunch = 1.0f;
    }

    public void PlayTierUp()
    {
        _tierFlash = 1.0f;
        _impact = 1.0f;
        _scalePunch = 1.0f;
    }

    public void PlayPerfectStreak()
    {
        _impact = Mathf.Max(_impact, 0.72f);
        _scalePunch = Mathf.Max(_scalePunch, 0.72f);
    }

    public void PlayComboBreak()
    {
        _breakAmount = 1.0f;
        _impact = 0.0f;
        _scalePunch = -0.7f;
    }

    private void ApplyTierPalette(int tier)
    {
        var primary = tier switch
        {
            1 => new Color(0.2f, 0.82f, 1.0f, 1.0f),
            2 => new Color(1.0f, 0.3f, 0.78f, 1.0f),
            3 => new Color(0.62f, 1.0f, 0.22f, 1.0f),
            _ => new Color(0.34f, 0.62f, 1.0f, 1.0f),
        };
        var secondary = tier switch
        {
            1 => new Color(0.62f, 0.28f, 1.0f, 1.0f),
            2 => new Color(1.0f, 0.72f, 0.16f, 1.0f),
            3 => new Color(1.0f, 0.16f, 0.78f, 1.0f),
            _ => new Color(0.7f, 0.22f, 1.0f, 1.0f),
        };
        _auraMaterial.SetShaderParameter("primary_color", primary);
        _auraMaterial.SetShaderParameter("secondary_color", secondary);
    }

    private void UpdateMaterial()
    {
        _auraMaterial.SetShaderParameter("energy", _displayEnergy);
        _auraMaterial.SetShaderParameter(
            "beat_phase",
            (float)(_rhythmManager?.BeatPhase ?? 0.0d));
        _auraMaterial.SetShaderParameter("impact", _impact);
        _auraMaterial.SetShaderParameter("tier_flash", _tierFlash);
        _auraMaterial.SetShaderParameter("break_amount", _breakAmount);
    }
}

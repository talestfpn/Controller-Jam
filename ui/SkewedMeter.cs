using Godot;

/// <summary>
/// Sincroniza o valor de um ProgressBar com o material visual configurado na cena.
/// A cena define geometria, shader e paleta; este script controla apenas estado e animação.
/// </summary>
public partial class SkewedMeter : ProgressBar
{
    [Export(PropertyHint.Range, "3,24,1")]
    public int SegmentCount { get; set; } = 12;

    [Export(PropertyHint.Range, "0.1,1.0,0.01")]
    public float Thickness { get; set; } = 0.68f;

    [Export(PropertyHint.Range, "-1.0,1.0,0.01")]
    public float Skew { get; set; } = 0.42f;

    [Export(PropertyHint.Range, "0.0,0.4,0.01")]
    public float SegmentGap { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "0.005,0.12,0.005")]
    public float EdgeWidth { get; set; } = 0.045f;

    [Export]
    public Color FillColor { get; set; } = new(1.0f, 0.08f, 0.3f, 1.0f);

    [Export]
    public Color TrailColor { get; set; } = new(1.0f, 0.55f, 0.2f, 1.0f);

    [Export]
    public Color EmptyColor { get; set; } = new(0.12f, 0.08f, 0.18f, 0.72f);

    [Export]
    public Color EdgeColor { get; set; } = new(1.0f, 0.58f, 0.72f, 1.0f);

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float GlowStrength { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "1.0,30.0,0.5")]
    public float FillAnimationSpeed { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.1,10.0,0.1")]
    public float DamageTrailSpeed { get; set; } = 2.8f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float DamageTrailDelay { get; set; } = 0.18f;

    private ShaderMaterial _material = null!;
    private float _displayRatio = 1.0f;
    private float _trailRatio = 1.0f;
    private float _trailDelayRemaining;
    private float _impactPulse;
    private float _lastTargetRatio = -1.0f;

    public override void _Ready()
    {
        var visual = GetNode<ColorRect>("MeterVisual");
        _material = (ShaderMaterial)visual.Material;
        _displayRatio = GetTargetRatio();
        _trailRatio = _displayRatio;
        ApplyConfiguration();
        UpdateMaterial();
    }

    public override void _Process(double delta)
    {
        var targetRatio = GetTargetRatio();
        var deltaSeconds = (float)delta;

        if (!Mathf.IsEqualApprox(targetRatio, _lastTargetRatio))
        {
            if (targetRatio < _lastTargetRatio || _lastTargetRatio < 0.0f)
            {
                _trailDelayRemaining = DamageTrailDelay;
                _impactPulse = 1.0f;
            }
            else
            {
                _trailRatio = targetRatio;
            }

            _lastTargetRatio = targetRatio;
        }

        _displayRatio = Mathf.MoveToward(
            _displayRatio,
            targetRatio,
            FillAnimationSpeed * deltaSeconds);

        if (_trailDelayRemaining > 0.0f)
        {
            _trailDelayRemaining -= deltaSeconds;
        }
        else
        {
            _trailRatio = Mathf.MoveToward(
                _trailRatio,
                targetRatio,
                DamageTrailSpeed * deltaSeconds);
        }

        _trailRatio = Mathf.Max(_displayRatio, _trailRatio);
        _impactPulse = Mathf.MoveToward(_impactPulse, 0.0f, 4.5f * deltaSeconds);
        UpdateMaterial();
    }

    public void PulseDamage()
    {
        _impactPulse = 1.0f;
        _trailDelayRemaining = DamageTrailDelay;
    }

    private float GetTargetRatio()
    {
        return MaxValue <= 0.0
            ? 0.0f
            : Mathf.Clamp((float)(Value / MaxValue), 0.0f, 1.0f);
    }

    private void ApplyConfiguration()
    {
        _material.SetShaderParameter("segment_count", SegmentCount);
        _material.SetShaderParameter("thickness", Thickness);
        _material.SetShaderParameter("skew", Skew);
        _material.SetShaderParameter("segment_gap", SegmentGap);
        _material.SetShaderParameter("edge_width", EdgeWidth);
        _material.SetShaderParameter("fill_color", FillColor);
        _material.SetShaderParameter("trail_color", TrailColor);
        _material.SetShaderParameter("empty_color", EmptyColor);
        _material.SetShaderParameter("edge_color", EdgeColor);
        _material.SetShaderParameter("glow_strength", GlowStrength);
    }

    private void UpdateMaterial()
    {
        _material.SetShaderParameter("value_ratio", _displayRatio);
        _material.SetShaderParameter("trail_ratio", _trailRatio);
        _material.SetShaderParameter("impact_pulse", _impactPulse);
    }
}

using Godot;

/// <summary>
/// Medidor contínuo baseado em porcentagem. A cena fornece o recorte da arte,
/// moldura, materiais e paleta; este script apenas interpola o valor recebido.
/// </summary>
public partial class PercentageMeter : ProgressBar
{
    [Export]
    public Color FillColor { get; set; } = new(1.0f, 0.08f, 0.3f, 1.0f);

    [Export]
    public Color TrailColor { get; set; } = new(1.0f, 0.55f, 0.2f, 1.0f);

    [Export]
    public Color EmptyColor { get; set; } = new(0.12f, 0.08f, 0.18f, 0.72f);

    [Export]
    public Color FrameColor { get; set; } = new(1.0f, 0.58f, 0.72f, 1.0f);

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float GlowStrength { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "1.0,30.0,0.5")]
    public float FillAnimationSpeed { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.1,10.0,0.1")]
    public float DamageTrailSpeed { get; set; } = 2.8f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float DamageTrailDelay { get; set; } = 0.18f;

    [Export]
    public Texture2D? FillTexture { get; set; }

    private TextureRect _meterVisual = null!;
    private TextureRect _trailVisual = null!;
    private ShaderMaterial _meterMaterial = null!;
    private ShaderMaterial _trailMaterial = null!;
    private float _displayRatio = 1.0f;
    private float _trailRatio = 1.0f;
    private float _trailDelayRemaining;
    private float _impactPulse;
    private float _lastTargetRatio = -1.0f;

    public override void _Ready()
    {
        _meterVisual = GetNode<TextureRect>("MeterVisual");
        _trailVisual = GetNode<TextureRect>("TrailVisual");
        _meterMaterial = _meterVisual.Material as ShaderMaterial ??
            throw new System.InvalidOperationException(
                $"{_meterVisual.GetPath()} precisa de ShaderMaterial.");
        _trailMaterial = _trailVisual.Material as ShaderMaterial ??
            throw new System.InvalidOperationException(
                $"{_trailVisual.GetPath()} precisa de ShaderMaterial.");

        _displayRatio = GetTargetRatio();
        _trailRatio = _displayRatio;
        ApplyConfiguration();
        UpdateVisuals();
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
        UpdateVisuals();
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
        if (FillTexture != null)
        {
            _meterVisual.Texture = FillTexture;
            _trailVisual.Texture = FillTexture;
        }

        _meterMaterial.SetShaderParameter("fill_color", FillColor);
        _meterMaterial.SetShaderParameter("frame_color", FrameColor);
        _meterMaterial.SetShaderParameter("glow_strength", GlowStrength);
        _trailMaterial.SetShaderParameter("fill_color", TrailColor);
        _trailMaterial.SetShaderParameter("frame_color", FrameColor);
        _trailMaterial.SetShaderParameter("glow_strength", GlowStrength * 0.7f);

        _meterMaterial.SetShaderParameter("empty_color", EmptyColor);
        _trailMaterial.SetShaderParameter("empty_color", EmptyColor);
    }

    private void UpdateVisuals()
    {
        _meterMaterial.SetShaderParameter("value_ratio", _displayRatio);
        _trailMaterial.SetShaderParameter("value_ratio", _trailRatio);
        _meterMaterial.SetShaderParameter("glow_strength", GlowStrength + _impactPulse * 0.18f);
    }
}

using Godot;

/// <summary>
/// Anima os valores de impacto já calculados pelo combate. A cena fornece
/// labels, fonte, material e área visual; este script controla somente estado.
/// </summary>
public partial class CombatValueFeedback : Control
{
    private Label _damageLabel = null!;
    private Label _resistanceLabel = null!;
    private ShaderMaterial _auraMaterial = null!;

    public override void _Ready()
    {
        _damageLabel = GetNode<Label>("DamageLabel");
        _resistanceLabel = GetNode<Label>("ResistanceLabel");
        var aura = GetNode<ColorRect>("Aura");
        _auraMaterial = (ShaderMaterial)aura.Material;
    }

    public void Play(
        Vector2 origin,
        float healthDamage,
        float resistanceDamage,
        bool incoming,
        TimingResult timingResult,
        float travelDirection)
    {
        Position = origin;
        PivotOffset = Size * 0.5f;
        Scale = Vector2.One * 0.72f;
        Modulate = Colors.White;

        var healthPrefix = incoming ? "DANO RECEBIDO" : "DANO";
        _damageLabel.Text = $"{healthPrefix}  {healthDamage:0.0}";
        _resistanceLabel.Text = $"RESISTÊNCIA  -{resistanceDamage:0.0}";

        var primary = GetPrimaryColor(timingResult, incoming);
        var secondary = GetSecondaryColor(timingResult, incoming);
        _damageLabel.Modulate = primary;
        _resistanceLabel.Modulate = secondary;
        _auraMaterial.SetShaderParameter("primary_color", primary);
        _auraMaterial.SetShaderParameter("secondary_color", secondary);
        _auraMaterial.SetShaderParameter(
            "energy",
            timingResult == TimingResult.Perfect ? 1.0f : 0.72f);
        _auraMaterial.SetShaderParameter("direction", travelDirection);

        var destination = origin + new Vector2(44.0f * travelDirection, -34.0f);
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(this, "scale", Vector2.One * 1.05f, 0.18d);
        tween.Parallel().TweenProperty(this, "position", origin + new Vector2(
            12.0f * travelDirection,
            -8.0f), 0.18d);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(this, "position", destination, 0.62d);
        tween.Parallel().TweenProperty(
            this,
            "modulate:a",
            0.0f,
            0.42d).SetDelay(0.2d);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private static Color GetPrimaryColor(TimingResult result, bool incoming)
    {
        if (incoming && result == TimingResult.Miss)
        {
            return new Color(1.0f, 0.16f, 0.2f, 1.0f);
        }

        return result switch
        {
            TimingResult.Perfect => new Color(0.55f, 1.0f, 0.24f, 1.0f),
            TimingResult.Good => new Color(0.25f, 0.78f, 1.0f, 1.0f),
            TimingResult.Ok => new Color(1.0f, 0.78f, 0.18f, 1.0f),
            _ => new Color(1.0f, 0.28f, 0.3f, 1.0f),
        };
    }

    private static Color GetSecondaryColor(TimingResult result, bool incoming)
    {
        if (incoming && result == TimingResult.Perfect)
        {
            return new Color(0.78f, 0.36f, 1.0f, 1.0f);
        }

        return result switch
        {
            TimingResult.Perfect => new Color(0.38f, 1.0f, 0.76f, 1.0f),
            TimingResult.Good => new Color(0.64f, 0.42f, 1.0f, 1.0f),
            TimingResult.Ok => new Color(1.0f, 0.48f, 0.14f, 1.0f),
            _ => new Color(1.0f, 0.45f, 0.22f, 1.0f),
        };
    }
}

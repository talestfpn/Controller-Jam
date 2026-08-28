using Godot;

/// <summary>
/// Configuração central dos julgamentos de timing e dos multiplicadores que serão
/// usados pelas próximas etapas de combate.
/// </summary>
[GlobalClass]
public partial class TimingConfig : Resource
{
    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float PerfectThreshold { get; set; } = 0.90f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float GoodThreshold { get; set; } = 0.70f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float OkThreshold { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "50.0,150.0,1.0")]
    public float MaximumAcceptedCircleSizePercent { get; set; } = 100.0f;

    [Export(PropertyHint.Range, "0.05,1.0,0.01")]
    public float JudgementWindowBeats { get; set; } = 0.50f;

    [Export(PropertyHint.Range, "0.0,3.0,0.01")]
    public float PerfectDamageMultiplier { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "0.0,3.0,0.01")]
    public float PerfectPostureMultiplier { get; set; } = 1.50f;

    [Export(PropertyHint.Range, "0.0,3.0,0.01")]
    public float GoodPostureMultiplier { get; set; } = 1.00f;

    [Export(PropertyHint.Range, "0.0,3.0,0.01")]
    public float OkPostureMultiplier { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PlayerPerfectPostureDamage { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PlayerGoodPostureDamage { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PlayerOkPostureDamage { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PlayerMissPostureDamage { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float ParryPostureDamage { get; set; } = 15.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float BlockPostureDamage { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float PartialBlockPostureDamage { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float HitPostureDamage { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,9999.0,1.0")]
    public float PerfectExecutionDamage { get; set; } = 50.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float PerfectIncomingDamageMultiplier { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float GoodIncomingDamageMultiplier { get; set; } = 0.0f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float OkIncomingDamageMultiplier { get; set; } = 0.30f;

    [Export(PropertyHint.Range, "0.0,1.0,0.01")]
    public float MissIncomingDamageMultiplier { get; set; } = 1.0f;
}

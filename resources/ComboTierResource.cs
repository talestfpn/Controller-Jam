using Godot;

/// <summary>
/// Um nível de Combo configurável no Inspector.
/// </summary>
[GlobalClass]
public partial class ComboTierResource : Resource
{
    [Export(PropertyHint.Range, "0,9999,1")]
    public int RequiredCombo { get; set; }

    [Export(PropertyHint.Range, "0.0,10.0,0.01")]
    public float DamageMultiplier { get; set; } = 1.0f;

    [Export]
    public string MilestoneText { get; set; } = "FLOW UP!";
}

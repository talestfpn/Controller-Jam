using Godot;

/// <summary>
/// Valores de recompensa configuráveis pelo Inspector.
/// </summary>
[GlobalClass]
public partial class BattleRewardConfig : Resource
{
    [Export(PropertyHint.Range, "0,1000,1")]
    public int PerfectBonusPerCount { get; set; } = 2;

    [Export(PropertyHint.Range, "0,1000,1")]
    public int ExecutionPerfectBonus { get; set; } = 10;

    [Export(PropertyHint.Range, "0.1,5.0,0.05")]
    public float ComboMultiplierBelowTen { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.1,5.0,0.05")]
    public float ComboMultiplierTenToNineteen { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "0.1,5.0,0.05")]
    public float ComboMultiplierTwentyOrMore { get; set; } = 1.5f;

    public float GetComboRewardMultiplier(int highestCombo)
    {
        if (highestCombo >= 20)
        {
            return ComboMultiplierTwentyOrMore;
        }

        if (highestCombo >= 10)
        {
            return ComboMultiplierTenToNineteen;
        }

        return ComboMultiplierBelowTen;
    }
}

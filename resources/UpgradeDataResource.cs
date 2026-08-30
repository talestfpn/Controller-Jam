using Godot;

public enum UpgradeCategory
{
    Damage,
    Posture,
    Parry,
    Combo,
    Survival,
    Execution,
    Essence,
    RiskReward,
}

public enum UpgradeRarity
{
    Common,
    Rare,
    Arcane,
}

/// <summary>
/// Dados de uma carta de upgrade. A cena da carta fornece o layout e o frame;
/// este Resource fornece texto, categoria, raridade, custo e valores de jogo.
/// </summary>
[GlobalClass]
public partial class UpgradeDataResource : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public string DisplayName { get; set; } = "UPGRADE";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string EffectSummary { get; set; } = string.Empty;

    [Export]
    public Texture2D? Icon { get; set; }

    [Export]
    public UpgradeCategory Category { get; set; } = UpgradeCategory.Damage;

    [Export]
    public UpgradeRarity Rarity { get; set; } = UpgradeRarity.Common;

    [Export(PropertyHint.Range, "0,9999,1")]
    public int EssenceCost { get; set; } = 60;

    [Export(PropertyHint.Range, "1,99,1")]
    public int MaxStacks { get; set; } = 1;

    [Export]
    public string SpecialEffectId { get; set; } = string.Empty;

    /// <summary>
    /// Cartas consumíveis resolvem seu efeito na compra e não entram na
    /// RunBuild. Elas podem voltar a aparecer em lojas futuras.
    /// </summary>
    [Export]
    public bool IsConsumable { get; set; }

    [Export(PropertyHint.Range, "-1000,1000,0.01")]
    public float PrimaryValue { get; set; }

    [Export(PropertyHint.Range, "-1000,1000,0.01")]
    public float SecondaryValue { get; set; }

    [Export(PropertyHint.Range, "-1000,1000,0.01")]
    public float TertiaryValue { get; set; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Id) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        MaxStacks > 0;
}

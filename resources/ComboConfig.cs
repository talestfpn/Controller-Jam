using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Configuração central dos tiers de Combo. A ordem dos Resources no Inspector
/// não precisa ser mantida manualmente: os tiers são ordenados pelo Combo exigido.
/// </summary>
[GlobalClass]
public partial class ComboConfig : Resource
{
    [Export]
    public Godot.Collections.Array<ComboTierResource> Tiers { get; set; } = new();

    public float GetDamageMultiplier(int combo)
    {
        var tier = GetTier(combo, out _);
        return tier == null ? 1.0f : Math.Max(0.0f, tier.DamageMultiplier);
    }

    public int GetTierIndex(int combo)
    {
        GetTier(combo, out var tierIndex);
        return tierIndex;
    }

    public List<ComboTierResource> GetOrderedTiers()
    {
        var orderedTiers = new List<ComboTierResource>();
        if (Tiers == null)
        {
            return orderedTiers;
        }

        foreach (var tier in Tiers)
        {
            if (tier != null)
            {
                orderedTiers.Add(tier);
            }
        }

        orderedTiers.Sort((left, right) =>
            left.RequiredCombo.CompareTo(right.RequiredCombo));
        return orderedTiers;
    }

    private ComboTierResource? GetTier(int combo, out int tierIndex)
    {
        var orderedTiers = GetOrderedTiers();
        tierIndex = 0;
        if (orderedTiers.Count == 0)
        {
            return null;
        }

        var safeCombo = Math.Max(0, combo);
        var selectedTier = orderedTiers[0];
        for (var index = 0; index < orderedTiers.Count; index++)
        {
            var tier = orderedTiers[index];
            if (safeCombo < tier.RequiredCombo)
            {
                break;
            }

            selectedTier = tier;
            tierIndex = index;
        }

        return selectedTier;
    }
}

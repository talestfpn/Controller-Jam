using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sorteio determinístico das três cartas. Não conhece a cena nem a UI.
/// </summary>
public static class ShopCandidateGenerator
{
    public static List<UpgradeDataResource> Generate(
        IEnumerable<UpgradeDataResource> pool,
        RunBuild build,
        int seed,
        float commonWeight,
        float rareWeight,
        float arcaneWeight,
        int count = 3,
        ISet<string>? excludedIds = null)
    {
        var safeCount = Math.Max(0, count);
        var random = new Random(seed);
        var result = new List<UpgradeDataResource>(safeCount);
        var selectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Fill(
            pool,
            build,
            random,
            safeCount,
            commonWeight,
            rareWeight,
            arcaneWeight,
            excludedIds,
            selectedIds,
            result);

        // Se ainda houver menos de três alternativas, permitir os IDs da
        // oferta anterior é preferível a mostrar menos cartas. Com a pool
        // inicial isso só seria alcançado após quase todos os upgrades.
        if (result.Count < safeCount && excludedIds != null)
        {
            Fill(
                pool,
                build,
                random,
                safeCount,
                commonWeight,
                rareWeight,
                arcaneWeight,
                null,
                selectedIds,
                result);
        }

        return result;
    }

    private static void Fill(
        IEnumerable<UpgradeDataResource> pool,
        RunBuild build,
        Random random,
        int count,
        float commonWeight,
        float rareWeight,
        float arcaneWeight,
        ISet<string>? excludedIds,
        ISet<string> selectedIds,
        ICollection<UpgradeDataResource> result)
    {
        while (result.Count < count)
        {
            var valid = pool
                .Where(definition =>
                    definition != null &&
                    definition.IsValid &&
                    build.CanAcquire(definition) &&
                    !selectedIds.Contains(definition.Id) &&
                    (excludedIds == null || !excludedIds.Contains(definition.Id)))
                .ToList();
            if (valid.Count == 0)
            {
                return;
            }

            var selectedRarity = ChooseRarity(
                valid,
                random,
                commonWeight,
                rareWeight,
                arcaneWeight);
            var byRarity = valid
                .Where(definition => definition.Rarity == selectedRarity)
                .ToList();
            var source = byRarity.Count > 0 ? byRarity : valid;
            var selected = source[random.Next(source.Count)];
            result.Add(selected);
            selectedIds.Add(selected.Id);
        }
    }

    private static UpgradeRarity ChooseRarity(
        IReadOnlyCollection<UpgradeDataResource> valid,
        Random random,
        float commonWeight,
        float rareWeight,
        float arcaneWeight)
    {
        var available = new List<(UpgradeRarity rarity, float weight)>();
        AddIfAvailable(available, valid, UpgradeRarity.Common, commonWeight);
        AddIfAvailable(available, valid, UpgradeRarity.Rare, rareWeight);
        AddIfAvailable(available, valid, UpgradeRarity.Arcane, arcaneWeight);
        if (available.Count == 0)
        {
            return UpgradeRarity.Common;
        }

        var totalWeight = available.Sum(item => Math.Max(0.0f, item.weight));
        if (totalWeight <= 0.0f)
        {
            return available[random.Next(available.Count)].rarity;
        }

        var roll = random.NextDouble() * totalWeight;
        foreach (var item in available)
        {
            roll -= Math.Max(0.0f, item.weight);
            if (roll <= 0.0d)
            {
                return item.rarity;
            }
        }

        return available[^1].rarity;
    }

    private static void AddIfAvailable(
        ICollection<(UpgradeRarity rarity, float weight)> available,
        IEnumerable<UpgradeDataResource> valid,
        UpgradeRarity rarity,
        float weight)
    {
        if (valid.Any(definition => definition.Rarity == rarity))
        {
            available.Add((rarity, weight));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Build temporária de uma run. Os Resources originais permanecem imutáveis;
/// somente a quantidade de stacks adquirida é armazenada aqui.
/// </summary>
public sealed class RunBuild
{
    private sealed class Entry
    {
        public Entry(UpgradeDataResource definition)
        {
            Definition = definition;
        }

        public UpgradeDataResource Definition { get; }
        public int Stacks { get; set; }
    }

    private readonly Dictionary<string, Entry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public int UniqueUpgradeCount => _entries.Count;

    public bool HasUpgrade(string id)
    {
        return GetStacks(id) > 0;
    }

    public int GetStacks(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
            _entries.TryGetValue(id, out var entry)
            ? entry.Stacks
            : 0;
    }

    public UpgradeDataResource? GetDefinition(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
            _entries.TryGetValue(id, out var entry)
            ? entry.Definition
            : null;
    }

    public bool CanAcquire(UpgradeDataResource? definition)
    {
        if (definition == null || !definition.IsValid)
        {
            return false;
        }

        // Cartas consumíveis, como Renascimento Arcano, não ocupam espaço na
        // build e podem reaparecer em uma loja futura.
        if (definition.IsConsumable)
        {
            return true;
        }

        return GetStacks(definition.Id) < definition.MaxStacks;
    }

    public bool AddUpgrade(UpgradeDataResource? definition)
    {
        if (!CanAcquire(definition) || definition!.IsConsumable)
        {
            return false;
        }

        var id = definition.Id;
        if (!_entries.TryGetValue(id, out var entry))
        {
            entry = new Entry(definition);
            _entries.Add(id, entry);
        }

        entry.Stacks += 1;
        return true;
    }

    public float GetPrimaryValue(string id, float fallback = 0.0f)
    {
        return GetDefinition(id) is { } definition
            ? definition.PrimaryValue * GetStacks(id)
            : fallback;
    }

    public float GetSecondaryValue(string id, float fallback = 0.0f)
    {
        return GetDefinition(id) is { } definition
            ? definition.SecondaryValue * GetStacks(id)
            : fallback;
    }

    public float GetTertiaryValue(string id, float fallback = 0.0f)
    {
        return GetDefinition(id) is { } definition
            ? definition.TertiaryValue * GetStacks(id)
            : fallback;
    }

    public float GetEffectiveMaxHealth(float baseHealth)
    {
        var vitalityBonus = GetPrimaryValue(UpgradeIds.Vitality);
        var glassPenalty = GetSecondaryValue(UpgradeIds.GlassArcana);
        var health = (Math.Max(1.0f, baseHealth) + vitalityBonus) *
            Math.Max(0.1f, 1.0f - glassPenalty);
        return Math.Max(1.0f, health);
    }

    public IReadOnlyDictionary<string, int> GetStacksSnapshot()
    {
        return _entries.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Stacks,
            StringComparer.OrdinalIgnoreCase);
    }

    public string GetDebugSummary()
    {
        if (_entries.Count == 0)
        {
            return "NONE";
        }

        return string.Join(
            ", ",
            _entries.Values
                .OrderBy(entry => entry.Definition.DisplayName)
                .Select(entry =>
                    $"{entry.Definition.DisplayName} {entry.Stacks}/{entry.Definition.MaxStacks}"));
    }

    public void Reset()
    {
        _entries.Clear();
    }
}

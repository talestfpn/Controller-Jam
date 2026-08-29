using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Resumo do encounter gerado, usado por debug e validação.
/// </summary>
public sealed class EncounterPatternStats
{
    public int EventCount { get; init; }
    public int PlayerAttackCount { get; init; }
    public int EnemyAttackCount { get; init; }
    public int HalfBeatCount { get; init; }
    public int BurstCount { get; init; }
    public int FeintCount { get; init; }
    public int FadeCount { get; init; }
    public int DecoyCount { get; init; }
    public int MaximumBurstLength { get; init; }
    public float MinimumTargetSpacingBeats { get; init; }
    public float AverageTargetSpacingBeats { get; init; }
    public float MinimumBurstSpacingBeats { get; init; }
    public int MaximumActiveLogicalPrompts { get; init; }
}

public sealed class EncounterPatternGenerationResult
{
    public required RhythmPatternResource Pattern { get; init; }
    public required EncounterPatternStats Stats { get; init; }
}

/// <summary>
/// Transforma candidatos do chart musical-base em um moveset rítmico de
/// inimigo. A transformação é leve, determinística e não modifica o Resource
/// de origem.
/// </summary>
public static class EncounterPatternGenerator
{
    private const double PositionTolerance = 0.001d;

    public static EncounterPatternGenerationResult Generate(
        RhythmPatternResource basePattern,
        EnemyRhythmProfileResource profile,
        ulong seed,
        float playerAttackDamage = 10.0f,
        float enemyAttackDamage = 20.0f,
        RhythmFairnessConfigResource? fairnessConfig = null)
    {
        ArgumentNullException.ThrowIfNull(basePattern);
        ArgumentNullException.ThrowIfNull(profile);

        var fairness = fairnessConfig ?? new RhythmFairnessConfigResource();
        var minimumTargetSpacing = fairness.GetEffectiveMinimumTargetSpacingBeats();
        var random = new RandomNumberGenerator
        {
            Seed = seed ^ GetStableHash(profile.ProfileName),
        };
        var sourceEvents = basePattern.GetEventsSorted();
        var selectedEvents = SelectAnchors(
            sourceEvents,
            profile,
            random,
            playerAttackDamage,
            enemyAttackDamage,
            minimumTargetSpacing);
        var encounterEvents = ExpandBursts(
            selectedEvents,
            profile,
            random,
            Math.Max(0.25d, basePattern.LoopLengthBeats),
            playerAttackDamage,
            enemyAttackDamage,
            fairness,
            minimumTargetSpacing);
        encounterEvents = ApplyFairnessFilter(
            encounterEvents,
            Math.Max(0.25d, basePattern.LoopLengthBeats),
            minimumTargetSpacing);

        var runtimePattern = new RhythmPatternResource
        {
            PatternName = $"{basePattern.PatternName} / {profile.ProfileName}",
            LoopLengthBeats = basePattern.LoopLengthBeats,
            Events = new Godot.Collections.Array<RhythmEventResource>(),
        };

        foreach (var rhythmEvent in encounterEvents)
        {
            runtimePattern.Events.Add(rhythmEvent);
        }

        return new EncounterPatternGenerationResult
        {
            Pattern = runtimePattern,
            Stats = BuildStats(encounterEvents, runtimePattern.LoopLengthBeats),
        };
    }

    private static List<RhythmEventResource> SelectAnchors(
        IReadOnlyList<RhythmEventResource> sourceEvents,
        EnemyRhythmProfileResource profile,
        RandomNumberGenerator random,
        float playerAttackDamage,
        float enemyAttackDamage,
        float minimumTargetSpacing)
    {
        var selected = new List<RhythmEventResource>();
        var lastSelectedBeat = double.NegativeInfinity;
        RhythmPromptType? previousType = null;

        foreach (var sourceEvent in sourceEvents)
        {
            if (sourceEvent == null ||
                sourceEvent.GetPromptType() == RhythmPromptType.Execution)
            {
                continue;
            }

            var beatPosition = Math.Max(0.0d, sourceEvent.BeatOffset);
            var isHalfBeat = IsHalfBeat(beatPosition);
            if (isHalfBeat &&
                (!profile.AllowHalfBeats || random.Randf() > profile.HalfBeatChance))
            {
                continue;
            }

            var spacing = beatPosition - lastSelectedBeat;
            if (spacing + PositionTolerance < Math.Max(
                    Math.Max(0.5f, profile.MinimumSpacingBeats),
                    minimumTargetSpacing))
            {
                continue;
            }

            var selectionChance = GetSelectionChance(
                sourceEvent,
                beatPosition,
                spacing,
                profile);
            if (random.Randf() > selectionChance)
            {
                continue;
            }

            var promptType = ChoosePromptType(
                sourceEvent.GetPromptType(),
                previousType,
                profile,
                random);
            var encounterEvent = CreateEncounterEvent(
                sourceEvent,
                beatPosition,
                promptType,
                ChooseBehavior(profile, random, promptType, false),
                playerAttackDamage,
                enemyAttackDamage);
            selected.Add(encounterEvent);
            lastSelectedBeat = beatPosition;
            previousType = promptType;
        }

        if (selected.Count == 0)
        {
            foreach (var sourceEvent in sourceEvents)
            {
                if (sourceEvent == null ||
                    sourceEvent.GetPromptType() == RhythmPromptType.Execution ||
                    (IsHalfBeat(sourceEvent.BeatOffset) && !profile.AllowHalfBeats))
                {
                    continue;
                }

                var promptType = ChoosePromptType(
                    sourceEvent.GetPromptType(),
                    null,
                    profile,
                    random);
                selected.Add(CreateEncounterEvent(
                    sourceEvent,
                    sourceEvent.BeatOffset,
                    promptType,
                    ChooseBehavior(profile, random, promptType, false),
                    playerAttackDamage,
                    enemyAttackDamage));
                break;
            }
        }

        return selected;
    }

    private static float GetSelectionChance(
        RhythmEventResource sourceEvent,
        double beatPosition,
        double spacing,
        EnemyRhythmProfileResource profile)
    {
        var chance = Mathf.Clamp(profile.EventDensity, 0.0f, 1.0f);
        var preferredSpacing = Math.Max(
            Math.Max(0.5f, profile.MinimumSpacingBeats),
            profile.PreferredSpacingBeats);
        if (!double.IsPositiveInfinity(spacing) && spacing < preferredSpacing)
        {
            chance *= Mathf.Clamp((float)(spacing / preferredSpacing), 0.35f, 1.0f);
        }

        if (IsStrongBeat(beatPosition))
        {
            chance += Mathf.Clamp(profile.StrongBeatPreference, 0.0f, 1.0f) * 0.35f;
        }

        // O chart-base não guarda energia espectral numérica. Os eventos já
        // classificados como ENEMY_ATTACK são os marcadores pesados que ainda
        // existem após a análise offline e servem como preferência, não regra.
        if (sourceEvent.GetPromptType() == RhythmPromptType.EnemyAttack)
        {
            chance += Mathf.Clamp(profile.HeavyEventPreference, 0.0f, 1.0f) * 0.35f;
        }

        return Mathf.Clamp(chance, 0.0f, 1.0f);
    }

    private static List<RhythmEventResource> ExpandBursts(
        IReadOnlyList<RhythmEventResource> anchors,
        EnemyRhythmProfileResource profile,
        RandomNumberGenerator random,
        double loopLength,
        float playerAttackDamage,
        float enemyAttackDamage,
        RhythmFairnessConfigResource fairness,
        float minimumTargetSpacing)
    {
        var result = new List<RhythmEventResource>();
        var nextChainId = 1;

        for (var anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            var anchor = anchors[anchorIndex];
            var chain = new List<RhythmEventResource> { anchor };
            var nextAnchorBeat = anchorIndex + 1 < anchors.Count
                ? anchors[anchorIndex + 1].BeatOffset
                : loopLength;
            var canBurst = profile.BurstChance > 0.0f &&
                random.Randf() <= profile.BurstChance;

            if (canBurst)
            {
                var maximumBurstLength = fairness.GetEffectiveMaximumBurstLength();
                var minLength = Math.Clamp(profile.MinBurstLength, 2, maximumBurstLength);
                var maxLength = Math.Clamp(profile.MaxBurstLength, minLength, maximumBurstLength);
                var desiredLength = random.RandiRange(minLength, maxLength);
                var burstSpacing = Math.Max(
                    minimumTargetSpacing,
                    Math.Max(
                        Math.Max(0.5f, fairness.MinimumBurstSpacingBeats),
                        profile.BurstSpacingBeats));
                var previousType = anchor.GetPromptType();

                for (var chainIndex = 1; chainIndex < desiredLength; chainIndex++)
                {
                    var childBeat = anchor.BeatOffset +
                        (burstSpacing * chainIndex);
                    if (childBeat >= loopLength - PositionTolerance ||
                        childBeat >= nextAnchorBeat - minimumTargetSpacing + PositionTolerance)
                    {
                        break;
                    }

                    var childType = ChoosePromptType(
                        previousType,
                        previousType,
                        profile,
                        random);
                    var child = CreateEncounterEvent(
                        anchor,
                        childBeat,
                        childType,
                        RhythmPromptBehavior.Normal,
                        playerAttackDamage,
                        enemyAttackDamage);
                    chain.Add(child);
                    previousType = childType;
                }
            }

            if (chain.Count > 1)
            {
                for (var chainIndex = 0; chainIndex < chain.Count; chainIndex++)
                {
                    // Burst é uma sequência de alvos, não uma combinação de
                    // truques visuais. Mantê-lo normal evita caos de leitura.
                    chain[chainIndex].PromptBehavior = (int)RhythmPromptBehavior.Normal;
                    chain[chainIndex].ChainId = nextChainId;
                    chain[chainIndex].ChainIndex = chainIndex;
                    chain[chainIndex].ChainLength = chain.Count;
                }

                nextChainId++;
            }

            result.AddRange(chain);
        }

        result.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));
        return result;
    }

    private static RhythmPromptType ChoosePromptType(
        RhythmPromptType sourceType,
        RhythmPromptType? previousType,
        EnemyRhythmProfileResource profile,
        RandomNumberGenerator random)
    {
        var playerWeight = Math.Max(0.0f, profile.PlayerAttackWeight);
        var enemyWeight = Math.Max(0.0f, profile.EnemyAttackWeight);
        var totalWeight = playerWeight + enemyWeight;
        var enemyChance = totalWeight <= 0.0f
            ? 0.5f
            : enemyWeight / totalWeight;
        var baseBias = Mathf.Clamp(profile.BaseActionBias, 0.0f, 1.0f);
        enemyChance = sourceType == RhythmPromptType.EnemyAttack
            ? Mathf.Lerp(enemyChance, 1.0f, baseBias)
            : Mathf.Lerp(enemyChance, 0.0f, baseBias);

        if (previousType.HasValue)
        {
            if (previousType.Value == RhythmPromptType.EnemyAttack &&
                random.Randf() <= profile.DefenseChainChance)
            {
                return RhythmPromptType.EnemyAttack;
            }

            if (random.Randf() <= profile.AlternationChance)
            {
                return previousType.Value == RhythmPromptType.EnemyAttack
                    ? RhythmPromptType.PlayerAttack
                    : RhythmPromptType.EnemyAttack;
            }
        }

        return random.Randf() <= enemyChance
            ? RhythmPromptType.EnemyAttack
            : RhythmPromptType.PlayerAttack;
    }

    private static RhythmPromptBehavior ChooseBehavior(
        EnemyRhythmProfileResource profile,
        RandomNumberGenerator random,
        RhythmPromptType promptType,
        bool isBurst)
    {
        if (isBurst || promptType == RhythmPromptType.Execution)
        {
            return RhythmPromptBehavior.Normal;
        }

        var feintChance = Mathf.Clamp(profile.FeintChance, 0.0f, 1.0f);
        var fadeChance = Mathf.Clamp(profile.VisualFadeChance, 0.0f, 1.0f);
        var decoyChance = promptType == RhythmPromptType.PlayerAttack
            ? Mathf.Clamp(profile.DecoyChance, 0.0f, 1.0f)
            : 0.0f;
        var roll = random.Randf();
        if (roll < decoyChance)
        {
            return RhythmPromptBehavior.Decoy;
        }

        roll -= decoyChance;
        if (roll < feintChance)
        {
            return RhythmPromptBehavior.Feint;
        }

        roll -= feintChance;
        return roll < fadeChance
            ? RhythmPromptBehavior.FadeBeforeTarget
            : RhythmPromptBehavior.Normal;
    }

    private static RhythmEventResource CreateEncounterEvent(
        RhythmEventResource sourceEvent,
        double beatPosition,
        RhythmPromptType promptType,
        RhythmPromptBehavior behavior,
        float playerAttackDamage,
        float enemyAttackDamage)
    {
        return new RhythmEventResource
        {
            BeatOffset = (float)beatPosition,
            PromptType = (int)promptType,
            Damage = promptType == RhythmPromptType.EnemyAttack
                ? Math.Max(0.0f, enemyAttackDamage)
                : Math.Max(0.0f, playerAttackDamage),
            PostureDamage = sourceEvent.PostureDamage,
            PromptBehavior = (int)behavior,
            ChainLength = 1,
            SourceBeatOffset = sourceEvent.SourceBeatOffset > 0.0f
                ? sourceEvent.SourceBeatOffset
                : sourceEvent.BeatOffset,
        };
    }

    private static List<RhythmEventResource> ApplyFairnessFilter(
        IReadOnlyList<RhythmEventResource> candidates,
        double loopLength,
        float minimumTargetSpacing)
    {
        var sortedCandidates = new List<RhythmEventResource>(candidates);
        sortedCandidates.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));

        var accepted = new List<RhythmEventResource>();
        foreach (var candidate in sortedCandidates)
        {
            if (candidate == null ||
                candidate.BeatOffset < -PositionTolerance ||
                candidate.BeatOffset >= loopLength - PositionTolerance)
            {
                continue;
            }

            if (accepted.Count == 0 ||
                candidate.BeatOffset - accepted[^1].BeatOffset + PositionTolerance >=
                minimumTargetSpacing)
            {
                accepted.Add(candidate);
            }
        }

        // Events repeat after the loop. If the end and the start are too close,
        // discard the first candidate instead of shifting either musical target.
        while (accepted.Count > 1)
        {
            var circularSpacing = loopLength - accepted[^1].BeatOffset + accepted[0].BeatOffset;
            if (circularSpacing + PositionTolerance >= minimumTargetSpacing)
            {
                break;
            }

            accepted.RemoveAt(0);
        }

        NormalizeChainMetadata(accepted);
        return accepted;
    }

    private static void NormalizeChainMetadata(List<RhythmEventResource> events)
    {
        var chains = new Dictionary<int, List<RhythmEventResource>>();
        foreach (var rhythmEvent in events)
        {
            if (rhythmEvent.ChainId <= 0)
            {
                rhythmEvent.ChainId = 0;
                rhythmEvent.ChainIndex = 0;
                rhythmEvent.ChainLength = 1;
                continue;
            }

            if (!chains.TryGetValue(rhythmEvent.ChainId, out var chain))
            {
                chain = new List<RhythmEventResource>();
                chains.Add(rhythmEvent.ChainId, chain);
            }

            chain.Add(rhythmEvent);
        }

        foreach (var chain in chains.Values)
        {
            chain.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));
            if (chain.Count < 2)
            {
                chain[0].ChainId = 0;
                chain[0].ChainIndex = 0;
                chain[0].ChainLength = 1;
                continue;
            }

            for (var index = 0; index < chain.Count; index++)
            {
                chain[index].ChainIndex = index;
                chain[index].ChainLength = chain.Count;
            }
        }
    }

    private static EncounterPatternStats BuildStats(
        IReadOnlyList<RhythmEventResource> events,
        double loopLength)
    {
        var playerAttacks = 0;
        var enemyAttacks = 0;
        var halfBeats = 0;
        var feints = 0;
        var fades = 0;
        var decoys = 0;
        var burstIds = new HashSet<int>();
        var chains = new Dictionary<int, List<RhythmEventResource>>();

        foreach (var rhythmEvent in events)
        {
            if (rhythmEvent.GetPromptType() == RhythmPromptType.EnemyAttack)
            {
                enemyAttacks++;
            }
            else if (rhythmEvent.GetPromptType() == RhythmPromptType.PlayerAttack)
            {
                playerAttacks++;
            }

            if (IsHalfBeat(rhythmEvent.BeatOffset))
            {
                halfBeats++;
            }

            if (rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.Feint)
            {
                feints++;
            }
            else if (rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.FadeBeforeTarget)
            {
                fades++;
            }
            else if (rhythmEvent.GetPromptBehavior() == RhythmPromptBehavior.Decoy)
            {
                decoys++;
            }

            if (rhythmEvent.ChainId > 0)
            {
                burstIds.Add(rhythmEvent.ChainId);
                if (!chains.TryGetValue(rhythmEvent.ChainId, out var chain))
                {
                    chain = new List<RhythmEventResource>();
                    chains.Add(rhythmEvent.ChainId, chain);
                }

                chain.Add(rhythmEvent);
            }
        }

        var sortedEvents = new List<RhythmEventResource>(events);
        sortedEvents.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));
        var minimumTargetSpacing = sortedEvents.Count == 0
            ? 0.0d
            : double.PositiveInfinity;
        var totalTargetSpacing = 0.0d;
        for (var index = 0; index < sortedEvents.Count; index++)
        {
            var nextIndex = (index + 1) % sortedEvents.Count;
            var nextBeat = nextIndex == 0
                ? sortedEvents[nextIndex].BeatOffset + loopLength
                : sortedEvents[nextIndex].BeatOffset;
            var spacing = nextBeat - sortedEvents[index].BeatOffset;
            minimumTargetSpacing = Math.Min(minimumTargetSpacing, spacing);
            totalTargetSpacing += spacing;
        }

        var maximumBurstLength = 0;
        var minimumBurstSpacing = double.PositiveInfinity;
        foreach (var chain in chains.Values)
        {
            chain.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));
            maximumBurstLength = Math.Max(maximumBurstLength, chain.Count);
            for (var index = 1; index < chain.Count; index++)
            {
                minimumBurstSpacing = Math.Min(
                    minimumBurstSpacing,
                    chain[index].BeatOffset - chain[index - 1].BeatOffset);
            }
        }

        return new EncounterPatternStats
        {
            EventCount = events.Count,
            PlayerAttackCount = playerAttacks,
            EnemyAttackCount = enemyAttacks,
            HalfBeatCount = halfBeats,
            BurstCount = burstIds.Count,
            FeintCount = feints,
            FadeCount = fades,
            DecoyCount = decoys,
            MaximumBurstLength = maximumBurstLength,
            MinimumTargetSpacingBeats = sortedEvents.Count == 0
                ? 0.0f
                : (float)minimumTargetSpacing,
            AverageTargetSpacingBeats = sortedEvents.Count == 0
                ? 0.0f
                : (float)(totalTargetSpacing / sortedEvents.Count),
            MinimumBurstSpacingBeats = double.IsPositiveInfinity(minimumBurstSpacing)
                ? 0.0f
                : (float)minimumBurstSpacing,
            MaximumActiveLogicalPrompts = sortedEvents.Count == 0 ? 0 : 1,
        };
    }

    private static bool IsHalfBeat(double beatPosition)
    {
        var fraction = beatPosition - Math.Floor(beatPosition);
        return Math.Abs(fraction - 0.5d) <= PositionTolerance;
    }

    private static bool IsStrongBeat(double beatPosition)
    {
        if (Math.Abs(beatPosition - Math.Round(beatPosition)) > PositionTolerance)
        {
            return false;
        }

        var beatInMeasure = ((int)Math.Round(beatPosition) % 4 + 4) % 4;
        return beatInMeasure == 0 || beatInMeasure == 2;
    }

    private static ulong GetStableHash(string value)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }
}

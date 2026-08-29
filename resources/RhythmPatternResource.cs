using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Padrão rítmico configurável por Resource. Os eventos permanecem fora do
/// controller para que ações, dano e futuras subdivisões sejam ajustáveis no
/// Inspector sem mover os dados para o código de combate.
/// </summary>
[GlobalClass]
public partial class RhythmPatternResource : Resource
{
    [Export]
    public string PatternName { get; set; } = "Rhythm Pattern";

    [Export(PropertyHint.Range, "0.25,4096.0,0.25")]
    public float LoopLengthBeats { get; set; } = 4.0f;

    [Export]
    public Godot.Collections.Array<RhythmEventResource> Events { get; set; } = new();

    [Export]
    public RhythmEventResource EventOne { get; set; } = null!;

    [Export]
    public RhythmEventResource EventTwo { get; set; } = null!;

    [Export]
    public RhythmEventResource EventThree { get; set; } = null!;

    [Export]
    public RhythmEventResource EventFour { get; set; } = null!;

    [Export]
    public RhythmEventResource EventFive { get; set; } = null!;

    [Export]
    public RhythmEventResource EventSix { get; set; } = null!;

    [Export]
    public RhythmEventResource EventSeven { get; set; } = null!;

    [Export]
    public RhythmEventResource EventEight { get; set; } = null!;

    public RhythmEventResource? GetEventAtBeat(int beatIndex)
    {
        var safeLoopLength = Math.Max(0.25d, LoopLengthBeats);
        var loopBeat = NormalizeBeatPosition(beatIndex, safeLoopLength);
        foreach (var rhythmEvent in EnumerateEvents())
        {
            var eventBeat = NormalizeBeatPosition(rhythmEvent.BeatOffset, safeLoopLength);

            if (Math.Abs(eventBeat - loopBeat) <= 0.01f)
            {
                return rhythmEvent;
            }
        }

        return null;
    }

    public List<RhythmEventResource> GetEventsSorted()
    {
        var uniqueEvents = new HashSet<RhythmEventResource>();
        var sortedEvents = new List<RhythmEventResource>();
        foreach (var rhythmEvent in EnumerateEvents())
        {
            if (uniqueEvents.Add(rhythmEvent))
            {
                sortedEvents.Add(rhythmEvent);
            }
        }

        sortedEvents.Sort((left, right) => left.BeatOffset.CompareTo(right.BeatOffset));
        return sortedEvents;
    }

    public bool TryGetNextEvent(
        int startBeat,
        out int eventBeat,
        out RhythmEventResource? rhythmEvent)
    {
        if (TryGetNextEvent(
                (double)startBeat,
                out var eventBeatPosition,
                out rhythmEvent))
        {
            eventBeat = (int)Math.Round(eventBeatPosition);
            return true;
        }

        eventBeat = -1;
        return false;
    }

    public bool TryGetNextEvent(
        double startBeatPosition,
        out double eventBeatPosition,
        out RhythmEventResource? rhythmEvent)
    {
        var safeStartBeat = Math.Max(0.0d, startBeatPosition);
        var safeLoopLength = Math.Max(0.25d, LoopLengthBeats);
        var bestBeatPosition = double.PositiveInfinity;
        RhythmEventResource? bestEvent = null;

        foreach (var candidateEvent in EnumerateEvents())
        {
            var eventOffset = NormalizeBeatPosition(
                candidateEvent.BeatOffset,
                safeLoopLength);
            var loopIndex = Math.Floor((safeStartBeat - eventOffset) / safeLoopLength);
            var candidateBeatPosition = loopIndex * safeLoopLength + eventOffset;
            if (candidateBeatPosition < safeStartBeat - 0.0001d)
            {
                candidateBeatPosition += safeLoopLength;
            }

            if (candidateBeatPosition < bestBeatPosition)
            {
                bestBeatPosition = candidateBeatPosition;
                bestEvent = candidateEvent;
            }
        }

        if (bestEvent != null)
        {
            eventBeatPosition = bestBeatPosition;
            rhythmEvent = bestEvent;
            return true;
        }

        eventBeatPosition = -1.0d;
        rhythmEvent = null;
        return false;
    }

    private IEnumerable<RhythmEventResource> EnumerateEvents()
    {
        foreach (var rhythmEvent in Events)
        {
            if (rhythmEvent != null)
            {
                yield return rhythmEvent;
            }
        }

        var legacyEvents = new[]
        {
            EventOne,
            EventTwo,
            EventThree,
            EventFour,
            EventFive,
            EventSix,
            EventSeven,
            EventEight,
        };

        foreach (var rhythmEvent in legacyEvents)
        {
            if (rhythmEvent != null)
            {
                yield return rhythmEvent;
            }
        }
    }

    private static double NormalizeBeatPosition(
        double beatPosition,
        double loopLength)
    {
        var normalized = beatPosition % loopLength;
        return normalized < 0.0d ? normalized + loopLength : normalized;
    }
}

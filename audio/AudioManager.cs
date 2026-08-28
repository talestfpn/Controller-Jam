using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Mapeia eventos de gameplay para áudio. Não decide regras de combate: apenas
/// toca streams configurados ou um fallback procedural quando o slot está vazio.
/// Os AudioStreamPlayers do pool pertencem à cena audio_manager.tscn.
/// </summary>
public partial class AudioManager : Node
{
    private enum AudioCue
    {
        PerfectAttack,
        GoodAttack,
        OkAttack,
        Miss,
        Parry,
        Block,
        PartialBlock,
        PlayerHit,
        PostureBreak,
        ExecutionReady,
        ExecutionSuccess,
        ExecutionFail,
        ComboMilestone,
        ComboBreak,
        PerfectStreak,
        Metronome,
    }

    private readonly struct CueProfile
    {
        public CueProfile(
            double startFrequency,
            double endFrequency,
            double secondaryFrequency,
            double duration,
            double decay,
            double amplitude,
            float volumeDb,
            double noiseAmount = 0.0d,
            double squareMix = 0.0d)
        {
            StartFrequency = startFrequency;
            EndFrequency = endFrequency;
            SecondaryFrequency = secondaryFrequency;
            Duration = duration;
            Decay = decay;
            Amplitude = amplitude;
            VolumeDb = volumeDb;
            NoiseAmount = noiseAmount;
            SquareMix = squareMix;
        }

        public double StartFrequency { get; }
        public double EndFrequency { get; }
        public double SecondaryFrequency { get; }
        public double Duration { get; }
        public double Decay { get; }
        public double Amplitude { get; }
        public float VolumeDb { get; }
        public double NoiseAmount { get; }
        public double SquareMix { get; }
    }

    [Export]
    public AudioFeedbackConfig FeedbackConfig { get; set; } = null!;

    [Export]
    public bool EnableMetronomeFallback { get; set; } = true;

    [Export(PropertyHint.Range, "-80.0,6.0,0.1")]
    public float MasterVolumeDb { get; set; }

    [Export(PropertyHint.Range, "-80.0,6.0,0.1")]
    public float MusicVolumeDb { get; set; } = -10.0f;

    [Export(PropertyHint.Range, "-80.0,6.0,0.1")]
    public float SfxVolumeDb { get; set; } = -3.0f;

    [Export(PropertyHint.Range, "-80.0,6.0,0.1")]
    public float UiVolumeDb { get; set; } = -3.0f;

    private readonly List<AudioStreamPlayer> _sfxPlayers = new();
    private readonly List<AudioStreamGenerator?> _fallbackGenerators = new();
    private RhythmManager _rhythmManager = null!;
    private int _nextPlayerIndex;

    public string LastSfxName { get; private set; } = "--";

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        foreach (var child in GetChildren())
        {
            if (child is not AudioStreamPlayer player)
            {
                continue;
            }

            _sfxPlayers.Add(player);
            _fallbackGenerators.Add(player.Stream as AudioStreamGenerator);
        }

        ApplyBusVolumes();
        _rhythmManager.BeatStarted += OnBeatStarted;
    }

    public override void _ExitTree()
    {
        if (_rhythmManager != null)
        {
            _rhythmManager.BeatStarted -= OnBeatStarted;
        }
    }

    public void PlayTimingFeedback(
        RhythmPromptType actionType,
        TimingResult timingResult)
    {
        var cue = actionType switch
        {
            RhythmPromptType.PlayerAttack => timingResult switch
            {
                TimingResult.Perfect => AudioCue.PerfectAttack,
                TimingResult.Good => AudioCue.GoodAttack,
                TimingResult.Ok => AudioCue.OkAttack,
                _ => AudioCue.Miss,
            },
            RhythmPromptType.EnemyAttack => timingResult switch
            {
                TimingResult.Perfect => AudioCue.Parry,
                TimingResult.Good => AudioCue.Block,
                TimingResult.Ok => AudioCue.PartialBlock,
                _ => AudioCue.PlayerHit,
            },
            RhythmPromptType.Execution => timingResult == TimingResult.Perfect
                ? AudioCue.ExecutionSuccess
                : AudioCue.ExecutionFail,
            _ => AudioCue.Miss,
        };

        PlayCue(cue);
    }

    public void PlayPostureBreak()
    {
        PlayCue(AudioCue.PostureBreak);
    }

    public void PlayExecutionReady()
    {
        PlayCue(AudioCue.ExecutionReady);
    }

    public void PlayComboMilestone(int comboTier)
    {
        var safeTier = Math.Clamp(comboTier, 1, 3);
        var pitchScale = 1.0f + (safeTier - 1) * 0.10f;
        PlayCue(AudioCue.ComboMilestone, pitchScale);
    }

    public void PlayComboBreak()
    {
        PlayCue(AudioCue.ComboBreak);
    }

    public void PlayPerfectStreak(int perfectStreak)
    {
        if (perfectStreak < 2)
        {
            return;
        }

        var safeStreak = Math.Min(perfectStreak, 8);
        var pitchScale = 1.0f + (safeStreak - 2) * 0.015f;
        PlayCue(AudioCue.PerfectStreak, pitchScale);
    }

    public void ApplyBusVolumes()
    {
        SetBusVolume("Master", MasterVolumeDb);
        SetBusVolume("Music", MusicVolumeDb);
        SetBusVolume("SFX", SfxVolumeDb);
        SetBusVolume("UI", UiVolumeDb);
    }

    private void OnBeatStarted(int beatIndex, double beatTime)
    {
        if (EnableMetronomeFallback && !_rhythmManager.UsingAudioClock)
        {
            var isMeasureStart = beatIndex % Math.Max(1, _rhythmManager.BeatsPerMeasure) == 0;
            PlayCue(AudioCue.Metronome, isMeasureStart ? 1.12f : 1.0f);
        }
    }

    private void PlayCue(AudioCue cue, float pitchScale = 1.0f)
    {
        LastSfxName = GetCueName(cue);
        if (_sfxPlayers.Count == 0)
        {
            return;
        }

        var playerIndex = GetNextPlayerIndex();
        var player = _sfxPlayers[playerIndex];
        var configuredStream = GetConfiguredStream(cue);
        var fallbackGenerator = _fallbackGenerators[playerIndex];
        var profile = GetCueProfile(cue);

        player.Stop();
        player.VolumeDb = profile.VolumeDb;
        if (configuredStream != null)
        {
            player.Stream = configuredStream;
            player.PitchScale = pitchScale;
            player.Play();
            return;
        }

        if (fallbackGenerator == null)
        {
            return;
        }

        player.Stream = fallbackGenerator;
        player.PitchScale = 1.0f;
        player.Play();
        FillFallbackGenerator(player, fallbackGenerator, profile, pitchScale);
    }

    private int GetNextPlayerIndex()
    {
        for (var index = 0; index < _sfxPlayers.Count; index++)
        {
            if (!_sfxPlayers[index].Playing)
            {
                return index;
            }
        }

        var selectedIndex = _nextPlayerIndex % _sfxPlayers.Count;
        _nextPlayerIndex = (_nextPlayerIndex + 1) % _sfxPlayers.Count;
        return selectedIndex;
    }

    private AudioStream? GetConfiguredStream(AudioCue cue)
    {
        if (FeedbackConfig == null)
        {
            return null;
        }

        return cue switch
        {
            AudioCue.PerfectAttack => FeedbackConfig.PerfectAttackSfx,
            AudioCue.GoodAttack => FeedbackConfig.GoodAttackSfx,
            AudioCue.OkAttack => FeedbackConfig.OkAttackSfx,
            AudioCue.Miss => FeedbackConfig.MissSfx,
            AudioCue.Parry => FeedbackConfig.ParrySfx,
            AudioCue.Block => FeedbackConfig.BlockSfx,
            AudioCue.PartialBlock => FeedbackConfig.PartialBlockSfx,
            AudioCue.PlayerHit => FeedbackConfig.PlayerHitSfx,
            AudioCue.PostureBreak => FeedbackConfig.PostureBreakSfx,
            AudioCue.ExecutionReady => FeedbackConfig.ExecutionReadySfx,
            AudioCue.ExecutionSuccess => FeedbackConfig.ExecutionSuccessSfx,
            AudioCue.ExecutionFail => FeedbackConfig.ExecutionFailSfx,
            AudioCue.ComboMilestone => FeedbackConfig.ComboMilestoneSfx,
            AudioCue.ComboBreak => FeedbackConfig.ComboBreakSfx,
            AudioCue.PerfectStreak => FeedbackConfig.PerfectStreakSfx,
            AudioCue.Metronome => FeedbackConfig.MetronomeSfx,
            _ => null,
        };
    }

    private static string GetCueName(AudioCue cue)
    {
        return cue switch
        {
            AudioCue.PerfectAttack => "PerfectAttackSfx",
            AudioCue.GoodAttack => "GoodAttackSfx",
            AudioCue.OkAttack => "OkAttackSfx",
            AudioCue.Miss => "MissSfx",
            AudioCue.Parry => "ParrySfx",
            AudioCue.Block => "BlockSfx",
            AudioCue.PartialBlock => "PartialBlockSfx",
            AudioCue.PlayerHit => "PlayerHitSfx",
            AudioCue.PostureBreak => "PostureBreakSfx",
            AudioCue.ExecutionReady => "ExecutionReadySfx",
            AudioCue.ExecutionSuccess => "ExecutionSuccessSfx",
            AudioCue.ExecutionFail => "ExecutionFailSfx",
            AudioCue.ComboMilestone => "ComboMilestoneSfx",
            AudioCue.ComboBreak => "ComboBreakSfx",
            AudioCue.PerfectStreak => "PerfectStreakSfx",
            AudioCue.Metronome => "MetronomeSfx",
            _ => "UnknownSfx",
        };
    }

    private static CueProfile GetCueProfile(AudioCue cue)
    {
        return cue switch
        {
            AudioCue.PerfectAttack => new CueProfile(
                720.0, 1100.0, 1440.0, 0.14, 18.0, 0.22, -1.0f, 0.02),
            AudioCue.GoodAttack => new CueProfile(
                560.0, 620.0, 0.0, 0.12, 16.0, 0.17, -2.0f),
            AudioCue.OkAttack => new CueProfile(
                330.0, 260.0, 0.0, 0.14, 12.0, 0.16, -2.5f, 0.03, 0.08),
            AudioCue.Miss => new CueProfile(
                210.0, 80.0, 0.0, 0.18, 9.0, 0.18, -1.5f, 0.18, 0.15),
            AudioCue.Parry => new CueProfile(
                1250.0, 1750.0, 2300.0, 0.18, 21.0, 0.27, 0.0f, 0.08, 0.08),
            AudioCue.Block => new CueProfile(
                230.0, 160.0, 70.0, 0.16, 12.0, 0.23, -1.0f, 0.12, 0.22),
            AudioCue.PartialBlock => new CueProfile(
                260.0, 115.0, 95.0, 0.19, 10.0, 0.22, -1.0f, 0.20, 0.18),
            AudioCue.PlayerHit => new CueProfile(
                135.0, 48.0, 65.0, 0.24, 8.0, 0.30, 0.0f, 0.28, 0.18),
            AudioCue.PostureBreak => new CueProfile(
                520.0, 85.0, 1600.0, 0.30, 7.0, 0.32, 0.0f, 0.40, 0.25),
            AudioCue.ExecutionReady => new CueProfile(
                620.0, 930.0, 1240.0, 0.18, 14.0, 0.19, -2.0f),
            AudioCue.ExecutionSuccess => new CueProfile(
                110.0, 42.0, 1050.0, 0.36, 6.5, 0.36, 0.0f, 0.22, 0.18),
            AudioCue.ExecutionFail => new CueProfile(
                250.0, 75.0, 130.0, 0.24, 8.5, 0.20, -1.0f, 0.18, 0.15),
            AudioCue.ComboMilestone => new CueProfile(
                650.0, 980.0, 1300.0, 0.20, 13.0, 0.20, -1.0f),
            AudioCue.ComboBreak => new CueProfile(
                310.0, 90.0, 145.0, 0.22, 8.5, 0.23, -1.0f, 0.12, 0.12),
            AudioCue.PerfectStreak => new CueProfile(
                850.0, 1250.0, 1700.0, 0.13, 19.0, 0.15, -2.5f),
            AudioCue.Metronome => new CueProfile(
                1000.0, 760.0, 0.0, 0.045, 60.0, 0.10, -5.0f, 0.10, 0.25),
            _ => new CueProfile(
                220.0, 180.0, 0.0, 0.10, 12.0, 0.12, -3.0f),
        };
    }

    private static void FillFallbackGenerator(
        AudioStreamPlayer player,
        AudioStreamGenerator generator,
        CueProfile profile,
        float pitchScale)
    {
        if (player.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
        {
            return;
        }

        var sampleRate = Math.Max(8000.0, generator.MixRate);
        var frameCount = Math.Max(1, (int)Math.Round(profile.Duration * sampleRate));
        var angularFrequency = Math.PI * 2.0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            if (!playback.CanPushBuffer(1))
            {
                break;
            }

            var time = frame / sampleRate;
            var progress = frameCount <= 1
                ? 1.0d
                : frame / (double)(frameCount - 1);
            var envelope = Math.Exp(-time * profile.Decay);
            var sweptPhase = angularFrequency * pitchScale *
                (profile.StartFrequency * time +
                 0.5d * (profile.EndFrequency - profile.StartFrequency) *
                 time * progress);
            var sine = Math.Sin(sweptPhase);
            var square = Math.Sign(sine);
            var primary = sine * (1.0d - profile.SquareMix) +
                square * profile.SquareMix;
            var secondary = profile.SecondaryFrequency > 0.0
                ? Math.Sin(angularFrequency * profile.SecondaryFrequency * pitchScale * time) * 0.45
                : 0.0;
            var pseudoNoise = Math.Sin((frame + 1.0d) * 12.9898d) * 43758.5453d;
            pseudoNoise = (pseudoNoise - Math.Floor(pseudoNoise)) * 2.0d - 1.0d;
            var sample = (primary + secondary + pseudoNoise * profile.NoiseAmount) *
                profile.Amplitude * envelope;
            playback.PushFrame(new Vector2((float)sample, (float)sample));
        }
    }

    private static void SetBusVolume(string busName, float volumeDb)
    {
        var busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex >= 0)
        {
            AudioServer.SetBusVolumeDb(busIndex, volumeDb);
        }
    }
}

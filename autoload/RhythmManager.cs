using System;
using Godot;

/// <summary>
/// Relógio central do protótipo. Usa o relógio do áudio quando existe um stream
/// configurado e recorre ao relógio monotônico apenas para a cena sem música.
/// </summary>
public partial class RhythmManager : Node
{
    [Signal]
    public delegate void BeatStartedEventHandler(int beatIndex, double beatTime);

    [Signal]
    public delegate void BeatFinishedEventHandler(int beatIndex, double beatTime);

    [Signal]
    public delegate void MeasureStartedEventHandler(int measureIndex, double beatTime);

    [Signal]
    public delegate void SubdivisionTriggeredEventHandler(int subdivisionIndex, double subdivisionTime);

    [Export]
    public MusicTrackResource MusicTrack { get; set; } = null!;

    [Export(PropertyHint.Range, "20.0,300.0,1.0")]
    public float Bpm { get; set; } = 120.0f;

    [Export(PropertyHint.Range, "1,16,1")]
    public int BeatsPerMeasure { get; set; } = 4;

    [Export(PropertyHint.Range, "1,8,1")]
    public int SubdivisionsPerBeat { get; set; } = 1;

    [Export(PropertyHint.Range, "0.0,5.0,0.01")]
    public float InitialOffsetSeconds { get; set; } = 0.0f;

    [Export]
    public bool AutoStart { get; set; } = false;

    public double BeatDuration => 60.0d / Math.Max(1.0d, Bpm);
    public float MusicOffsetMs => MusicTrack != null
        ? MusicTrack.MusicOffsetMs
        : InitialOffsetSeconds * 1000.0f;
    public int CurrentBeat { get; private set; } = -1;
    public int CurrentSubdivision { get; private set; } = -1;
    public double MusicPosition { get; private set; } = double.NegativeInfinity;
    public bool IsRunning { get; private set; }
    public bool UsingAudioClock { get; private set; }
    public bool MusicPlaying => _audioPlayer != null && _audioPlayer.Playing;
    public bool MusicStreamConfigured => _audioPlayer != null && _audioPlayer.Stream != null;
    public double BeatPhase
    {
        get
        {
            var position = GetMusicPosition();
            if (position < 0.0d)
            {
                return 0.0d;
            }

            var beatPosition = position / BeatDuration;
            return beatPosition - Math.Floor(beatPosition);
        }
    }

    private AudioStreamPlayer _audioPlayer = null!;
    private ulong _clockStartUsec;
    private double _audioLoopOffsetSeconds;
    private double _lastAudioPlaybackPosition;
    private double _audioStreamLengthSeconds;

    public override void _Ready()
    {
        _audioPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");
        _audioPlayer.Finished += OnMusicFinished;
        ApplyMusicTrackConfiguration();

        if (AutoStart)
        {
            Start();
        }
    }

    public override void _ExitTree()
    {
        if (_audioPlayer != null)
        {
            _audioPlayer.Finished -= OnMusicFinished;
        }
    }

    public override void _Process(double delta)
    {
        if (!IsRunning)
        {
            return;
        }

        MusicPosition = GetMusicPosition();
        if (MusicPosition < 0.0d)
        {
            return;
        }

        EmitPendingBeats(MusicPosition);
        EmitPendingSubdivisions(MusicPosition);
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        ApplyMusicTrackConfiguration();
        _clockStartUsec = Time.GetTicksUsec();
        _audioLoopOffsetSeconds = 0.0d;
        _lastAudioPlaybackPosition = 0.0d;
        _audioStreamLengthSeconds = GetAudioStreamLengthSeconds();
        CurrentBeat = -1;
        CurrentSubdivision = -1;
        MusicPosition = -GetMusicOffsetSeconds();
        UsingAudioClock = _audioPlayer.Stream != null;

        if (UsingAudioClock)
        {
            _audioPlayer.Play();
        }

        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        if (_audioPlayer.Playing)
        {
            _audioPlayer.Stop();
        }

        IsRunning = false;
        UsingAudioClock = false;
    }

    public double GetMusicPosition()
    {
        if (!IsRunning)
        {
            return MusicPosition;
        }

        if (UsingAudioClock && _audioPlayer.Playing)
        {
            var audioPosition = Math.Max(0.0d, _audioPlayer.GetPlaybackPosition());
            if (_lastAudioPlaybackPosition - audioPosition > 0.05d)
            {
                _audioLoopOffsetSeconds += _audioStreamLengthSeconds > 0.0d
                    ? _audioStreamLengthSeconds
                    : _lastAudioPlaybackPosition;
            }

            _lastAudioPlaybackPosition = audioPosition;
            var mixCorrection = AudioServer.GetTimeSinceLastMix();
            return _audioLoopOffsetSeconds + audioPosition + mixCorrection -
                GetMusicOffsetSeconds();
        }

        if (UsingAudioClock)
        {
            return _audioLoopOffsetSeconds + _lastAudioPlaybackPosition -
                GetMusicOffsetSeconds();
        }

        var elapsedSeconds = (Time.GetTicksUsec() - _clockStartUsec) / 1_000_000.0d;
        return elapsedSeconds - GetMusicOffsetSeconds();
    }

    public double GetBeatTime(int beatIndex)
    {
        return GetBeatTime((double)beatIndex);
    }

    public double GetBeatTime(double beatPosition)
    {
        return Math.Max(0.0d, beatPosition) * BeatDuration;
    }

    public double GetSubdivisionTime(int subdivisionIndex)
    {
        var safeSubdivisions = Math.Max(1, SubdivisionsPerBeat);
        return Math.Max(0, subdivisionIndex) * (BeatDuration / safeSubdivisions);
    }

    private void EmitPendingBeats(double musicPosition)
    {
        var beatIndexToEmit = (int)Math.Floor((musicPosition + 0.0000001d) / BeatDuration);
        if (beatIndexToEmit <= CurrentBeat)
        {
            return;
        }

        for (var beatIndex = CurrentBeat + 1; beatIndex <= beatIndexToEmit; beatIndex++)
        {
            if (CurrentBeat >= 0)
            {
                EmitSignal(SignalName.BeatFinished, CurrentBeat, GetBeatTime(CurrentBeat));
            }

            CurrentBeat = beatIndex;

            var safeBeatsPerMeasure = Math.Max(1, BeatsPerMeasure);
            if (beatIndex % safeBeatsPerMeasure == 0)
            {
                var measureIndex = beatIndex / safeBeatsPerMeasure;
                EmitSignal(SignalName.MeasureStarted, measureIndex, GetBeatTime(beatIndex));
            }

            EmitSignal(SignalName.BeatStarted, beatIndex, GetBeatTime(beatIndex));
        }
    }

    private void EmitPendingSubdivisions(double musicPosition)
    {
        var safeSubdivisions = Math.Max(1, SubdivisionsPerBeat);
        var subdivisionDuration = BeatDuration / safeSubdivisions;
        var subdivisionIndexToEmit = (int)Math.Floor((musicPosition + 0.0000001d) / subdivisionDuration);
        if (subdivisionIndexToEmit <= CurrentSubdivision)
        {
            return;
        }

        for (var subdivisionIndex = CurrentSubdivision + 1;
             subdivisionIndex <= subdivisionIndexToEmit;
             subdivisionIndex++)
        {
            CurrentSubdivision = subdivisionIndex;
            EmitSignal(
                SignalName.SubdivisionTriggered,
                subdivisionIndex,
                GetSubdivisionTime(subdivisionIndex));
        }
    }

    private void ApplyMusicTrackConfiguration()
    {
        if (MusicTrack == null)
        {
            return;
        }

        Bpm = Math.Max(1.0f, MusicTrack.Bpm);
        BeatsPerMeasure = Math.Max(1, MusicTrack.TimeSignatureNumerator);
        _audioPlayer.Stream = MusicTrack.AudioStream;
    }

    private double GetMusicOffsetSeconds()
    {
        return MusicOffsetMs / 1000.0d;
    }

    private double GetAudioStreamLengthSeconds()
    {
        return _audioPlayer.Stream == null
            ? 0.0d
            : Math.Max(0.0d, _audioPlayer.Stream.GetLength());
    }

    private void OnMusicFinished()
    {
        if (!IsRunning || MusicTrack == null || !MusicTrack.LoopEnabled ||
            _audioPlayer.Stream == null)
        {
            return;
        }

        _audioLoopOffsetSeconds += _audioStreamLengthSeconds;
        _lastAudioPlaybackPosition = 0.0d;
        _audioPlayer.Play();
    }
}

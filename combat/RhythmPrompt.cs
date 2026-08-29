using System;
using Godot;

/// <summary>
/// Prompt visual reutilizável. A cena fornece os nós visuais; este script apenas
/// anima, recebe o input e comunica o julgamento.
/// </summary>
public partial class RhythmPrompt : Node2D
{
    [Signal]
    public delegate void ResolvedEventHandler(
        int result,
        float precisionPercent,
        float offsetMilliseconds,
        string direction,
        string resultName);

    [Export]
    public TimingConfig TimingConfig { get; set; } = new();

    [Export(PropertyHint.Range, "1.0,4.0,0.05")]
    public float OuterStartScale { get; set; } = 2.4f;

    [Export(PropertyHint.Range, "0.5,1.0,0.01")]
    public float CircleScaleAtTarget { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.01,0.9,0.01")]
    public float OuterEndScale { get; set; } = 0.35f;

    [Export]
    public string PromptTitle { get; set; } = "RHYTHM ACTION";

    private Line2D _outerRing = null!;
    private Line2D _decoyRing = null!;
    private Line2D _targetRing = null!;
    private Polygon2D _centerDot = null!;
    private Label _promptLabel = null!;
    private Label _feedbackLabel = null!;
    private Timer _resolveTimer = null!;
    private RhythmManager _rhythmManager = null!;

    private double _targetTime;
    private double _startTime;
    private double _beatDuration;
    private float _fadeBeatsBeforeTarget = 0.5f;
    private float _feintStrength = 0.24f;
    private RhythmPromptBehavior _behavior = RhythmPromptBehavior.Normal;
    private bool _isConfigured;
    private bool _isResolved;

    public double TargetTime => _targetTime;
    public double StartTime => _startTime;
    public bool IsResolved => _isResolved;
    public RhythmPromptBehavior Behavior => _behavior;

    public override void _Ready()
    {
        _outerRing = GetNode<Line2D>("OuterRing");
        _decoyRing = GetNode<Line2D>("DecoyAnchor/DecoyRing");
        _targetRing = GetNode<Line2D>("TargetRing");
        _centerDot = GetNode<Polygon2D>("CenterDot");
        _promptLabel = GetNode<Label>("PromptLabel");
        _feedbackLabel = GetNode<Label>("FeedbackLabel");
        _resolveTimer = GetNode<Timer>("ResolveTimer");
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");

        _resolveTimer.Timeout += OnResolveTimerTimeout;
        _feedbackLabel.Visible = false;
        _promptLabel.Text = PromptTitle;
        _outerRing.Scale = Vector2.One * OuterStartScale;
    }

    public override void _ExitTree()
    {
        if (_resolveTimer != null)
        {
            _resolveTimer.Timeout -= OnResolveTimerTimeout;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isConfigured || _isResolved)
        {
            return;
        }

        var currentTime = _rhythmManager.GetMusicPosition();
        var currentScale = GetVisualScaleAtTime(currentTime);
        _outerRing.Scale = Vector2.One * currentScale;
        _outerRing.Visible = !ShouldHideOuterRing(currentTime);
        _decoyRing.Visible = IsDecoyVisibleAt(currentTime);
        if (_decoyRing.Visible)
        {
            _decoyRing.Scale = Vector2.One * GetDecoyScaleAtTime(currentTime);
        }

        var windowSeconds = GetJudgementWindowSeconds();
        if (currentTime > _targetTime + windowSeconds)
        {
            Resolve(TimingJudge.JudgeByCircleSize(
                currentTime,
                _targetTime,
                _beatDuration,
                TimingConfig,
                GetJudgementScaleAtTime(currentTime) * 100.0f));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_isConfigured || _isResolved || !@event.IsActionPressed("rhythm_action"))
        {
            return;
        }

        var inputTime = _rhythmManager.GetMusicPosition();
        ResolveInputAt(inputTime);
        GetViewport().SetInputAsHandled();
    }

    public void Configure(double targetTime, double beatDuration)
    {
        Configure(
            targetTime,
            beatDuration,
            RhythmPromptBehavior.Normal,
            1.0f,
            0.5f,
            0.24f);
    }

    public void Configure(
        double targetTime,
        double beatDuration,
        RhythmPromptBehavior behavior,
        float telegraphBeats,
        float fadeBeatsBeforeTarget,
        float feintStrength)
    {
        _targetTime = targetTime;
        _beatDuration = Math.Max(0.001d, beatDuration);
        _startTime = _targetTime -
            (_beatDuration * Math.Max(0.5f, telegraphBeats));
        _behavior = behavior;
        _fadeBeatsBeforeTarget = Math.Max(0.1f, fadeBeatsBeforeTarget);
        _feintStrength = Mathf.Clamp(feintStrength, 0.05f, 0.6f);
        _isConfigured = true;
        _isResolved = false;

        _outerRing.Visible = true;
        _decoyRing.Visible = false;
        _targetRing.Visible = true;
        _centerDot.Visible = true;
        _feedbackLabel.Visible = false;
        _outerRing.Scale = Vector2.One * OuterStartScale;
    }

    public void SetPromptPresentation(
        string title,
        Color color,
        float outerRingWidth = 5.0f,
        float centerDotScale = 1.0f)
    {
        _promptLabel.Text = title;
        _promptLabel.Modulate = color;
        _outerRing.Modulate = color;
        var decoyColor = color.Lerp(Colors.White, 0.35f);
        _decoyRing.Modulate = new Color(
            decoyColor.R,
            decoyColor.G,
            decoyColor.B,
            0.55f);
        _outerRing.Width = outerRingWidth;
        _centerDot.Scale = Vector2.One * centerDotScale;
    }

    public void ResolveInputAt(double inputTime)
    {
        if (!_isConfigured || _isResolved)
        {
            return;
        }

        Resolve(TimingJudge.JudgeByCircleSize(
            inputTime,
            _targetTime,
            _beatDuration,
            TimingConfig,
            GetJudgementScaleAtTime(inputTime) * 100.0f));
    }

    public void ForceMiss()
    {
        if (!_isConfigured || _isResolved)
        {
            return;
        }

        var missTime = _targetTime + GetJudgementWindowSeconds() + 0.001d;
        Resolve(TimingJudge.JudgeByCircleSize(
            missTime,
            _targetTime,
            _beatDuration,
            TimingConfig,
            GetJudgementScaleAtTime(missTime) * 100.0f));
    }

    public float GetCircleSizePercentAt(double time)
    {
        return GetVisualScaleAtTime(time) * 100.0f;
    }

    public float GetJudgementCircleSizePercentAt(double time)
    {
        return GetJudgementScaleAtTime(time) * 100.0f;
    }

    public float GetDecoyCircleSizePercentAt(double time)
    {
        return GetDecoyScaleAtTime(time) * 100.0f;
    }

    public bool IsOuterRingVisibleAt(double time)
    {
        return !ShouldHideOuterRing(time);
    }

    public bool IsDecoyVisibleAt(double time)
    {
        return _behavior == RhythmPromptBehavior.Decoy && time < _targetTime;
    }

    private void Resolve(TimingJudgement judgement)
    {
        if (_isResolved)
        {
            return;
        }

        _isResolved = true;
        _outerRing.Visible = false;
        _decoyRing.Visible = false;
        _targetRing.Visible = false;
        _centerDot.Visible = false;
        _feedbackLabel.Visible = true;
        _feedbackLabel.Text =
            $"{TimingJudge.GetDisplayName(judgement.Result)}\n" +
            $"{TimingJudge.FormatOffsetMilliseconds(judgement.OffsetMilliseconds)}";

        EmitSignal(
            SignalName.Resolved,
            (int)judgement.Result,
            (float)judgement.PrecisionPercent,
            (float)judgement.OffsetMilliseconds,
            TimingJudge.GetDirectionDisplayName(judgement.Direction),
            TimingJudge.GetDisplayName(judgement.Result));

        _resolveTimer.Start();
    }

    private double GetJudgementWindowSeconds()
    {
        var safeConfig = TimingConfig ?? new TimingConfig();
        return Math.Max(0.001d, _beatDuration * Math.Max(0.001f, safeConfig.JudgementWindowBeats));
    }

    private float GetJudgementScaleAtTime(double currentTime)
    {
        if (currentTime <= _targetTime)
        {
            var preTargetDuration = Math.Max(0.001d, _targetTime - _startTime);
            var preTargetProgress = Mathf.Clamp(
                (float)((currentTime - _startTime) / preTargetDuration),
                0.0f,
                1.0f);
            return Mathf.Lerp(OuterStartScale, CircleScaleAtTarget, preTargetProgress);
        }

        var postTargetProgress = Mathf.Clamp(
            (float)((currentTime - _targetTime) / GetJudgementWindowSeconds()),
            0.0f,
            1.0f);
        return Mathf.Lerp(CircleScaleAtTarget, OuterEndScale, postTargetProgress);
    }

    private float GetVisualScaleAtTime(double currentTime)
    {
        var judgementScale = GetJudgementScaleAtTime(currentTime);
        if (_behavior != RhythmPromptBehavior.Feint || currentTime >= _targetTime)
        {
            return judgementScale;
        }

        var preTargetDuration = Math.Max(0.001d, _targetTime - _startTime);
        var progress = Mathf.Clamp(
            (float)((currentTime - _startTime) / preTargetDuration),
            0.0f,
            1.0f);
        const float feintStart = 0.58f;
        const float firstShrinkEnd = 0.65f;
        const float feintPeak = 0.72f;
        if (progress <= feintStart)
        {
            return judgementScale;
        }

        var firstShrinkScale = GetJudgementScaleForProgress(firstShrinkEnd) - 0.02f;
        var feintPeakScale = firstShrinkScale + _feintStrength;
        if (progress <= feintPeak)
        {
            if (progress <= firstShrinkEnd)
            {
                return SmoothLerp(
                    GetJudgementScaleForProgress(feintStart),
                    firstShrinkScale,
                    (progress - feintStart) / (firstShrinkEnd - feintStart));
            }

            return SmoothLerp(
                firstShrinkScale,
                feintPeakScale,
                (progress - firstShrinkEnd) / (feintPeak - firstShrinkEnd));
        }

        return SmoothLerp(
            feintPeakScale,
            CircleScaleAtTarget,
            (progress - feintPeak) / (1.0f - feintPeak));
    }

    private float GetJudgementScaleForProgress(float progress)
    {
        var clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (clampedProgress <= 0.0f)
        {
            return OuterStartScale;
        }

        if (clampedProgress >= 1.0f)
        {
            return CircleScaleAtTarget;
        }

        return Mathf.Lerp(
            OuterStartScale,
            CircleScaleAtTarget,
            clampedProgress);
    }

    private float GetDecoyScaleAtTime(double currentTime)
    {
        if (currentTime >= _targetTime)
        {
            return GetJudgementScaleAtTime(currentTime);
        }

        var preTargetDuration = Math.Max(0.001d, _targetTime - _startTime);
        var progress = Mathf.Clamp(
            (float)((currentTime - _startTime) / preTargetDuration),
            0.0f,
            1.0f);
        const float decoyTargetProgress = 0.78f;
        if (progress <= decoyTargetProgress)
        {
            return SmoothLerp(
                OuterStartScale,
                CircleScaleAtTarget,
                progress / decoyTargetProgress);
        }

        return SmoothLerp(
            CircleScaleAtTarget,
            0.78f,
            (progress - decoyTargetProgress) / (1.0f - decoyTargetProgress));
    }

    private static float SmoothLerp(float from, float to, float weight)
    {
        var clampedWeight = Mathf.Clamp(weight, 0.0f, 1.0f);
        var smoothWeight = clampedWeight * clampedWeight *
            (3.0f - 2.0f * clampedWeight);
        return Mathf.Lerp(from, to, smoothWeight);
    }

    private bool ShouldHideOuterRing(double currentTime)
    {
        if (_behavior != RhythmPromptBehavior.FadeBeforeTarget)
        {
            return false;
        }

        var fadeTime = _targetTime - (_beatDuration * _fadeBeatsBeforeTarget);
        return currentTime >= fadeTime;
    }

    private void OnResolveTimerTimeout()
    {
        QueueFree();
    }
}

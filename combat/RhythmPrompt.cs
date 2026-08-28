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
    private Line2D _targetRing = null!;
    private Polygon2D _centerDot = null!;
    private Label _promptLabel = null!;
    private Label _feedbackLabel = null!;
    private Timer _resolveTimer = null!;
    private RhythmManager _rhythmManager = null!;

    private double _targetTime;
    private double _startTime;
    private double _beatDuration;
    private bool _isConfigured;
    private bool _isResolved;

    public double TargetTime => _targetTime;
    public bool IsResolved => _isResolved;

    public override void _Ready()
    {
        _outerRing = GetNode<Line2D>("OuterRing");
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
        var currentScale = GetOuterScaleAtTime(currentTime);
        _outerRing.Scale = Vector2.One * currentScale;

        var windowSeconds = GetJudgementWindowSeconds();
        if (currentTime > _targetTime + windowSeconds)
        {
            Resolve(TimingJudge.JudgeByCircleSize(
                currentTime,
                _targetTime,
                _beatDuration,
                TimingConfig,
                currentScale * 100.0f));
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
        _targetTime = targetTime;
        _beatDuration = Math.Max(0.001d, beatDuration);
        _startTime = _targetTime - _beatDuration;
        _isConfigured = true;
        _isResolved = false;

        _outerRing.Visible = true;
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
            GetOuterScaleAtTime(inputTime) * 100.0f));
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
            GetOuterScaleAtTime(missTime) * 100.0f));
    }

    public float GetCircleSizePercentAt(double time)
    {
        return GetOuterScaleAtTime(time) * 100.0f;
    }

    private void Resolve(TimingJudgement judgement)
    {
        if (_isResolved)
        {
            return;
        }

        _isResolved = true;
        _outerRing.Visible = false;
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

    private float GetOuterScaleAtTime(double currentTime)
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

    private void OnResolveTimerTimeout()
    {
        QueueFree();
    }
}

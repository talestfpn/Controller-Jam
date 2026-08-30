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

    [Export(PropertyHint.Range, "0.2,1.2,0.01")]
    public float ReactionDuration { get; set; } = 0.56f;

    private Line2D _outerRing = null!;
    private Line2D _decoyRing = null!;
    private Line2D _targetRing = null!;
    private ColorRect _outerWave = null!;
    private ColorRect _decoyWave = null!;
    private ColorRect _targetWave = null!;
    private ColorRect _reactionWave = null!;
    private AnimatedSprite2D _reactionSprite = null!;
    private ShaderMaterial _outerWaveMaterial = null!;
    private ShaderMaterial _decoyWaveMaterial = null!;
    private ShaderMaterial _reactionWaveMaterial = null!;
    private ShaderMaterial _reactionSpriteMaterial = null!;
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
    private RhythmPromptType _promptType = RhythmPromptType.PlayerAttack;
    private double _reactionElapsed;
    private Color _feedbackBaseColor = Colors.White;
    private Vector2 _reactionSpriteBaseScale;
    private Vector2 _activeReactionSpriteScale;
    private Vector2 _reactionSpriteBasePosition;
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
        _outerWave = GetNode<ColorRect>("OuterWave");
        _decoyWave = GetNode<ColorRect>("DecoyAnchor/DecoyWave");
        _targetWave = GetNode<ColorRect>("TargetWave");
        _reactionWave = GetNode<ColorRect>("ReactionWave");
        _reactionSprite = GetNode<AnimatedSprite2D>("ReactionSprite");
        _outerWaveMaterial = GetWaveMaterial(_outerWave);
        _decoyWaveMaterial = GetWaveMaterial(_decoyWave);
        _reactionWaveMaterial = GetWaveMaterial(_reactionWave);
        _reactionSpriteMaterial = GetShaderMaterial(_reactionSprite);
        _centerDot = GetNode<Polygon2D>("CenterDot");
        _promptLabel = GetNode<Label>("PromptLabel");
        _feedbackLabel = GetNode<Label>("FeedbackLabel");
        _resolveTimer = GetNode<Timer>("ResolveTimer");
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");

        _resolveTimer.Timeout += OnResolveTimerTimeout;
        _feedbackLabel.Visible = false;
        _reactionWave.Visible = false;
        _reactionSprite.Visible = false;
        _reactionSpriteBaseScale = _reactionSprite.Scale;
        _activeReactionSpriteScale = _reactionSpriteBaseScale;
        _reactionSpriteBasePosition = _reactionSprite.Position;
        _promptLabel.Text = PromptTitle;
        _outerRing.Scale = Vector2.One * OuterStartScale;
        _outerWave.Scale = Vector2.One * OuterStartScale;
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
        if (!_isConfigured)
        {
            return;
        }

        if (_isResolved)
        {
            UpdateReaction(delta);
            return;
        }

        var currentTime = _rhythmManager.GetMusicPosition();
        var currentScale = GetVisualScaleAtTime(currentTime);
        _outerRing.Scale = Vector2.One * currentScale;
        _outerWave.Scale = Vector2.One * currentScale;
        var hideOuterRing = ShouldHideOuterRing(currentTime);
        _outerRing.Visible = !hideOuterRing;
        _outerWave.Visible = !hideOuterRing;
        var decoyVisible = IsDecoyVisibleAt(currentTime);
        _decoyRing.Visible = decoyVisible;
        _decoyWave.Visible = decoyVisible;
        if (decoyVisible)
        {
            var decoyScale = Vector2.One * GetDecoyScaleAtTime(currentTime);
            _decoyRing.Scale = decoyScale;
            _decoyWave.Scale = decoyScale;
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
        _reactionElapsed = 0.0d;

        _outerRing.Visible = true;
        _outerWave.Visible = true;
        _decoyRing.Visible = false;
        _decoyWave.Visible = false;
        _targetRing.Visible = true;
        _targetWave.Visible = true;
        _centerDot.Visible = true;
        _reactionWave.Visible = false;
        _reactionSprite.Visible = false;
        _activeReactionSpriteScale = _reactionSpriteBaseScale;
        _reactionSprite.Scale = _reactionSpriteBaseScale;
        _reactionSprite.Position = _reactionSpriteBasePosition;
        _feedbackLabel.Visible = false;
        _feedbackLabel.Modulate = Colors.White;
        _reactionWaveMaterial.SetShaderParameter("reaction_progress", 0.0f);
        _reactionSpriteMaterial.SetShaderParameter("burn_progress", 0.0f);
        _outerRing.Scale = Vector2.One * OuterStartScale;
        _outerWave.Scale = Vector2.One * OuterStartScale;
    }

    public void SetPromptType(RhythmPromptType promptType)
    {
        _promptType = promptType;
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
        var secondaryColor = GetPresentationSecondaryColor(color);
        SetWavePalette(_outerWaveMaterial, color, secondaryColor);
        var decoyColor = color.Lerp(Colors.White, 0.35f);
        _decoyRing.Modulate = new Color(
            decoyColor.R,
            decoyColor.G,
            decoyColor.B,
            0.55f);
        SetWavePalette(
            _decoyWaveMaterial,
            new Color(decoyColor.R, decoyColor.G, decoyColor.B, 0.56f),
            new Color(secondaryColor.R, secondaryColor.G, secondaryColor.B, 0.48f));
        _outerRing.Width = Math.Max(2.0f, outerRingWidth * 0.55f);
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
        _outerWave.Visible = false;
        _decoyRing.Visible = false;
        _decoyWave.Visible = false;
        _targetRing.Visible = false;
        _targetWave.Visible = false;
        _centerDot.Visible = false;
        _reactionElapsed = 0.0d;
        ConfigureReaction(judgement.Result);
        _reactionWave.Visible = true;
        _feedbackLabel.Modulate = _feedbackBaseColor;
        _feedbackLabel.Text = _reactionSprite.Visible
            ? string.Empty
            : GetFeedbackDisplayName(judgement.Result);
        _feedbackLabel.Visible = !string.IsNullOrEmpty(_feedbackLabel.Text);

        EmitSignal(
            SignalName.Resolved,
            (int)judgement.Result,
            (float)judgement.PrecisionPercent,
            (float)judgement.OffsetMilliseconds,
            TimingJudge.GetDirectionDisplayName(judgement.Direction),
            TimingJudge.GetDisplayName(judgement.Result));

        _resolveTimer.WaitTime = Math.Max(0.2d, ReactionDuration);
        _resolveTimer.Start();
    }

    private void UpdateReaction(double delta)
    {
        _reactionElapsed += Math.Max(0.0d, delta);
        var safeDuration = Math.Max(0.001d, ReactionDuration);
        var progress = Mathf.Clamp(
            (float)(_reactionElapsed / safeDuration),
            0.0f,
            1.0f);
        _reactionWaveMaterial.SetShaderParameter("reaction_progress", progress);
        _reactionSpriteMaterial.SetShaderParameter("burn_progress", progress);

        if (_reactionSprite.Visible)
        {
            float scaleFactor;
            if (progress < 0.18f)
            {
                scaleFactor = SmoothLerp(0.72f, 1.12f, progress / 0.18f);
            }
            else if (progress < 0.34f)
            {
                scaleFactor = SmoothLerp(
                    1.12f,
                    1.0f,
                    (progress - 0.18f) / 0.16f);
            }
            else
            {
                var remaining = 1.0f - progress;
                scaleFactor = 1.0f +
                    Mathf.Sin(progress * Mathf.Tau * 2.0f) * 0.025f * remaining;
            }

            _reactionSprite.Scale = _activeReactionSpriteScale * scaleFactor;
            _reactionSprite.Position = _reactionSpriteBasePosition + new Vector2(
                0.0f,
                -SmoothLerp(0.0f, 7.0f, progress));
        }

        var labelFade = 1.0f - Mathf.Clamp(
            (progress - 0.62f) / 0.38f,
            0.0f,
            1.0f);
        _feedbackLabel.Modulate = new Color(
            _feedbackBaseColor.R,
            _feedbackBaseColor.G,
            _feedbackBaseColor.B,
            _feedbackBaseColor.A * labelFade);
    }

    private void ConfigureReaction(TimingResult result)
    {
        var isParry = _promptType == RhythmPromptType.EnemyAttack &&
            result == TimingResult.Perfect;
        var isBlock = _promptType == RhythmPromptType.EnemyAttack &&
            result == TimingResult.Good;
        var isExecution = _promptType == RhythmPromptType.Execution;
        var isExecutionSuccess = isExecution && result == TimingResult.Perfect;
        var reactionMode = isParry
            ? 4
            : result switch
            {
                TimingResult.Perfect => 1,
                TimingResult.Good => 2,
                TimingResult.Ok => 3,
                _ => 5,
            };

        Color primaryColor;
        Color secondaryColor;
        float waveAmplitude;
        float waveFrequency;
        float waveSpeed;
        float leftFalloff;
        float rightFalloff;

        switch (reactionMode)
        {
            case 1:
                primaryColor = isExecutionSuccess
                    ? new Color(0.68f, 1.0f, 0.08f, 1.0f)
                    : new Color(0.18f, 0.9f, 0.22f, 1.0f);
                secondaryColor = isExecutionSuccess
                    ? new Color(0.46f, 0.62f, 0.02f, 1.0f)
                    : new Color(0.02f, 0.46f, 0.07f, 1.0f);
                waveAmplitude = 0.03f;
                waveFrequency = 8.0f;
                waveSpeed = 4.2f;
                leftFalloff = 0.8f;
                rightFalloff = 1.25f;
                break;
            case 2:
                primaryColor = isBlock
                    ? new Color(1.0f, 0.58f, 0.03f, 1.0f)
                    : new Color(0.10f, 0.4f, 0.88f, 1.0f);
                secondaryColor = isBlock
                    ? new Color(0.88f, 0.28f, 0.0f, 1.0f)
                    : new Color(0.02f, 0.14f, 0.58f, 1.0f);
                waveAmplitude = 0.024f;
                waveFrequency = 6.0f;
                waveSpeed = 2.7f;
                leftFalloff = 1.0f;
                rightFalloff = 1.0f;
                break;
            case 3:
                primaryColor = new Color(1.0f, 0.9f, 0.12f, 1.0f);
                secondaryColor = new Color(0.86f, 0.55f, 0.02f, 1.0f);
                waveAmplitude = 0.04f;
                waveFrequency = 5.0f;
                waveSpeed = 1.8f;
                leftFalloff = 0.55f;
                rightFalloff = 2.0f;
                break;
            case 4:
                primaryColor = new Color(0.94f, 0.25f, 1.0f, 1.0f);
                secondaryColor = new Color(0.22f, 0.12f, 0.76f, 1.0f);
                waveAmplitude = 0.034f;
                waveFrequency = 10.0f;
                waveSpeed = 8.0f;
                leftFalloff = 0.7f;
                rightFalloff = 0.7f;
                break;
            default:
                primaryColor = new Color(1.0f, 0.2f, 0.08f, 1.0f);
                secondaryColor = new Color(0.52f, 0.03f, 0.12f, 1.0f);
                waveAmplitude = 0.058f;
                waveFrequency = 4.0f;
                waveSpeed = -2.2f;
                leftFalloff = 2.8f;
                rightFalloff = 0.42f;
                break;
        }

        _feedbackBaseColor = primaryColor.Lerp(Colors.White, 0.16f);
        SetWavePalette(_reactionWaveMaterial, primaryColor, secondaryColor);
        _reactionWaveMaterial.SetShaderParameter("reaction_mode", reactionMode);
        _reactionWaveMaterial.SetShaderParameter("reaction_progress", 0.0f);
        _reactionWaveMaterial.SetShaderParameter("wave_amplitude", waveAmplitude);
        _reactionWaveMaterial.SetShaderParameter("wave_frequency", waveFrequency);
        _reactionWaveMaterial.SetShaderParameter("wave_speed", waveSpeed);
        _reactionWaveMaterial.SetShaderParameter("left_falloff", leftFalloff);
        _reactionWaveMaterial.SetShaderParameter("right_falloff", rightFalloff);
        _reactionWaveMaterial.SetShaderParameter("opacity", 1.0f);
        ConfigureReactionSprite(
            result,
            isParry,
            isBlock,
            isExecutionSuccess,
            primaryColor,
            secondaryColor);
    }

    private void ConfigureReactionSprite(
        TimingResult result,
        bool isParry,
        bool isBlock,
        bool isExecutionSuccess,
        Color primaryColor,
        Color secondaryColor)
    {
        var animationName = string.Empty;
        if (isParry)
        {
            animationName = "parry";
        }
        else if (isBlock)
        {
            animationName = "block";
        }
        else if (isExecutionSuccess)
        {
            animationName = "execution";
        }
        else
        {
            animationName = result switch
            {
                TimingResult.Perfect => "perfect",
                TimingResult.Good => "good",
                TimingResult.Ok => "ok",
                TimingResult.Miss => "miss",
                _ => string.Empty,
            };
        }

        _reactionSprite.Visible = !string.IsNullOrEmpty(animationName);
        _activeReactionSpriteScale = _reactionSpriteBaseScale *
            GetReactionScaleMultiplier(animationName);
        _reactionSprite.Scale = _activeReactionSpriteScale * 0.72f;
        _reactionSprite.Position = _reactionSpriteBasePosition;
        _reactionSpriteMaterial.SetShaderParameter("burn_progress", 0.0f);
        if (!_reactionSprite.Visible)
        {
            return;
        }

        _reactionSprite.Animation = new StringName(animationName);
        _reactionSprite.Frame = 0;
        _reactionSpriteMaterial.SetShaderParameter("glow_color", primaryColor);
        _reactionSpriteMaterial.SetShaderParameter(
            "burn_color",
            secondaryColor.Lerp(Colors.White, 0.12f));
        _reactionSpriteMaterial.SetShaderParameter(
            "burn_origin",
            animationName switch
            {
                "perfect" => new Vector2(0.5f, 0.52f),
                "parry" => new Vector2(0.42f, 0.48f),
                "ok" => new Vector2(0.56f, 0.5f),
                "good" => new Vector2(0.5f, 0.5f),
                "block" => new Vector2(0.52f, 0.52f),
                "execution" => new Vector2(0.5f, 0.52f),
                _ => new Vector2(0.5f, 0.42f),
            });
    }

    private static float GetReactionScaleMultiplier(string animationName)
    {
        return animationName switch
        {
            "perfect" => 0.92f,
            "miss" => 1.0f,
            "parry" => 0.98f,
            "ok" => 1.05f,
            "block" => 0.98f,
            "execution" => 0.92f,
            "good" => 1.05f,
            _ => 1.0f,
        };
    }

    private string GetFeedbackDisplayName(TimingResult result)
    {
        if (_promptType == RhythmPromptType.EnemyAttack)
        {
            return result switch
            {
                TimingResult.Perfect => "PARRY",
                TimingResult.Good => "BLOCK",
                TimingResult.Ok => "PARTIAL BLOCK",
                _ => "HIT",
            };
        }

        if (_promptType == RhythmPromptType.Execution && result == TimingResult.Perfect)
        {
            return "EXECUTION";
        }

        return TimingJudge.GetDisplayName(result);
    }

    private Color GetPresentationSecondaryColor(Color primaryColor)
    {
        var secondaryColor = _promptType switch
        {
            RhythmPromptType.EnemyAttack => new Color(1.0f, 0.12f, 0.66f, primaryColor.A),
            RhythmPromptType.Execution => new Color(0.28f, 0.86f, 1.0f, primaryColor.A),
            _ => new Color(1.0f, 0.18f, 0.72f, primaryColor.A),
        };
        return secondaryColor.Lerp(Colors.White, 0.08f);
    }

    private static ShaderMaterial GetWaveMaterial(CanvasItem waveNode)
    {
        return GetShaderMaterial(waveNode);
    }

    private static ShaderMaterial GetShaderMaterial(CanvasItem canvasItem)
    {
        return canvasItem.Material as ShaderMaterial ??
            throw new InvalidOperationException(
                $"{canvasItem.GetPath()} precisa possuir ShaderMaterial configurado na cena.");
    }

    private static void SetWavePalette(
        ShaderMaterial material,
        Color primaryColor,
        Color secondaryColor)
    {
        material.SetShaderParameter("primary_color", primaryColor);
        material.SetShaderParameter("secondary_color", secondaryColor);
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

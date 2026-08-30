using System;
using Godot;

/// <summary>
/// Cena mínima para validar a fundação do ritmo antes de adicionar combate.
/// </summary>
public partial class RhythmFoundationTest : Node2D
{
	[Export]
	public PackedScene PromptScene { get; set; } = null!;

	[Export]
	public bool DebugRhythm { get; set; } = true;

	private RhythmManager _rhythmManager = null!;
	private Node2D _promptContainer = null!;
	private ResourcePreloader _sceneResources = null!;
	private Label _bpmLabel = null!;
	private Label _beatLabel = null!;
	private Label _musicPositionLabel = null!;
	private Label _promptTargetLabel = null!;
	private Label _lastOffsetLabel = null!;
	private Label _timingResultLabel = null!;
	private Label _circleSizeLabel = null!;
	private Label _resultLabel = null!;
	private Label _resultDetailLabel = null!;
	private Label _beatIndicator = null!;
	private Label _clockSourceLabel = null!;
	private Label _statusLabel = null!;
	private RhythmPrompt? _activePrompt;
	private double _beatFeedbackTime;

	public override void _Ready()
	{
		_rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
		_promptContainer = GetNode<Node2D>("PromptContainer");
		_sceneResources = GetNode<ResourcePreloader>("SceneResources");
		_bpmLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/BpmLabel");
		_beatLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/BeatLabel");
		_musicPositionLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/MusicPositionLabel");
		_promptTargetLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/PromptTargetLabel");
		_lastOffsetLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/LastOffsetLabel");
		_timingResultLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/TimingResultLabel");
		_circleSizeLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/CircleSizeLabel");
		_resultLabel = GetNode<Label>("CanvasLayer/Ui/ResultLabel");
		_resultDetailLabel = GetNode<Label>("CanvasLayer/Ui/ResultDetailLabel");
		_beatIndicator = GetNode<Label>("CanvasLayer/Ui/BeatIndicator");
		_clockSourceLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/ClockSourceLabel");
		_statusLabel = GetNode<Label>("CanvasLayer/Ui/StatusLabel");

		_rhythmManager.BeatStarted += OnBeatStarted;
		if (PromptScene == null)
		{
			var preloadedPromptScene = _sceneResources.GetResource("rhythm_prompt") as PackedScene;
			if (preloadedPromptScene != null)
			{
				PromptScene = preloadedPromptScene;
			}
		}
		_resultLabel.Text = "AGUARDANDO INPUT";
		_resultDetailLabel.Text = "Pressione SPACE a qualquer momento; o tamanho define o resultado";
		_statusLabel.Text = "Inicializando relógio de ritmo...";

		if (!_rhythmManager.IsRunning)
		{
			_rhythmManager.Start();
		}
		else if (_rhythmManager.CurrentBeat >= 0)
		{
			OnBeatStarted(
				_rhythmManager.CurrentBeat,
				_rhythmManager.GetBeatTime(_rhythmManager.CurrentBeat));
		}
	}

	public override void _Process(double delta)
	{
		UpdateDebugLabels();

		if (_beatFeedbackTime > 0.0d)
		{
			_beatFeedbackTime -= delta;
			var intensity = Mathf.Clamp((float)(_beatFeedbackTime / 0.18d), 0.0f, 1.0f);
			_beatIndicator.Modulate = new Color(1.0f, 0.82f, 0.34f, 0.35f + intensity * 0.65f);
		}
		else
		{
			_beatIndicator.Modulate = new Color(0.75f, 0.78f, 0.88f, 0.7f);
		}
	}

	public override void _ExitTree()
	{
		if (_rhythmManager != null)
		{
			_rhythmManager.BeatStarted -= OnBeatStarted;
		}
	}

	private void OnBeatStarted(int beatIndex, double beatTime)
	{
		_beatFeedbackTime = 0.18d;
		_beatIndicator.Text = $"BEAT {beatIndex + 1}";

		var canCreatePrompt = _activePrompt == null ||
			!GodotObject.IsInstanceValid(_activePrompt) ||
			_activePrompt.IsResolved;

		if (canCreatePrompt && CreatePromptForBeat(beatIndex + 1))
		{
			_statusLabel.Text = "Aperte SPACE a qualquer momento; o tamanho do amarelo define o resultado";
		}
	}

	private bool CreatePromptForBeat(int targetBeat)
	{
		if (PromptScene == null)
		{
			ShowPromptError("PackedScene do prompt não foi encontrado na cena.");
			return false;
		}

		try
		{
			var prompt = PromptScene.Instantiate<RhythmPrompt>();
			_promptContainer.AddChild(prompt);
			prompt.Resolved += OnPromptResolved;
			prompt.Configure(_rhythmManager.GetBeatTime(targetBeat), _rhythmManager.BeatDuration);
			_activePrompt = prompt;
			return true;
		}
		catch (Exception exception)
		{
			ShowPromptError($"Falha ao instanciar RhythmPrompt: {exception.Message}");
			GD.PrintErr(exception);
			return false;
		}
	}

	private void ShowPromptError(string message)
	{
		_statusLabel.Text = $"ERRO: {message}";
		_promptTargetLabel.Text = "Prompt target: ERRO";
	}

	private void OnPromptResolved(
		int result,
		float precisionPercent,
		float offsetMilliseconds,
		string direction,
		string resultName)
	{
		var timingResult = (TimingResult)result;
		_resultLabel.Text = resultName;
		_resultDetailLabel.Text =
			$"{TimingJudge.FormatOffsetMilliseconds(offsetMilliseconds)}  •  " +
			$"{precisionPercent:0.0}%  •  {direction}";
		_lastOffsetLabel.Text = $"Last input: {TimingJudge.FormatOffsetMilliseconds(offsetMilliseconds)}";
		_timingResultLabel.Text = $"Timing result: {resultName}";
		_statusLabel.Text = "Resultado registrado. Aguarde o próximo beat.";
		_resultLabel.Modulate = GetResultColor(timingResult);
	}

	private void UpdateDebugLabels()
	{
		if (!DebugRhythm)
		{
			return;
		}

		var musicPosition = _rhythmManager.GetMusicPosition();
		var targetTime = -1.0d;
		var circleSizePercent = -1.0f;
		if (_activePrompt != null &&
			GodotObject.IsInstanceValid(_activePrompt) &&
			!_activePrompt.IsResolved)
		{
			targetTime = _activePrompt.TargetTime;
			circleSizePercent = _activePrompt.GetCircleSizePercentAt(musicPosition);
		}

		_bpmLabel.Text = $"BPM: {_rhythmManager.Bpm:0.0}";
		_beatLabel.Text = $"Beat atual: {_rhythmManager.CurrentBeat + 1}";
		_musicPositionLabel.Text = $"Music position: {musicPosition:0.000}s";
		_promptTargetLabel.Text = targetTime >= 0.0d
			? $"Prompt target: {targetTime:0.000}s"
			: "Prompt target: --";
		_circleSizeLabel.Text = circleSizePercent >= 0.0f
			? $"Circle size: {circleSizePercent:0.0}%"
			: "Circle size: --";
		_clockSourceLabel.Text = _rhythmManager.UsingAudioClock
			? "Clock: AudioStreamPlayer"
			: "Clock: monotônico (sem stream)";
	}

	private static Color GetResultColor(TimingResult result)
	{
		return result switch
		{
			TimingResult.Perfect => new Color(1.0f, 0.84f, 0.3f, 1.0f),
			TimingResult.Good => new Color(0.45f, 1.0f, 0.65f, 1.0f),
			TimingResult.Ok => new Color(0.45f, 0.78f, 1.0f, 1.0f),
			_ => new Color(1.0f, 0.38f, 0.38f, 1.0f),
		};
	}
}

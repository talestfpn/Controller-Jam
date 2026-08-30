using Godot;

/// <summary>
/// Controla somente a entrada e o pulso do menu. A cena fornece a arte,
/// tipografia e composição visual; música e transição persistem em autoloads.
/// </summary>
public partial class MainMenuController : Control
{
    [Export(PropertyHint.Range, "1.0,999.0,1.0")]
    public float InitialPlayerHealth { get; set; } = 100.0f;

    private RhythmManager _rhythmManager = null!;
    private RunManager _runManager = null!;
    private SceneTransitionManager _sceneTransitionManager = null!;
    private Label _promptLabel = null!;
    private Label _promptGlow = null!;
    private bool _starting;
    private double _elapsed;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _runManager = GetNode<RunManager>("/root/RunManager");
        _sceneTransitionManager = GetNode<SceneTransitionManager>(
            "/root/SceneTransitionManager");
        _promptLabel = GetNode<Label>("PressPrompt");
        _promptGlow = GetNode<Label>("PressPromptGlow");

        _runManager.BeginRun(InitialPlayerHealth);
        if (!_rhythmManager.IsRunning)
        {
            _rhythmManager.Start();
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        var beatPulse = 1.0f - (float)_rhythmManager.BeatPhase;
        beatPulse *= beatPulse;
        var idlePulse = 0.5f + 0.5f * Mathf.Sin((float)_elapsed * 2.1f);
        var intensity = Mathf.Clamp(idlePulse * 0.6f + beatPulse * 0.4f, 0.0f, 1.0f);

        var scale = 1.0f + intensity * 0.035f;
        _promptLabel.Scale = Vector2.One * scale;
        _promptGlow.Scale = Vector2.One * (scale + 0.025f);
        _promptLabel.Modulate = new Color(1.0f, 0.9f, 0.56f, 0.72f + intensity * 0.28f);
        _promptGlow.Modulate = new Color(0.72f, 0.22f, 1.0f, 0.08f + intensity * 0.18f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_starting ||
            _sceneTransitionManager.IsTransitioning ||
            !@event.IsActionPressed("rhythm_action"))
        {
            return;
        }

        _starting = true;
        _promptLabel.Text = "O RITMO DESPERTA...";
        _promptGlow.Text = _promptLabel.Text;
        _sceneTransitionManager.TransitionToCombat();
        GetViewport().SetInputAsHandled();
    }
}

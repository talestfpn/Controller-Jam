using Godot;

/// <summary>
/// Mantém a transição viva durante a troca de cenas. O overlay e o material
/// são montados na cena do autoload; este componente só conduz o estado e o
/// fator da máscara e troca a PackedScene sob a tela coberta.
/// </summary>
public partial class SceneTransitionManager : CanvasLayer
{
    [Signal]
    public delegate void TransitionStartedEventHandler();

    [Signal]
    public delegate void TransitionFinishedEventHandler();

    [Export(PropertyHint.Range, "0.1,2.0,0.01")]
    public float CoverDuration { get; set; } = 0.46f;

    [Export(PropertyHint.Range, "0.1,2.0,0.01")]
    public float RevealDuration { get; set; } = 0.52f;

    [Export]
    public PackedScene CombatScene { get; set; } = null!;

    private enum TransitionPhase
    {
        Idle,
        Covering,
        Revealing,
    }

    private ColorRect _overlay = null!;
    private ShaderMaterial _material = null!;
    private TransitionPhase _phase;
    private double _phaseElapsed;
    private PackedScene? _targetScene;

    public bool IsTransitioning => _phase != TransitionPhase.Idle;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _overlay = GetNode<ColorRect>("TransitionOverlay");
        if (_overlay.Material is ShaderMaterial shaderMaterial)
        {
            _material = shaderMaterial;
        }
        else
        {
            GD.PrintErr(
                "SceneTransitionManager: TransitionOverlay precisa de um ShaderMaterial.");
        }

        GetViewport().SizeChanged += OnViewportSizeChanged;
        OnViewportSizeChanged();
        SetFactor(0.0f);
        _overlay.Visible = false;
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null)
        {
            GetViewport().SizeChanged -= OnViewportSizeChanged;
        }
    }

    public override void _Process(double delta)
    {
        switch (_phase)
        {
            case TransitionPhase.Covering:
                ProcessCover(delta);
                break;
            case TransitionPhase.Revealing:
                ProcessReveal(delta);
                break;
        }
    }

    public void TransitionToPacked(PackedScene targetScene)
    {
        if (targetScene == null || IsTransitioning)
        {
            return;
        }

        _targetScene = targetScene;
        _phaseElapsed = 0.0d;
        _phase = TransitionPhase.Covering;
        _overlay.Visible = true;
        SetFactor(0.0f);
        EmitSignal(SignalName.TransitionStarted);
    }

    public void TransitionToCombat()
    {
        if (CombatScene == null)
        {
            GD.PrintErr(
                "SceneTransitionManager: CombatScene não foi configurada no autoload.");
            return;
        }

        TransitionToPacked(CombatScene);
    }

    private void ProcessCover(double delta)
    {
        _phaseElapsed += delta;
        var duration = Mathf.Max(0.01f, CoverDuration);
        var progress = Mathf.Clamp((float)(_phaseElapsed / duration), 0.0f, 1.0f);
        SetFactor(progress);
        if (progress < 1.0f)
        {
            return;
        }

        var targetScene = _targetScene;
        _targetScene = null;
        if (targetScene == null)
        {
            FinishTransition();
            return;
        }

        var changeError = GetTree().ChangeSceneToPacked(targetScene);
        if (changeError != Error.Ok)
        {
            GD.PrintErr($"SceneTransitionManager: falha ao trocar de cena: {changeError}");
            FinishTransition();
            return;
        }

        _phaseElapsed = 0.0d;
        _phase = TransitionPhase.Revealing;
        SetFactor(1.0f);
    }

    private void ProcessReveal(double delta)
    {
        _phaseElapsed += delta;
        var duration = Mathf.Max(0.01f, RevealDuration);
        var progress = Mathf.Clamp((float)(_phaseElapsed / duration), 0.0f, 1.0f);
        SetFactor(1.0f - progress);
        if (progress >= 1.0f)
        {
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        _phase = TransitionPhase.Idle;
        _phaseElapsed = 0.0d;
        SetFactor(0.0f);
        _overlay.Visible = false;
        EmitSignal(SignalName.TransitionFinished);
    }

    private void SetFactor(float factor)
    {
        if (_material != null)
        {
            _material.SetShaderParameter("factor", Mathf.Clamp(factor, 0.0f, 1.0f));
        }
    }

    private void OnViewportSizeChanged()
    {
        if (_material == null)
        {
            return;
        }

        _material.SetShaderParameter(
            "node_resolution",
            GetViewport().GetVisibleRect().Size);
    }
}

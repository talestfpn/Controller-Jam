using Godot;

/// <summary>
/// Tela final da run. A cena fornece toda a apresentação; o controller apenas
/// lê o resultado persistido e conduz o início de uma nova run.
/// </summary>
public partial class VictoryController : Control
{
    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float StartingPlayerMaxHealth { get; set; } = 100.0f;

    private RunManager _runManager = null!;
    private SceneTransitionManager _sceneTransitionManager = null!;
    private Label _enemyNameLabel = null!;
    private Label _summaryLabel = null!;
    private Label _essenceLabel = null!;
    private Label _statusLabel = null!;
    private Button _continueButton = null!;

    public override void _Ready()
    {
        _runManager = GetNode<RunManager>("/root/RunManager");
        _sceneTransitionManager = GetNode<SceneTransitionManager>(
            "/root/SceneTransitionManager");
        _enemyNameLabel = GetNode<Label>("Content/EnemyNameLabel");
        _summaryLabel = GetNode<Label>("Content/SummaryLabel");
        _essenceLabel = GetNode<Label>("Content/EssenceLabel");
        _statusLabel = GetNode<Label>("StatusLabel");
        _continueButton = GetNode<Button>("ContinueButton");

        _continueButton.Pressed += OnContinuePressed;
        PopulateResult();
    }

    public override void _ExitTree()
    {
        if (_continueButton != null)
        {
            _continueButton.Pressed -= OnContinuePressed;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo ||
            keyEvent.Keycode != Key.Space && keyEvent.PhysicalKeycode != Key.Space)
        {
            return;
        }

        OnContinuePressed();
        GetViewport().SetInputAsHandled();
    }

    private void PopulateResult()
    {
        var result = _runManager.LastBattleResult;
        if (result == null)
        {
            _enemyNameLabel.Text = "THE RESISTANCE";
            _summaryLabel.Text = "A run foi concluída.";
            _essenceLabel.Text = $"ESSENCE  {_runManager.CurrentEssence:000}";
            return;
        }

        _enemyNameLabel.Text = result.EnemyName;
        _summaryLabel.Text =
            $"PERFECT  {result.PerfectCount}    GOOD  {result.GoodCount}    " +
            $"OK  {result.OkCount}    MISS  {result.MissCount}\n" +
            $"MELHOR COMBO  x{result.HighestCombo}    " +
            $"EXECUÇÕES  {result.SuccessfulExecutions}";
        _essenceLabel.Text =
            $"ESSENCE OBTIDA  +{result.Reward.TotalReward}    " +
            $"TOTAL  {_runManager.CurrentEssence:000}";
    }

    private void OnContinuePressed()
    {
        if (_continueButton.Disabled || _sceneTransitionManager.IsTransitioning)
        {
            return;
        }

        _continueButton.Disabled = true;
        _statusLabel.Text = "NOVA RUN — o primeiro arcano aguarda.";
        _runManager.BeginRun(Mathf.Max(1.0f, StartingPlayerMaxHealth));
        _sceneTransitionManager.TransitionToCombat();
    }
}

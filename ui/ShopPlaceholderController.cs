using Godot;

/// <summary>
/// Transição visual mínima da loja. Produtos, preços e upgrades ficam para a
/// próxima fase; aqui validamos apenas a continuidade da run e da Essence.
/// </summary>
public partial class ShopPlaceholderController : Control
{
    [Export]
    public string CombatScenePath { get; set; } = "res://scenes/combat_test.tscn";

    private RunManager _runManager = null!;
    private Label _essenceLabel = null!;
    private Button _continueButton = null!;

    public override void _Ready()
    {
        _runManager = GetNode<RunManager>("/root/RunManager");
        _essenceLabel = GetNode<Label>("Content/EssenceLabel");
        _continueButton = GetNode<Button>("ContinueButton");
        _continueButton.Pressed += OnContinuePressed;
        UpdateEssenceLabel();
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
            !IsConfirmKey(keyEvent))
        {
            return;
        }

        OnContinuePressed();
        GetViewport().SetInputAsHandled();
    }

    private void UpdateEssenceLabel()
    {
        _essenceLabel.Text = $"ESSENCE CARRIED   {_runManager.CurrentEssence}";
    }

    private void OnContinuePressed()
    {
        if (string.IsNullOrWhiteSpace(CombatScenePath))
        {
            GD.PrintErr("ShopPlaceholderController: CombatScenePath não foi configurado na cena.");
            return;
        }

        var changeError = GetTree().ChangeSceneToFile(CombatScenePath);
        if (changeError != Error.Ok)
        {
            GD.PrintErr($"Falha ao voltar para o combate: {changeError}");
        }
    }

    private static bool IsConfirmKey(InputEventKey keyEvent)
    {
        return keyEvent.Keycode == Key.Space ||
            keyEvent.PhysicalKeycode == Key.Space;
    }
}

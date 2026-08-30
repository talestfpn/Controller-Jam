using Godot;

/// <summary>
/// Componente visual reutilizável de uma Upgrade Card. A Shop fornece o
/// Resource e escuta o sinal; este componente não compra nem aplica upgrades.
/// </summary>
public partial class UpgradeCardController : Control
{
    [Signal]
    public delegate void ChosenEventHandler();

    [Signal]
    public delegate void RerollRequestedEventHandler();

    [Export]
    public Texture2D? CommonFrame { get; set; }

    [Export]
    public Texture2D? RareFrame { get; set; }

    [Export]
    public Texture2D? ArcaneFrame { get; set; }

    private TextureRect _cardFrame = null!;
    private TextureRect _iconTexture = null!;
    private Label _iconPlaceholder = null!;
    private Label _categoryLabel = null!;
    private Label _nameLabel = null!;
    private Label _descriptionLabel = null!;
    private Label _effectLabel = null!;
    private Label _stackLabel = null!;
    private Label _costLabel = null!;
    private Label _lockedLabel = null!;
    private Button _button = null!;
    private Button _rerollButton = null!;
    private Control _glow = null!;
    private bool _canInteract;
    private bool _canReroll;

    public UpgradeDataResource? Definition { get; private set; }

    public override void _Ready()
    {
        _cardFrame = GetNode<TextureRect>("CardFrame");
        _iconTexture = GetNode<TextureRect>("IconTexture");
        _iconPlaceholder = GetNode<Label>("IconPlaceholder");
        _categoryLabel = GetNode<Label>("CategoryLabel");
        _nameLabel = GetNode<Label>("NameLabel");
        _descriptionLabel = GetNode<Label>("DescriptionLabel");
        _effectLabel = GetNode<Label>("EffectLabel");
        _stackLabel = GetNode<Label>("StackLabel");
        _costLabel = GetNode<Label>("CostLabel");
        _lockedLabel = GetNode<Label>("LockedLabel");
        _button = GetNode<Button>("CardButton");
        _rerollButton = GetNode<Button>("RerollButton");
        _glow = GetNode<Control>("CardGlow");
        _button.Pressed += OnButtonPressed;
        _button.MouseEntered += OnMouseEntered;
        _button.MouseExited += OnMouseExited;
        _rerollButton.Pressed += OnRerollButtonPressed;
        _glow.Visible = false;
        _cardFrame.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _iconTexture.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        SetRerollAvailable(false);
    }

    public override void _ExitTree()
    {
        if (_button != null)
        {
            _button.Pressed -= OnButtonPressed;
            _button.MouseEntered -= OnMouseEntered;
            _button.MouseExited -= OnMouseExited;
        }

        if (_rerollButton != null)
        {
            _rerollButton.Pressed -= OnRerollButtonPressed;
        }
    }

    public void Configure(
        UpgradeDataResource definition,
        int currentStacks,
        bool canAfford)
    {
        Definition = definition;
        _cardFrame.Texture = GetFrameForRarity(definition.Rarity);
        _categoryLabel.Text =
            $"{GetCategoryName(definition.Category)}  •  {GetRarityName(definition.Rarity)}";
        _nameLabel.Text = definition.DisplayName;
        _descriptionLabel.Text = definition.Description;
        _effectLabel.Text = definition.EffectSummary;
        _stackLabel.Text = definition.IsConsumable
            ? "CARTA CONSUMÍVEL"
            : $"ACÚMULOS {currentStacks + 1}/{definition.MaxStacks}";
        _costLabel.Text = $"{definition.EssenceCost} ESSENCE";
        _lockedLabel.Visible = !canAfford;
        _iconPlaceholder.Text = GetCategorySymbol(definition.Category);
        _iconTexture.Texture = definition.Icon;
        _iconTexture.Visible = definition.Icon != null;
        _iconPlaceholder.Visible = definition.Icon == null;
        SetInteractive(canAfford);
        SetRerollAvailable(false);
        Modulate = Colors.White;
        Scale = Vector2.One;
        Visible = true;
    }

    public void SetInteractive(bool canInteract)
    {
        _canInteract = canInteract && Definition != null;
        if (_button != null)
        {
            _button.Disabled = !_canInteract;
        }

        if (!_canInteract)
        {
            _lockedLabel.Visible = true;
            Modulate = new Color(0.48f, 0.5f, 0.62f, 0.82f);
            _glow.Visible = false;
        }
        else
        {
            _lockedLabel.Visible = false;
            Modulate = Colors.White;
        }
    }

    public void SetRerollAvailable(bool available)
    {
        _canReroll = available && Definition != null;
        if (_rerollButton == null)
        {
            return;
        }

        _rerollButton.Disabled = !_canReroll;
        _rerollButton.Modulate = _canReroll
            ? Colors.White
            : new Color(0.42f, 0.38f, 0.5f, 0.62f);
    }

    public void ShowPurchased()
    {
        _canInteract = false;
        _button.Disabled = true;
        SetRerollAvailable(false);
        _lockedLabel.Text = "ADQUIRIDA";
        _lockedLabel.Visible = true;
        _glow.Visible = true;
        _glow.Modulate = new Color(0.7f, 1.0f, 0.5f, 1.0f);
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(this, "scale", Vector2.One * 1.035f, 0.22d);
    }

    public void FadeAsUnselected()
    {
        _canInteract = false;
        _button.Disabled = true;
        SetRerollAvailable(false);
        _glow.Visible = false;
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.In);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(this, "modulate", new Color(1, 1, 1, 0), 0.18d);
    }

    private void OnButtonPressed()
    {
        if (_canInteract && Definition != null)
        {
            EmitSignal(SignalName.Chosen);
        }
    }

    private void OnRerollButtonPressed()
    {
        if (_canReroll && Definition != null)
        {
            EmitSignal(SignalName.RerollRequested);
        }
    }

    private void OnMouseEntered()
    {
        if (!_canInteract)
        {
            return;
        }

        _glow.Visible = true;
        _glow.Modulate = new Color(1.0f, 0.78f, 0.3f, 0.9f);
        ZIndex = 10;
        AnimateScale(Vector2.One * 1.035f, 0.16d);
    }

    private void OnMouseExited()
    {
        if (!_canInteract)
        {
            return;
        }

        _glow.Visible = false;
        ZIndex = 0;
        AnimateScale(Vector2.One, 0.18d);
    }

    private void AnimateScale(Vector2 target, double duration)
    {
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(this, "scale", target, duration);
    }

    private static string GetCategoryName(UpgradeCategory category)
    {
        return category switch
        {
            UpgradeCategory.Damage => "DANO",
            UpgradeCategory.Posture => "RESISTÊNCIA",
            UpgradeCategory.Parry => "PARRY",
            UpgradeCategory.Combo => "COMBO",
            UpgradeCategory.Survival => "SOBREVIVÊNCIA",
            UpgradeCategory.Execution => "EXECUÇÃO",
            UpgradeCategory.Essence => "ESSENCE",
            UpgradeCategory.RiskReward => "RISCO / RECOMPENSA",
            _ => "ARCANA",
        };
    }

    private static string GetRarityName(UpgradeRarity rarity)
    {
        return rarity switch
        {
            UpgradeRarity.Common => "COMUM",
            UpgradeRarity.Rare => "RARA",
            UpgradeRarity.Arcane => "ARCANA",
            _ => "ARCANA",
        };
    }

    private Texture2D? GetFrameForRarity(UpgradeRarity rarity)
    {
        return rarity switch
        {
            UpgradeRarity.Common => CommonFrame ?? ArcaneFrame,
            UpgradeRarity.Rare => RareFrame ?? ArcaneFrame,
            UpgradeRarity.Arcane => ArcaneFrame,
            _ => ArcaneFrame,
        };
    }

    private static string GetCategorySymbol(UpgradeCategory category)
    {
        return category switch
        {
            UpgradeCategory.Damage => "✦",
            UpgradeCategory.Posture => "◇",
            UpgradeCategory.Parry => "◈",
            UpgradeCategory.Combo => "∞",
            UpgradeCategory.Survival => "♢",
            UpgradeCategory.Execution => "†",
            UpgradeCategory.Essence => "☼",
            UpgradeCategory.RiskReward => "☽",
            _ => "✧",
        };
    }
}

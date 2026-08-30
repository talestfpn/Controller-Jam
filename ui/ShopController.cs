using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Controla a rodada da loja. Os cards já existem na cena; este controller
/// sorteia Resources, aplica compras no RunManager e conduz a progressão.
/// </summary>
public partial class ShopController : Control
{
    [Export]
    public Godot.Collections.Array<UpgradeDataResource> UpgradePool { get; set; } = new();

    [Export(PropertyHint.Range, "1,20,1")]
    public int EncounterCount { get; set; } = 8;

    [Export]
    public int ShopSeed { get; set; } = 12345;

    [Export(PropertyHint.Range, "0,100,1")]
    public float CommonRarityWeight { get; set; } = 60.0f;

    [Export(PropertyHint.Range, "0,100,1")]
    public float RareRarityWeight { get; set; } = 30.0f;

    [Export(PropertyHint.Range, "0,100,1")]
    public float ArcaneRarityWeight { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "1.0,9999.0,1.0")]
    public float DefaultPlayerMaxHealth { get; set; } = 100.0f;

    [Export]
    public bool DebugShop { get; set; }

    private RunManager _runManager = null!;
    private SceneTransitionManager _sceneTransitionManager = null!;
    private UpgradeCardController[] _cards = Array.Empty<UpgradeCardController>();
    private Button _continueButton = null!;
    private Label _essenceLabel = null!;
    private Label _encounterLabel = null!;
    private Label _statusLabel = null!;
    private Control _debugPanel = null!;
    private Label _debugLabel = null!;
    private List<UpgradeDataResource> _candidates = new();
    private int _activeShopSeed;

    public override void _Ready()
    {
        _runManager = GetNode<RunManager>("/root/RunManager");
        _sceneTransitionManager = GetNode<SceneTransitionManager>(
            "/root/SceneTransitionManager");
        _cards = new[]
        {
            GetNode<UpgradeCardController>("Content/Cards/Card1"),
            GetNode<UpgradeCardController>("Content/Cards/Card2"),
            GetNode<UpgradeCardController>("Content/Cards/Card3"),
        };
        _continueButton = GetNode<Button>("ContinueButton");
        _essenceLabel = GetNode<Label>("EssenceLabel");
        _encounterLabel = GetNode<Label>("Content/EncounterLabel");
        _statusLabel = GetNode<Label>("StatusLabel");
        _debugPanel = GetNode<Control>("DebugPanel");
        _debugLabel = GetNode<Label>("DebugPanel/DebugLabel");

        _cards[0].Chosen += OnCardOneChosen;
        _cards[1].Chosen += OnCardTwoChosen;
        _cards[2].Chosen += OnCardThreeChosen;
        _cards[0].RerollRequested += OnCardOneRerollRequested;
        _cards[1].RerollRequested += OnCardTwoRerollRequested;
        _cards[2].RerollRequested += OnCardThreeRerollRequested;
        _continueButton.Pressed += OnContinuePressed;

        _runManager.EnsureRunStarted(DefaultPlayerMaxHealth);
        _activeShopSeed = _runManager.StartShop(ShopSeed);
        _debugPanel.Visible = DebugShop;
        _encounterLabel.Text =
            $"ARCANA {Math.Min(_runManager.CurrentEncounterIndex + 1, EncounterCount):00}  /  {EncounterCount:00}";
        GenerateCandidates(_activeShopSeed);
        UpdateShopUi();
    }

    public override void _ExitTree()
    {
        if (_cards.Length == 3)
        {
            _cards[0].Chosen -= OnCardOneChosen;
            _cards[1].Chosen -= OnCardTwoChosen;
            _cards[2].Chosen -= OnCardThreeChosen;
        }

        if (_continueButton != null)
        {
            _continueButton.Pressed -= OnContinuePressed;
        }

        if (_cards.Length == 3)
        {
            _cards[0].RerollRequested -= OnCardOneRerollRequested;
            _cards[1].RerollRequested -= OnCardTwoRerollRequested;
            _cards[2].RerollRequested -= OnCardThreeRerollRequested;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo ||
            !IsSpace(keyEvent))
        {
            return;
        }

        OnContinuePressed();
        GetViewport().SetInputAsHandled();
    }

    private void GenerateCandidates(int seed, ISet<string>? excludedIds = null)
    {
        _activeShopSeed = seed;
        _candidates = ShopCandidateGenerator.Generate(
            UpgradePool,
            _runManager.Build,
            seed,
            CommonRarityWeight,
            RareRarityWeight,
            ArcaneRarityWeight,
            _cards.Length,
            excludedIds);

        for (var index = 0; index < _cards.Length; index++)
        {
            if (index >= _candidates.Count)
            {
                _cards[index].Visible = false;
                continue;
            }

            var candidate = _candidates[index];
            _cards[index].Configure(
                candidate,
                _runManager.Build.GetStacks(candidate.Id),
                _runManager.CanAfford(candidate.EssenceCost));
        }

        UpdateCardRerollButtons();
    }

    private void UpdateShopUi()
    {
        _essenceLabel.Text = $"ESSENCE  {_runManager.CurrentEssence:000}";
        _continueButton.Text = _runManager.ShopPurchaseMade
            ? "CONTINUAR PARA O PRÓXIMO ENCONTRO"
            : "CONTINUAR SEM COMPRAR";
        _debugLabel.Text =
            $"Essence atual: {_runManager.CurrentEssence}\n" +
            $"Upgrades atuais: {_runManager.GetBuildDebugSummary()}\n" +
            $"Acúmulos únicos: {_runManager.Build.UniqueUpgradeCount}\n" +
            $"Semente da loja: {_activeShopSeed}\n" +
            $"Rerolls usados: {_cards.Length - _runManager.ShopRerollsRemaining}/{_cards.Length}\n" +
            $"Cartas disponíveis: {GetValidCandidatePoolSize()}";
        UpdateCardRerollButtons();
    }

    private int GetValidCandidatePoolSize()
    {
        return UpgradePool.Count(definition =>
            definition != null &&
            definition.IsValid &&
            _runManager.Build.CanAcquire(definition));
    }

    private void UpdateCardRerollButtons()
    {
        for (var index = 0; index < _cards.Length; index++)
        {
            var available = index < _candidates.Count &&
                _runManager.IsShopSlotRerollAvailable(index) &&
                !_runManager.ShopPurchaseMade;
            _cards[index].SetRerollAvailable(available);
        }
    }

    private void OnCardOneRerollRequested()
    {
        OnCardRerollRequested(0);
    }

    private void OnCardTwoRerollRequested()
    {
        OnCardRerollRequested(1);
    }

    private void OnCardThreeRerollRequested()
    {
        OnCardRerollRequested(2);
    }

    private void OnCardRerollRequested(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= _candidates.Count ||
            !_runManager.IsShopSlotRerollAvailable(slotIndex) ||
            _runManager.ShopPurchaseMade)
        {
            return;
        }

        var excludedIds = new HashSet<string>(
            _candidates.Select(candidate => candidate.Id),
            StringComparer.OrdinalIgnoreCase);
        var replacementSeed = unchecked(
            _activeShopSeed +
            1009 +
            slotIndex * 7919);
        var replacement = ShopCandidateGenerator.Generate(
            UpgradePool,
            _runManager.Build,
            replacementSeed,
            CommonRarityWeight,
            RareRarityWeight,
            ArcaneRarityWeight,
            count: 1,
            excludedIds: excludedIds);
        if (replacement.Count == 0)
        {
            _statusLabel.Text =
                "NÃO HÁ OUTRA CARTA DISPONÍVEL PARA ESTE SLOT.";
            return;
        }

        if (!_runManager.TryUseShopReroll(slotIndex))
        {
            return;
        }

        var selected = replacement[0];
        _candidates[slotIndex] = selected;
        _cards[slotIndex].Configure(
            selected,
            _runManager.Build.GetStacks(selected.Id),
            _runManager.CanAfford(selected.EssenceCost));
        _statusLabel.Text =
            $"A CARTA {slotIndex + 1} FOI REVELADA NOVAMENTE.";
        UpdateShopUi();
    }

    private void OnCardOneChosen()
    {
        OnCardChosen(0);
    }

    private void OnCardTwoChosen()
    {
        OnCardChosen(1);
    }

    private void OnCardThreeChosen()
    {
        OnCardChosen(2);
    }

    private void OnCardChosen(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= _candidates.Count)
        {
            return;
        }

        var selected = _candidates[cardIndex];
        if (!_runManager.TryPurchaseUpgrade(selected))
        {
            _statusLabel.Text = _runManager.ShopPurchaseMade
                ? "UMA CARTA JÁ FOI ADQUIRIDA NESTA RODADA."
                : "ESSENCE INSUFICIENTE PARA ESTA CARTA.";
            UpdateShopUi();
            return;
        }

        for (var index = 0; index < _cards.Length; index++)
        {
            if (index == cardIndex)
            {
                _cards[index].ShowPurchased();
            }
            else
            {
                _cards[index].FadeAsUnselected();
            }
        }

        _statusLabel.Text =
            $"CARTA ADQUIRIDA\n{selected.DisplayName}\n{selected.EffectSummary}";
        UpdateShopUi();
    }

    private void OnContinuePressed()
    {
        if (_continueButton.Disabled || _sceneTransitionManager.IsTransitioning)
        {
            return;
        }

        if (_runManager.AdvanceToNextEncounter(EncounterCount))
        {
            _continueButton.Disabled = true;
            _sceneTransitionManager.TransitionToCombat();

            return;
        }

        _continueButton.Disabled = true;
        _statusLabel.Text =
            "OS ENCONTROS DISPONÍVEIS TERMINARAM — a run foi concluída.";
    }

    private static bool IsSpace(InputEventKey keyEvent)
    {
        return keyEvent.Keycode == Key.Space ||
            keyEvent.PhysicalKeycode == Key.Space;
    }
}

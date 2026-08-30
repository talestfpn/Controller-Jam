using System;
using Godot;

/// <summary>
/// Apresenta o snapshot de uma batalha vencida. A cena fornece toda a árvore
/// visual; este controller apenas preenche dados e conduz a revelação.
/// </summary>
public partial class BattleResultController : Control
{
    [Export]
    public PackedScene ShopScene { get; set; } = null!;

    private RunManager _runManager = null!;
    private SceneTransitionManager _sceneTransitionManager = null!;
    private Label _enemyNameLabel = null!;
    private Label _perfectValueLabel = null!;
    private Label _goodValueLabel = null!;
    private Label _okValueLabel = null!;
    private Label _missValueLabel = null!;
    private Label _highestComboValueLabel = null!;
    private Label _executionValueLabel = null!;
    private Label _baseRewardValueLabel = null!;
    private Label _perfectBonusValueLabel = null!;
    private Label _executionBonusValueLabel = null!;
    private Label _multiplierValueLabel = null!;
    private Label _totalRewardValueLabel = null!;
    private Label _ownedEssenceValueLabel = null!;
    private Button _continueButton = null!;
    private Label _continueHintLabel = null!;
    private Control[] _revealTargets = Array.Empty<Control>();
    private int _nextRevealIndex;
    private double _revealTimer;
    private bool _sequenceFinished;
    private BattleResultData? _result;

    public override void _Ready()
    {
        _runManager = GetNode<RunManager>("/root/RunManager");
        _sceneTransitionManager = GetNode<SceneTransitionManager>(
            "/root/SceneTransitionManager");
        _enemyNameLabel = GetNode<Label>("Content/EnemyNameLabel");
        _perfectValueLabel = GetNode<Label>("Content/Performance/PerfectRow/Value");
        _goodValueLabel = GetNode<Label>("Content/Performance/GoodRow/Value");
        _okValueLabel = GetNode<Label>("Content/Performance/OkRow/Value");
        _missValueLabel = GetNode<Label>("Content/Performance/MissRow/Value");
        _highestComboValueLabel = GetNode<Label>(
            "Content/Performance/HighestComboRow/Value");
        _executionValueLabel = GetNode<Label>("Content/Performance/ExecutionRow/Value");
        _baseRewardValueLabel = GetNode<Label>("Content/Rewards/BaseRewardRow/Value");
        _perfectBonusValueLabel = GetNode<Label>(
            "Content/Rewards/PerfectBonusRow/Value");
        _executionBonusValueLabel = GetNode<Label>(
            "Content/Rewards/ExecutionBonusRow/Value");
        _multiplierValueLabel = GetNode<Label>("Content/Rewards/MultiplierRow/Value");
        _totalRewardValueLabel = GetNode<Label>("Content/Rewards/TotalRewardRow/Value");
        _ownedEssenceValueLabel = GetNode<Label>("Content/OwnedEssenceRow/Value");
        _continueButton = GetNode<Button>("ContinueButton");
        _continueHintLabel = GetNode<Label>("ContinueHintLabel");

        _continueButton.Pressed += OnContinuePressed;
        _result = _runManager.LastBattleResult;
        if (_result == null)
        {
            ShowMissingResult();
            return;
        }

        PopulateResult(_result);
        _revealTargets = new Control[]
        {
            _enemyNameLabel,
            GetNode<Control>("Content/Performance/Title"),
            GetNode<Control>("Content/Performance/PerfectRow"),
            GetNode<Control>("Content/Performance/GoodRow"),
            GetNode<Control>("Content/Performance/OkRow"),
            GetNode<Control>("Content/Performance/MissRow"),
            GetNode<Control>("Content/Performance/HighestComboRow"),
            GetNode<Control>("Content/Performance/ExecutionRow"),
            GetNode<Control>("Content/Rewards/Title"),
            GetNode<Control>("Content/Rewards/BaseRewardRow"),
            GetNode<Control>("Content/Rewards/PerfectBonusRow"),
            GetNode<Control>("Content/Rewards/ExecutionBonusRow"),
            GetNode<Control>("Content/Rewards/MultiplierRow"),
            GetNode<Control>("Content/Rewards/TotalRewardRow"),
            GetNode<Control>("Content/OwnedEssenceRow"),
        };

        foreach (var target in _revealTargets)
        {
            SetRevealState(target, false);
        }

        _continueButton.Visible = false;
        _continueHintLabel.Text = "SPACE — continuar para a loja";
    }

    public override void _ExitTree()
    {
        if (_continueButton != null)
        {
            _continueButton.Pressed -= OnContinuePressed;
        }
    }

    public override void _Process(double delta)
    {
        if (_sequenceFinished || _revealTargets.Length == 0)
        {
            return;
        }

        _revealTimer += delta;
        while (_revealTimer >= 0.12d && !_sequenceFinished)
        {
            _revealTimer -= 0.12d;
            RevealNext();
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

        if (!_sequenceFinished)
        {
            RevealAll();
        }
        else
        {
            OnContinuePressed();
        }

        GetViewport().SetInputAsHandled();
    }

    private void PopulateResult(BattleResultData result)
    {
        var reward = result.Reward;
        _enemyNameLabel.Text = result.EnemyName;
        _perfectValueLabel.Text = result.PerfectCount.ToString();
        _goodValueLabel.Text = result.GoodCount.ToString();
        _okValueLabel.Text = result.OkCount.ToString();
        _missValueLabel.Text = result.MissCount.ToString();
        _highestComboValueLabel.Text = $"x{result.HighestCombo}";
        _executionValueLabel.Text = result.SuccessfulExecutions.ToString();
        _baseRewardValueLabel.Text = $"+{reward.BaseReward}";
        _perfectBonusValueLabel.Text = $"+{reward.PerfectBonus}";
        _executionBonusValueLabel.Text = $"+{reward.ExecutionBonus}";
        _multiplierValueLabel.Text = $"x{reward.ComboMultiplier:0.00}";
        _totalRewardValueLabel.Text = $"+{reward.TotalReward}";
        _ownedEssenceValueLabel.Text = _runManager.CurrentEssence.ToString();
    }

    private void RevealNext()
    {
        if (_nextRevealIndex >= _revealTargets.Length)
        {
            FinishSequence();
            return;
        }

        var target = _revealTargets[_nextRevealIndex++];
        SetRevealState(target, true);
        target.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        target.Scale = Vector2.One * 0.96f;
        var tween = CreateTween().SetParallel(true);
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(target, "modulate", Colors.White, 0.2d);
        tween.TweenProperty(target, "scale", Vector2.One, 0.24d);

        if (_nextRevealIndex >= _revealTargets.Length)
        {
            FinishSequence();
        }
    }

    private void RevealAll()
    {
        foreach (var target in _revealTargets)
        {
            SetRevealState(target, true);
            target.Modulate = Colors.White;
            target.Scale = Vector2.One;
        }

        _nextRevealIndex = _revealTargets.Length;
        FinishSequence();
    }

    private void FinishSequence()
    {
        if (_sequenceFinished)
        {
            return;
        }

        _sequenceFinished = true;
        _continueButton.Visible = true;
        _continueButton.Modulate = Colors.White;
        _continueHintLabel.Visible = true;
        CallDeferred(nameof(FocusContinueButton));
    }

    private void FocusContinueButton()
    {
        if (_continueButton != null && IsInstanceValid(_continueButton))
        {
            _continueButton.GrabFocus();
        }
    }

    private void ShowMissingResult()
    {
        _enemyNameLabel.Text = "NO BATTLE RESULT";
        _continueHintLabel.Text = "SPACE — voltar ao combate";
        _continueButton.Visible = true;
        _sequenceFinished = true;
    }

    private void OnContinuePressed()
    {
        if (_continueButton.Disabled || _sceneTransitionManager.IsTransitioning)
        {
            return;
        }

        if (ShopScene == null)
        {
            GD.PrintErr("BattleResultController: ShopScene não foi configurada na cena.");
            return;
        }

        _continueButton.Disabled = true;
        _sceneTransitionManager.TransitionToPacked(ShopScene);
    }

    private static void SetRevealState(Control target, bool visible)
    {
        target.Visible = visible;
        if (!visible)
        {
            target.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
            target.Scale = Vector2.One * 0.96f;
        }
    }

    private static bool IsConfirmKey(InputEventKey keyEvent)
    {
        return keyEvent.Keycode == Key.Space ||
            keyEvent.PhysicalKeycode == Key.Space;
    }
}

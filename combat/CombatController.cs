using System;
using System.Text;
using Godot;

/// <summary>
/// Orquestra a primeira fatia de combate: um inimigo, prompts ofensivos e
/// defensivos, HP, postura e execução. A estrutura visual permanece na cena;
/// o controller apenas instancia cenas, acompanha o ritmo e comunica o resultado.
/// </summary>
public partial class CombatController : Node2D
{
    [Export]
    public PackedScene PromptScene { get; set; } = null!;

    [Export]
    public PackedScene EnemyScene { get; set; } = null!;

    [Export]
    public RhythmPatternResource AttackPattern { get; set; } = null!;

    [Export]
    public RhythmFairnessConfigResource FairnessConfig { get; set; } = null!;

    [Export]
    public Godot.Collections.Array<EnemyDataResource> EnemyRoster { get; set; } = new();

    [Export(PropertyHint.Range, "0,20,1")]
    public int StartingEnemyIndex { get; set; }

    [Export]
    public int EncounterSeed { get; set; } = 12345;

    [Export]
    public TimingConfig TimingConfig { get; set; } = null!;

    [Export]
    public BattleRewardConfig RewardConfig { get; set; } = null!;

    [Export]
    public PackedScene BattleResultScene { get; set; } = null!;

    [Export]
    public PackedScene VictoryScene { get; set; } = null!;

    [Export]
    public PackedScene CombatValueFeedbackScene { get; set; } = null!;

    [Export(PropertyHint.Range, "0.0,999.0,1.0")]
    public float BaseAttackDamage { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.25,2.0,0.25")]
    public float PromptLeadBeats { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "1,4,1")]
    public int ExecutionTargetDelayBeats { get; set; } = 2;

    [Export(PropertyHint.Range, "1,6,1")]
    public int ExecutionRecoveryGapBeats { get; set; } = 3;

    [Export]
    public bool DebugRhythm { get; set; } = true;

    [Export(PropertyHint.Range, "-1,6,1")]
    public int DebugBossPhase { get; set; } = -1;

    [Export]
    public bool DebugComboTrainingDummy { get; set; }

    [Export(PropertyHint.Range, "100.0,9999.0,100.0")]
    public float DebugComboDummyHealth { get; set; } = 1000.0f;

    private RhythmManager _rhythmManager = null!;
    private RunManager _runManager = null!;
    private SceneTransitionManager _sceneTransitionManager = null!;
    private ResourcePreloader _sceneResources = null!;
    private Node2D _promptContainer = null!;
    private Node2D _enemyContainer = null!;
    private EnemyBase _enemy = null!;
    private PlayerBase _player = null!;
    private RhythmPrompt? _activePrompt;
    private RhythmEventResource? _activePatternEvent;
    private RhythmEventResource? _queuedPatternEvent;
    private double _queuedPatternBeatPosition = -1.0d;
    private bool _hasQueuedPatternEvent;
    private int _executionTargetBeat = -1;
    private RhythmPromptType _activePromptType = RhythmPromptType.PlayerAttack;
    private float _activePromptDamage;
    private double _nextPatternSearchBeatPosition = 1.0d;
    private float _lastIncomingDamage;
    private float _lastPostureDamage;
    private CombatState _combatState = CombatState.Running;
    private RhythmPatternResource? _encounterPattern;
    private EnemyDataResource? _selectedEnemyData;
    private BossDataResource? _selectedBossData;
    private BossController? _bossController;
    private EnemyRhythmProfileResource? _activeRhythmProfile;
    private EncounterPatternStats? _encounterStats;
    private int _selectedEnemyIndex;
    private int _activeRhythmPhaseIndex = -1;
    private int _pendingRhythmPhaseIndex = -1;
    private int _activeBossPhaseIndex = -1;
    private int _pendingBossPhaseIndex = -1;
    private bool _bossPhaseTransitionActive;
    private int _transitioningBossPhaseIndex = -1;

    private Label _bpmLabel = null!;
    private Label _beatLabel = null!;
    private Label _musicPositionLabel = null!;
    private Label _promptTargetLabel = null!;
    private Label _circleSizeLabel = null!;
    private Label _currentActionLabel = null!;
    private Label _enemyBaseDamageLabel = null!;
    private Label _lastIncomingDamageLabel = null!;
    private Label _playerHpDebugLabel = null!;
    private ProgressBar _playerHealthBar = null!;
    private Label _playerHealthLabel = null!;
    private Label _combatDetailsLabel = null!;
    private Label _resultLabel = null!;
    private Label _resultDetailLabel = null!;
    private Label _damageLabel = null!;
    private Label _postureDamageLabel = null!;
    private Label _statusLabel = null!;
    private Label _beatIndicator = null!;
    private Label _clockSourceLabel = null!;
    private Label _comboLabel = null!;
    private Label _comboMultiplierLabel = null!;
    private Label _perfectStreakLabel = null!;
    private Label _comboFeedbackLabel = null!;
    private Label _enemySelectionLabel = null!;
    private Label _profileDebugLabel = null!;
    private Label _patternPreviewLabel = null!;
    private ArcaneComboDisplay _comboPanel = null!;
    private ArcaneCombatAtmosphere _arcaneAtmosphere = null!;
    private Control _combatValueFeedbackLayer = null!;
    private Control _debugPanel = null!;
    private Timer _executionDelayTimer = null!;
    private Timer _comboFeedbackTimer = null!;
    private Timer _encounterStartDelayTimer = null!;
    private Timer? _bossPhaseTransitionTimer;
    private Control? _bossPhaseTransitionPanel;
    private Label? _bossPhaseTransitionTitle;
    private Label? _bossPhaseTransitionSubtitle;
    private Label? _bossPhaseTransitionRules;
    private ShaderMaterial? _bossPhaseTransitionAuraMaterial;
    private ComboManager _comboManager = null!;
    private AudioManager _audioManager = null!;
    private readonly BattlePerformance _battlePerformance = new();
    private bool _battleResultQueued;
    private float _lastHealthDamage;
    private float _lastRawHealthDamage;
    private double _comboFeedbackTime;
    private double _comboLabelPunchTime;
    private double _perfectStreakPunchTime;
    private double _resultPunchTime;
    private Color _comboFeedbackColor = Colors.White;
    private double _beatFeedbackTime;
    private int _displayedCombo;
    private int _displayedComboTier;
    private float _basePlayerMaxHealth;
    private bool _secondBreathUsedThisCombat;
    private bool _unbrokenUsedThisCombat;
    private bool _riposteReady;
    private bool _encounterStartDelayActive;
    private bool _waitingForTransitionReveal;

    public EnemyRhythmProfileResource? ActiveRhythmProfile => _activeRhythmProfile;
    public EncounterPatternStats? EncounterStats => _encounterStats;
    public RhythmEventResource? ActivePatternEvent => _activePatternEvent;
    public int CurrentRhythmPhaseIndex => _activeRhythmPhaseIndex;
    public bool IsRhythmPhaseChangePending => _pendingRhythmPhaseIndex >= 0;
    public float LastRawHealthDamage => _lastRawHealthDamage;
    public float LastFinalHealthDamage => _lastHealthDamage;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
        _runManager = GetNode<RunManager>("/root/RunManager");
        _sceneTransitionManager = GetNode<SceneTransitionManager>(
            "/root/SceneTransitionManager");
        _sceneResources = GetNode<ResourcePreloader>("SceneResources");
        _promptContainer = GetNode<Node2D>("PromptContainer");
        _enemyContainer = GetNode<Node2D>("EnemyContainer");
        _player = GetNode<PlayerBase>("PlayerContainer/Player");
        _comboManager = GetNode<ComboManager>("ComboManager");
        _audioManager = GetNode<AudioManager>("AudioManager");
        _executionDelayTimer = GetNode<Timer>("ExecutionDelayTimer");
        _executionDelayTimer.Timeout += OnExecutionDelayTimeout;
        _comboFeedbackTimer = GetNode<Timer>("ComboFeedbackTimer");
        _comboFeedbackTimer.Timeout += OnComboFeedbackTimeout;
        _encounterStartDelayTimer = GetNode<Timer>("EncounterStartDelayTimer");
        _encounterStartDelayTimer.Timeout += OnEncounterStartDelayTimeout;
        _bossPhaseTransitionTimer = GetNodeOrNull<Timer>("BossPhaseTransitionTimer");
        if (_bossPhaseTransitionTimer != null)
        {
            _bossPhaseTransitionTimer.Timeout += OnBossPhaseTransitionTimeout;
        }

        _bpmLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/BpmLabel");
        _beatLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/BeatLabel");
        _musicPositionLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/MusicPositionLabel");
        _promptTargetLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/PromptTargetLabel");
        _circleSizeLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/CircleSizeLabel");
        _currentActionLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/CurrentActionLabel");
        _enemyBaseDamageLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/EnemyBaseDamageLabel");
        _lastIncomingDamageLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/LastIncomingDamageLabel");
        _playerHpDebugLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/PlayerHpLabel");
        _combatDetailsLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/CombatDetailsLabel");
        _playerHealthBar = GetNode<ProgressBar>("CanvasLayer/Ui/PlayerHealthBar");
        _playerHealthLabel = GetNode<Label>("CanvasLayer/Ui/PlayerHealthLabel");
        _resultLabel = GetNode<Label>("CanvasLayer/Ui/ResultLabel");
        _resultDetailLabel = GetNode<Label>("CanvasLayer/Ui/ResultDetailLabel");
        _damageLabel = GetNode<Label>("CanvasLayer/Ui/DamageLabel");
        _postureDamageLabel = GetNode<Label>("CanvasLayer/Ui/PostureDamageLabel");
        _statusLabel = GetNode<Label>("CanvasLayer/Ui/StatusLabel");
        _beatIndicator = GetNode<Label>("CanvasLayer/Ui/BeatIndicator");
        _clockSourceLabel = GetNode<Label>("CanvasLayer/Ui/DebugPanel/ClockSourceLabel");
        _comboPanel = GetNode<ArcaneComboDisplay>("CanvasLayer/Ui/ComboPanel");
        _arcaneAtmosphere = GetNode<ArcaneCombatAtmosphere>(
            "ArcaneCombatAtmosphere");
        _combatValueFeedbackLayer = GetNode<Control>(
            "CanvasLayer/Ui/CombatValueFeedbackLayer");
        _comboLabel = GetNode<Label>("CanvasLayer/Ui/ComboPanel/ComboLabel");
        _comboMultiplierLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/ComboMultiplierLabel");
        _perfectStreakLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/PerfectStreakLabel");
        _comboFeedbackLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/ComboFeedbackLabel");
        _enemySelectionLabel = GetNode<Label>(
            "CanvasLayer/Ui/PatternDebugPanel/EnemySelectionLabel");
        _profileDebugLabel = GetNode<Label>(
            "CanvasLayer/Ui/PatternDebugPanel/ProfileDebugLabel");
        _patternPreviewLabel = GetNode<Label>(
            "CanvasLayer/Ui/PatternDebugPanel/PatternPreviewLabel");
        _debugPanel = GetNode<Control>("CanvasLayer/Ui/DebugPanel");
        _bossPhaseTransitionPanel = GetNodeOrNull<Control>(
            "CanvasLayer/Ui/BossPhaseTransitionPanel");
        _bossPhaseTransitionTitle = GetNodeOrNull<Label>(
            "CanvasLayer/Ui/BossPhaseTransitionPanel/PhaseTitle");
        _bossPhaseTransitionSubtitle = GetNodeOrNull<Label>(
            "CanvasLayer/Ui/BossPhaseTransitionPanel/PhaseSubtitle");
        _bossPhaseTransitionRules = GetNodeOrNull<Label>(
            "CanvasLayer/Ui/BossPhaseTransitionPanel/PhaseRules");
        var bossPhaseAura = GetNodeOrNull<ColorRect>(
            "CanvasLayer/Ui/BossPhaseTransitionPanel/Aura");
        _bossPhaseTransitionAuraMaterial = bossPhaseAura?.Material as ShaderMaterial;
        _debugPanel.Visible = DebugRhythm;
        GetNode<Control>("CanvasLayer/Ui/PatternDebugPanel").Visible = DebugRhythm;

        _comboManager.ComboChanged += OnComboChanged;
        _comboManager.PerfectStreakChanged += OnPerfectStreakChanged;
        _comboManager.DamageMultiplierChanged += OnDamageMultiplierChanged;
        _comboManager.ComboMilestoneReached += OnComboMilestoneReached;
        _comboManager.ComboBroken += OnComboBroken;
        _comboManager.TimingResultRegistered += OnTimingResultRegistered;
        _battlePerformance.Reset();
        _battleResultQueued = false;
        _secondBreathUsedThisCombat = false;
        _unbrokenUsedThisCombat = false;
        _riposteReady = false;
        UpdateComboUi();

        ResolveSceneReferences();
        _basePlayerMaxHealth = _player.MaxHealth;
        var runWasInitialized = _runManager.IsRunInitialized;
        _runManager.EnsureRunStarted(_basePlayerMaxHealth);
        _comboManager.RestoreRunState(
            _runManager.CurrentCombo,
            _runManager.HighestCombo,
            _runManager.PerfectStreak);
        _displayedCombo = _comboManager.CurrentCombo;
        _displayedComboTier = _comboManager.CurrentComboTier;
        UpdateComboUi();
        _runManager.ClearLastBattleResult();
        FairnessConfig ??= new RhythmFairnessConfigResource();
        var encounterIndex = runWasInitialized
            ? _runManager.CurrentEncounterIndex
            : StartingEnemyIndex;
        if (!runWasInitialized)
        {
            _runManager.SetCurrentEncounter(encounterIndex);
        }

        _player.SetMaxHealth(_runManager.GetEffectivePlayerMaxHealth());
        SelectEnemyData(encounterIndex);
        BuildEncounterPattern();
        CreateEnemy();

        _player.HealthChanged += OnPlayerHealthChanged;
        _player.Died += OnPlayerDied;
        _player.SetCurrentHealth(_runManager.PlayerCurrentHP);
        OnPlayerHealthChanged(_player.CurrentHealth, _player.MaxHealth);

        ShowImportantResult(
            "AGUARDANDO ATAQUE",
            "Aperte SPACE quando o círculo amarelo entrar no azul");
        _damageLabel.Text = "DANO: --";
        _postureDamageLabel.Text = "RESISTÊNCIA: --";
        _lastIncomingDamageLabel.Text = "Last incoming damage: 0.0";
        _lastHealthDamage = 0.0f;
        _lastRawHealthDamage = 0.0f;
        _statusLabel.Text = $"{GetCurrentEnemyName()} aguarda seu ritmo.";

        BeginEncounterStartDelay();
        _rhythmManager.BeatStarted += OnBeatStarted;
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
        if (_bossController != null)
        {
            _bossController.ProcessRegeneration(
                delta,
                CanCountBossRegenerationTime());
        }

        if (!_bossPhaseTransitionActive &&
            (_combatState == CombatState.Running ||
             _combatState == CombatState.ResolvingPrompt))
        {
            TryApplyPendingRhythmPhase();
            TryScheduleNextPrompt();
            if (_combatState == CombatState.Running)
            {
                UpdateWaitingStatus();
            }
        }
        else if (!_bossPhaseTransitionActive &&
                 _combatState == CombatState.ExecutionPrompt)
        {
            TryScheduleExecutionPrompt();
        }

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

        UpdateComboFeedback(delta);
        UpdateComboPunches(delta);
        UpdateResultPunch(delta);
    }

    public override void _ExitTree()
    {
        if (_executionDelayTimer != null)
        {
            _executionDelayTimer.Timeout -= OnExecutionDelayTimeout;
        }

        if (_comboFeedbackTimer != null)
        {
            _comboFeedbackTimer.Timeout -= OnComboFeedbackTimeout;
        }

        if (_encounterStartDelayTimer != null)
        {
            _encounterStartDelayTimer.Timeout -= OnEncounterStartDelayTimeout;
        }

        if (_sceneTransitionManager != null)
        {
            _sceneTransitionManager.TransitionFinished -= OnSceneTransitionFinished;
        }

        if (_bossPhaseTransitionTimer != null)
        {
            _bossPhaseTransitionTimer.Timeout -= OnBossPhaseTransitionTimeout;
        }

        if (_rhythmManager != null)
        {
            _rhythmManager.BeatStarted -= OnBeatStarted;
        }

        if (_enemy != null && GodotObject.IsInstanceValid(_enemy))
        {
            DetachBossControllerSignals();
            _enemy.HealthChanged -= OnEnemyHealthChanged;
            _enemy.PostureChanged -= OnEnemyPostureChanged;
            _enemy.PostureBroken -= OnEnemyPostureBroken;
            _enemy.Died -= OnEnemyDied;
        }

        if (_player != null && GodotObject.IsInstanceValid(_player))
        {
            _player.HealthChanged -= OnPlayerHealthChanged;
            _player.Died -= OnPlayerDied;
        }

        if (_comboManager != null && GodotObject.IsInstanceValid(_comboManager))
        {
            _comboManager.ComboChanged -= OnComboChanged;
            _comboManager.PerfectStreakChanged -= OnPerfectStreakChanged;
            _comboManager.DamageMultiplierChanged -= OnDamageMultiplierChanged;
            _comboManager.ComboMilestoneReached -= OnComboMilestoneReached;
            _comboManager.ComboBroken -= OnComboBroken;
            _comboManager.TimingResultRegistered -= OnTimingResultRegistered;
        }
    }

    private void ResolveSceneReferences()
    {
        if (PromptScene == null)
        {
            var preloadedPromptScene = _sceneResources.GetResource("rhythm_prompt") as PackedScene;
            if (preloadedPromptScene != null)
            {
                PromptScene = preloadedPromptScene;
            }
        }

        if (EnemyScene == null)
        {
            var preloadedEnemyScene = _sceneResources.GetResource("enemy_base") as PackedScene;
            if (preloadedEnemyScene != null)
            {
                EnemyScene = preloadedEnemyScene;
            }
        }

        if (AttackPattern == null)
        {
            var preloadedPattern = _sceneResources.GetResource("fool_pattern") as RhythmPatternResource;
            if (preloadedPattern != null)
            {
                AttackPattern = preloadedPattern;
            }
        }
    }

    private void SelectEnemyData(int requestedIndex)
    {
        _activeRhythmProfile = null;
        _activeRhythmPhaseIndex = -1;
        _pendingRhythmPhaseIndex = -1;
        _activeBossPhaseIndex = -1;
        _pendingBossPhaseIndex = -1;
        _selectedBossData = null;
        if (EnemyRoster.Count == 0)
        {
            _selectedEnemyIndex = 0;
            _selectedEnemyData = null;
            return;
        }

        _selectedEnemyIndex = Math.Clamp(requestedIndex, 0, EnemyRoster.Count - 1);
        _selectedEnemyData = EnemyRoster[_selectedEnemyIndex];
        _selectedBossData = _selectedEnemyData as BossDataResource;
        if (_selectedBossData != null && _selectedBossData.PhaseCount > 0)
        {
            _activeBossPhaseIndex = DebugBossPhase >= 0
                ? Math.Clamp(DebugBossPhase, 0, _selectedBossData.PhaseCount - 1)
                : 0;
        }
    }

    private void BuildEncounterPattern()
    {
        var phaseIndex = -1;
        EnemyRhythmProfileResource? profile;
        if (_selectedBossData != null && _activeBossPhaseIndex >= 0)
        {
            phaseIndex = _activeBossPhaseIndex;
            profile = _selectedBossData.GetBossPhase(phaseIndex)?.RhythmProfile;
        }
        else
        {
            profile = _selectedEnemyData == null
                ? null
                : _selectedEnemyData.GetRhythmProfileForHealthPercent(
                    1.0f,
                    out phaseIndex);
        }

        _activeRhythmPhaseIndex = phaseIndex;
        _pendingRhythmPhaseIndex = -1;
        BuildEncounterPattern(profile, phaseIndex);
    }

    private void BuildEncounterPattern(
        EnemyRhythmProfileResource? profile,
        int phaseIndex)
    {
        _encounterStats = null;
        if (AttackPattern == null)
        {
            _encounterPattern = null;
            _activeRhythmProfile = profile;
            ShowError("Chart musical-base não foi encontrado na cena.");
            return;
        }

        if (profile == null)
        {
            _encounterPattern = AttackPattern;
            _activeRhythmProfile = null;
            return;
        }

        _activeRhythmProfile = profile;
        var generationResult = EncounterPatternGenerator.Generate(
            AttackPattern,
            profile,
            GetEncounterSeed(phaseIndex),
            BaseAttackDamage,
            GetActiveEnemyAttackDamage(),
            FairnessConfig);
        _encounterPattern = generationResult.Pattern;
        _encounterStats = generationResult.Stats;
        GD.Print(
            $"ENCOUNTER_PATTERN enemy={GetCurrentEnemyName()} " +
            $"profile={profile.ProfileName} phase={phaseIndex} seed={EncounterSeed} " +
            $"events={_encounterStats.EventCount} " +
            $"player={_encounterStats.PlayerAttackCount} " +
            $"enemy={_encounterStats.EnemyAttackCount} " +
            $"half={_encounterStats.HalfBeatCount} " +
            $"bursts={_encounterStats.BurstCount} " +
            $"feints={_encounterStats.FeintCount} " +
            $"fades={_encounterStats.FadeCount} " +
            $"decoys={_encounterStats.DecoyCount} " +
            $"min_spacing={_encounterStats.MinimumTargetSpacingBeats:0.00}");
    }

    private ulong GetEncounterSeed(int phaseIndex)
    {
        var baseSeed = unchecked((ulong)Math.Max(0, EncounterSeed));
        if (phaseIndex < 0)
        {
            return baseSeed;
        }

        return baseSeed + (ulong)((phaseIndex + 1) * 104729);
    }

    private float GetActiveEnemyAttackDamage()
    {
        if (_selectedBossData != null && _activeBossPhaseIndex >= 0)
        {
            var phaseDamage = _selectedBossData.GetBossPhase(_activeBossPhaseIndex)?.AttackDamage;
            if (phaseDamage.HasValue)
            {
                return Math.Max(0.0f, phaseDamage.Value);
            }
        }

        return Math.Max(0.0f, _selectedEnemyData?.BaseAttackDamage ?? 20.0f);
    }

    private bool CanCountBossRegenerationTime()
    {
        return _selectedBossData != null &&
            _bossController != null &&
            !_bossPhaseTransitionActive &&
            _combatState == CombatState.ResolvingPrompt &&
            IsPromptActive() &&
            _enemy != null &&
            !_enemy.IsDead &&
            !_enemy.IsStaggered;
    }

    private void CreateEnemy()
    {
        var enemyScene = _selectedEnemyData?.EnemyScene ?? EnemyScene;
        if (enemyScene == null)
        {
            ShowError("PackedScene do inimigo não foi encontrado na cena.");
            return;
        }

        try
        {
            _enemy = enemyScene.Instantiate<EnemyBase>();
            if (_selectedEnemyData != null)
            {
                _enemy.Configure(_selectedEnemyData);
            }

            if (DebugComboTrainingDummy)
            {
                _enemy.MaxHealth = Math.Max(_enemy.MaxHealth, DebugComboDummyHealth);
            }

            _enemyContainer.AddChild(_enemy);
            _arcaneAtmosphere.ConfigureEnemy(_enemy.EnemyName);

            _bossController = _enemy.GetNodeOrNull<BossController>("BossController");
            if (_selectedBossData != null)
            {
                if (_bossController == null)
                {
                    ShowError("BossController não foi configurado na cena do boss.");
                    return;
                }

                _bossController.Configure(_selectedBossData);
                _bossController.RegenerationStarted += OnBossRegenerationStarted;
                _bossController.RegenerationStopped += OnBossRegenerationStopped;
                _bossController.RegenerationTick += OnBossRegenerationTick;
                if (!_bossController.ActivatePhase(_activeBossPhaseIndex))
                {
                    ShowError("As fases do boss não foram configuradas corretamente.");
                    return;
                }

                if (DebugBossPhase >= 0)
                {
                    var debugPhase = _selectedBossData.GetBossPhase(_activeBossPhaseIndex);
                    if (debugPhase != null)
                    {
                        _enemy.SetCurrentHealth(
                            _enemy.MaxHealth * _selectedBossData.GetPhaseStartHealthPercent(
                                _activeBossPhaseIndex));
                    }
                }
            }

            _enemy.HealthChanged += OnEnemyHealthChanged;
            _enemy.PostureChanged += OnEnemyPostureChanged;
            _enemy.PostureBroken += OnEnemyPostureBroken;
            _enemy.Died += OnEnemyDied;
            OnEnemyHealthChanged(_enemy.CurrentHealth, _enemy.MaxHealth);
            OnEnemyPostureChanged(_enemy.CurrentPosture, _enemy.MaxPosture);
        }
        catch (Exception exception)
        {
            ShowError($"Falha ao instanciar o inimigo: {exception.Message}");
            GD.PrintErr(exception);
        }
    }

    private void OnBeatStarted(int beatIndex, double beatTime)
    {
        _beatFeedbackTime = 0.18d;
        _beatIndicator.Text = $"BEAT {beatIndex + 1}";
        if (_bossController != null)
        {
            _bossController.ProcessBeat(CanCountBossRegenerationTime());
        }

        if (_bossPhaseTransitionActive)
        {
            return;
        }

        if (_combatState == CombatState.Running)
        {
            TryScheduleNextPrompt();
            UpdateWaitingStatus();
        }
        else if (_combatState == CombatState.ExecutionPrompt)
        {
            TryScheduleExecutionPrompt();
        }
    }

    private void TryScheduleNextPrompt()
    {
        if (_encounterStartDelayActive ||
            _bossPhaseTransitionActive ||
            (_combatState != CombatState.Running &&
             _combatState != CombatState.ResolvingPrompt) ||
            _encounterPattern == null)
        {
            return;
        }

        if (!_hasQueuedPatternEvent)
        {
            var currentBeatPosition = Math.Max(
                0.0d,
                _rhythmManager.GetMusicPosition() / _rhythmManager.BeatDuration);
            var earliestTargetBeat = IsPromptActive()
                ? currentBeatPosition + 0.001d
                : currentBeatPosition + GetMinimumTelegraphBeats();
            var firstPossibleBeatPosition = Math.Max(
                _nextPatternSearchBeatPosition,
                earliestTargetBeat);
            if (!_encounterPattern.TryGetNextEvent(
                    firstPossibleBeatPosition,
                    out var targetBeatPosition,
                    out var rhythmEvent) ||
                rhythmEvent == null)
            {
                return;
            }

            _nextPatternSearchBeatPosition = targetBeatPosition + 0.001d;
            _queuedPatternBeatPosition = targetBeatPosition;
            _queuedPatternEvent = rhythmEvent;
            _hasQueuedPatternEvent = true;
        }

        if (IsPromptActive())
        {
            var currentTime = _rhythmManager.GetMusicPosition();
            var queuedStartTime = GetQueuedPromptStartTime();
            if (currentTime >= queuedStartTime)
            {
                // O prompt seguinte não cabe mais com o telegraph global sem
                // sobrepor o atual. Descartar preserva a música e a justiça;
                // o próximo candidato será buscado no futuro.
                _nextPatternSearchBeatPosition = Math.Max(
                    _nextPatternSearchBeatPosition,
                    _queuedPatternBeatPosition + 0.001d);
                _queuedPatternEvent = null;
                _queuedPatternBeatPosition = -1.0d;
                _hasQueuedPatternEvent = false;
            }

            return;
        }

        if (_queuedPatternEvent == null ||
            _rhythmManager.GetMusicPosition() < GetQueuedPromptStartTime())
        {
            return;
        }

        var nextEvent = _queuedPatternEvent;
        var nextEventBeatPosition = _queuedPatternBeatPosition;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        CreatePrompt(nextEvent, nextEventBeatPosition);
    }

    private double GetQueuedPromptStartTime()
    {
        var safeLeadBeats = GetMinimumTelegraphBeats();
        return _rhythmManager.GetBeatTime(_queuedPatternBeatPosition) -
            (_rhythmManager.BeatDuration * safeLeadBeats);
    }

    private double GetMinimumTelegraphBeats()
    {
        var configuredMinimum = FairnessConfig?.MinimumTelegraphBeats ?? 1.25f;
        var profileTelegraph = _activeRhythmProfile?.TelegraphBeats ?? PromptLeadBeats;
        return Math.Max(
            0.5d,
            Math.Max(
                configuredMinimum,
                Math.Max(0.5d, profileTelegraph)));
    }

    private bool IsPromptActive()
    {
        return _activePrompt != null &&
            GodotObject.IsInstanceValid(_activePrompt) &&
            !_activePrompt.IsResolved;
    }

    private void CreatePrompt(
        RhythmEventResource rhythmEvent,
        double targetBeatPosition)
    {
        var promptType = rhythmEvent.GetPromptType();
        var promptDamage = rhythmEvent.Damage > 0.0f
            ? rhythmEvent.Damage
            : promptType == RhythmPromptType.EnemyAttack
                ? GetActiveEnemyAttackDamage()
                : BaseAttackDamage;
        _activePatternEvent = rhythmEvent;
        CreatePrompt(
            promptType,
            promptDamage,
            targetBeatPosition,
            rhythmEvent.GetPromptBehavior());
    }

    private void CreatePrompt(
        RhythmPromptType promptType,
        float promptDamage,
        double targetBeatPosition,
        RhythmPromptBehavior promptBehavior = RhythmPromptBehavior.Normal)
    {
        if (_bossPhaseTransitionActive)
        {
            return;
        }

        if (PromptScene == null)
        {
            ShowError("PackedScene do prompt não foi encontrado na cena.");
            return;
        }

        try
        {
            var prompt = PromptScene.Instantiate<RhythmPrompt>();
            _promptContainer.AddChild(prompt);
            prompt.Resolved += OnAttackPromptResolved;
            _activePromptType = promptType;
            _activePromptDamage = promptDamage;
            prompt.SetPromptType(promptType);
            var safePromptBehavior = promptType == RhythmPromptType.Execution
                ? RhythmPromptBehavior.Normal
                : promptBehavior;

            if (_activePromptType == RhythmPromptType.EnemyAttack)
            {
                prompt.SetPromptPresentation(
                    "[!] ENEMY ATTACK [!]",
                    new Color(1.0f, 0.34f, 0.34f, 1.0f),
                    7.0f);
                if (_enemy != null && !_enemy.IsDefeated)
                {
                    _enemy.PlayAttackFeedback();
                }
                _arcaneAtmosphere.PlayEnemyTelegraph(
                    (float)(_rhythmManager.BeatDuration * GetMinimumTelegraphBeats()));
            }
            else if (_activePromptType == RhythmPromptType.Execution)
            {
                var executionEmphasis = 1.35f +
                    _runManager.Build.GetPrimaryValue(UpgradeIds.BreakingPoint, 0.0f);
                prompt.SetPromptPresentation(
                    "[!] EXECUTION [!]",
                    new Color(0.72f, 0.42f, 1.0f, 1.0f),
                    9.0f,
                    executionEmphasis);
                _audioManager.PlayExecutionReady();
            }
            else
            {
                prompt.SetPromptPresentation(
                    "PLAYER ATTACK",
                    new Color(1.0f, 0.72f, 0.3f, 1.0f));
            }

            var profile = _activeRhythmProfile;
            prompt.Configure(
                _rhythmManager.GetBeatTime(targetBeatPosition),
                _rhythmManager.BeatDuration,
                safePromptBehavior,
                (float)GetMinimumTelegraphBeats(),
                profile?.FadeBeatsBeforeTarget ?? 0.5f,
                profile?.FeintStrength ?? 0.24f);
            _activePrompt = prompt;
            _combatState = _activePromptType == RhythmPromptType.Execution
                ? CombatState.ExecutionPrompt
                : CombatState.ResolvingPrompt;
            UpdateCurrentActionLabels();
            _statusLabel.Text = _activePromptType switch
            {
                RhythmPromptType.EnemyAttack => "ENEMY ATTACK — pressione SPACE para defender",
                RhythmPromptType.Execution => "EXECUTION — pressione SPACE no timing desejado",
                _ => "PLAYER ATTACK — pressione SPACE no timing desejado",
            };
        }
        catch (Exception exception)
        {
            ShowError($"Falha ao instanciar o prompt: {exception.Message}");
            GD.PrintErr(exception);
        }
    }

    private void OnAttackPromptResolved(
        int result,
        float precisionPercent,
        float offsetMilliseconds,
        string direction,
        string resultName)
    {
        var timingResult = (TimingResult)result;
        var resolvedAction = _activePromptType;
        var resolvedBaseDamage = _activePromptDamage;
        _activePrompt = null;
        _activePatternEvent = null;

        if (resolvedAction == RhythmPromptType.EnemyAttack)
        {
            _arcaneAtmosphere.StopEnemyTelegraph();
        }

        if (resolvedAction == RhythmPromptType.Execution)
        {
            ResolveExecution(
                timingResult,
                precisionPercent,
                offsetMilliseconds,
                direction);
            return;
        }

        if (resolvedAction == RhythmPromptType.EnemyAttack)
        {
            ResolveEnemyAttack(
                timingResult,
                resolvedBaseDamage,
                precisionPercent,
                offsetMilliseconds,
                direction);
        }
        else
        {
            ResolvePlayerAttack(
                timingResult,
                resolvedBaseDamage,
                precisionPercent,
                offsetMilliseconds,
                direction,
                resultName);
        }

        if (_combatState == CombatState.ResolvingPrompt)
        {
            _combatState = CombatState.Running;
            TryApplyPendingRhythmPhase();
            TryScheduleNextPrompt();
        }
    }

    private void ResolvePlayerAttack(
        TimingResult timingResult,
        float baseDamage,
        float precisionPercent,
        float offsetMilliseconds,
        string direction,
        string resultName)
    {
        _audioManager.PlayTimingFeedback(
            RhythmPromptType.PlayerAttack,
            timingResult);
        var comboMultiplier = _comboManager.GetDamageMultiplier();
        var rawDamage = DamageCalculator.CalculatePlayerAttackDamage(
            baseDamage,
            timingResult,
            TimingConfig,
            comboMultiplier) * GetPerfectAttackUpgradeMultiplier();
        if (timingResult == TimingResult.Perfect && _riposteReady)
        {
            _riposteReady = false;
        }
        var armorActive = _enemy != null && _enemy.IsArmorActive;
        var damage = DamageCalculator.ApplyArmorToHealthDamage(
            rawDamage,
            armorActive,
            _enemy?.ArmorHealthDamageMultiplier ?? 1.0f);
        damage = _bossController?.ClampHealthDamage(damage) ?? damage;
        var postureDamage = DamageCalculator.CalculatePostureDamage(
            RhythmPromptType.PlayerAttack,
            timingResult,
            TimingConfig) * GetOffensivePostureMultiplier(timingResult);
        _lastRawHealthDamage = rawDamage;
        _lastHealthDamage = damage;
        _lastPostureDamage = postureDamage;

        SetResultFeedback(
            resultName,
            timingResult,
            precisionPercent,
            offsetMilliseconds,
            direction);
        _postureDamageLabel.Text = $"RESISTÊNCIA: -{postureDamage:0.0}";
        ShowCombatValueFeedback(
            damage,
            postureDamage,
            incoming: false,
            timingResult: timingResult);
        _arcaneAtmosphere.PlayPlayerImpact(
            damage,
            postureDamage,
            timingResult);

        if (damage > 0.0f && _enemy != null && !_enemy.IsDead)
        {
            _damageLabel.Text = $"DANO: {damage:0.0}";
            _damageLabel.Modulate = GetResultColor(timingResult);
            _enemy.TakeDamage(damage);

            if (_combatState == CombatState.EnemyDead)
            {
                RegisterComboResult(timingResult);
                return;
            }

            if (_pendingBossPhaseIndex >= 0)
            {
                _statusLabel.Text =
                    $"{resultName} — limite alcançado; a próxima fase se aproxima.";
                RegisterComboResult(timingResult);
                _combatState = CombatState.Running;
                TryApplyPendingRhythmPhase();
                return;
            }

            _statusLabel.Text = "IMPACTO DIRETO";
        }
        else
        {
            _damageLabel.Text = "DANO: 0";
            _damageLabel.Modulate = GetResultColor(timingResult);
        }

        if (_enemy == null || _enemy.IsDead)
        {
            RegisterComboResult(timingResult);
            return;
        }

        if (postureDamage > 0.0f)
        {
            _enemy.TakePostureDamage(postureDamage);
        }

        if (_combatState == CombatState.EnemyStaggered)
        {
            RegisterComboResult(timingResult);
            return;
        }

        _statusLabel.Text = armorActive && rawDamage > 0.0f
            ? "A ARMADURA ABSORVEU PARTE DO IMPACTO"
            : timingResult switch
            {
                TimingResult.Perfect => "IMPACTO DIRETO",
                TimingResult.Good or TimingResult.Ok => "PRESSÃO NA RESISTÊNCIA",
                _ => "ATAQUE PERDIDO",
            };
        RegisterComboResult(timingResult);
    }

    private void ResolveEnemyAttack(
        TimingResult timingResult,
        float baseDamage,
        float precisionPercent,
        float offsetMilliseconds,
        string direction)
    {
        _audioManager.PlayTimingFeedback(
            RhythmPromptType.EnemyAttack,
            timingResult);
        var rawIncomingDamage = DamageCalculator.CalculateIncomingDamage(
            baseDamage,
            timingResult,
            TimingConfig);
        var incomingDamage = rawIncomingDamage * GetIncomingDamageMultiplier(timingResult);
        if (timingResult == TimingResult.Miss &&
            _runManager.HasUpgrade(UpgradeIds.SecondBreath) &&
            !_secondBreathUsedThisCombat &&
            _player.CurrentHealth > 1.0f &&
            incomingDamage >= _player.CurrentHealth)
        {
            incomingDamage = _player.CurrentHealth - 1.0f;
            _secondBreathUsedThisCombat = true;
        }

        var defenseResult = GetDefenseResultName(timingResult);
        _lastIncomingDamage = incomingDamage;
        _lastRawHealthDamage = rawIncomingDamage;
        _lastHealthDamage = incomingDamage;
        _lastIncomingDamageLabel.Text = $"Last incoming damage: {incomingDamage:0.0}";

        var postureDamage = DamageCalculator.CalculatePostureDamage(
            RhythmPromptType.EnemyAttack,
            timingResult,
            TimingConfig) * GetDefensivePostureMultiplier(timingResult);
        _lastPostureDamage = postureDamage;
        _postureDamageLabel.Text = $"RESISTÊNCIA: -{postureDamage:0.0}";

        _damageLabel.Text = $"DANO RECEBIDO: {incomingDamage:0.0}";
        _damageLabel.Modulate = GetResultColor(timingResult);
        ShowCombatValueFeedback(
            incomingDamage,
            postureDamage,
            incoming: true,
            timingResult: timingResult);

        if (incomingDamage > 0.0f)
        {
            _player.TakeDamage(incomingDamage);
        }

        if (_combatState == CombatState.PlayerDead)
        {
            RegisterComboResult(timingResult);
            return;
        }

        SetResultFeedback(
            defenseResult,
            timingResult,
            precisionPercent,
            offsetMilliseconds,
            direction);

        if (timingResult == TimingResult.Perfect)
        {
            _riposteReady = _runManager.HasUpgrade(UpgradeIds.Riposte);
            var reflectionHeal = _runManager.Build.GetPrimaryValue(
                UpgradeIds.Reflection,
                0.0f);
            if (reflectionHeal > 0.0f)
            {
                _player.Heal(reflectionHeal);
            }
        }

        if (postureDamage > 0.0f && _enemy != null && !_enemy.IsDead)
        {
            _enemy.TakePostureDamage(postureDamage);
        }

        if (_combatState == CombatState.EnemyStaggered)
        {
            RegisterComboResult(timingResult);
            return;
        }

        if (_combatState == CombatState.ResolvingPrompt)
        {
            _statusLabel.Text = defenseResult;
        }

        RegisterComboResult(timingResult);
    }

    private void SetResultFeedback(
        string displayResult,
        TimingResult timingResult,
        float precisionPercent,
        float offsetMilliseconds,
        string direction)
    {
        _resultPunchTime = 0.18d;
        _resultLabel.Text = displayResult;
        _resultLabel.Modulate = GetResultColor(timingResult);
        _resultDetailLabel.Text = string.Empty;
        _resultLabel.Visible = false;
        _resultDetailLabel.Visible = false;
    }

    private void ShowImportantResult(string title, string detail)
    {
        _resultLabel.Visible = true;
        _resultDetailLabel.Visible = true;
        _resultLabel.Text = title;
        _resultDetailLabel.Text = detail;
    }

    private void ShowCombatValueFeedback(
        float healthDamage,
        float resistanceDamage,
        bool incoming,
        TimingResult timingResult)
    {
        if (CombatValueFeedbackScene == null ||
            _combatValueFeedbackLayer == null)
        {
            return;
        }

        var feedback = CombatValueFeedbackScene.Instantiate<CombatValueFeedback>();
        _combatValueFeedbackLayer.AddChild(feedback);

        var origin = incoming
            ? new Vector2(604.0f, 304.0f)
            : new Vector2(298.0f, 304.0f);
        feedback.Play(
            origin,
            healthDamage,
            resistanceDamage,
            incoming,
            timingResult,
            incoming ? 1.0f : -1.0f);
    }

    private static string GetDefenseResultName(TimingResult result)
    {
        return result switch
        {
            TimingResult.Perfect => "PARRY",
            TimingResult.Good => "BLOCK",
            TimingResult.Ok => "PARTIAL BLOCK",
            _ => "HIT",
        };
    }

    private void ResolveExecution(
        TimingResult timingResult,
        float precisionPercent,
        float offsetMilliseconds,
        string direction)
    {
        if (_enemy == null || _enemy.IsDead || _player.IsDead)
        {
            return;
        }

        _audioManager.PlayTimingFeedback(
            RhythmPromptType.Execution,
            timingResult);
        var executionDamage = DamageCalculator.CalculateExecutionDamage(
            timingResult,
            TimingConfig) * (1.0f +
                _runManager.Build.GetPrimaryValue(UpgradeIds.Executioner));
        executionDamage = _bossController?.ClampHealthDamage(executionDamage) ?? executionDamage;
        var executionResult = GetExecutionResultName(timingResult);
        _lastRawHealthDamage = executionDamage;
        _lastHealthDamage = executionDamage;

        SetResultFeedback(
            executionResult,
            timingResult,
            precisionPercent,
            offsetMilliseconds,
            direction);
        if (timingResult != TimingResult.Perfect)
        {
            _resultLabel.Modulate = GetResultColor(TimingResult.Miss);
        }

        _lastPostureDamage = 0.0f;
        _postureDamageLabel.Text = "RESISTÊNCIA: --";
        _damageLabel.Text = executionDamage > 0.0f
            ? $"EXECUTION DAMAGE: {executionDamage:0.0}"
            : "EXECUTION DAMAGE: 0";
        _damageLabel.Modulate = timingResult == TimingResult.Perfect
            ? GetResultColor(timingResult)
            : GetResultColor(TimingResult.Miss);
        ShowCombatValueFeedback(
            executionDamage,
            0.0f,
            incoming: false,
            timingResult: timingResult);
        _arcaneAtmosphere.PlayPlayerImpact(
            executionDamage,
            0.0f,
            timingResult);

        if (executionDamage > 0.0f)
        {
            if (timingResult == TimingResult.Perfect)
            {
                _battlePerformance.RegisterSuccessfulExecution();
                var executionHeal = _runManager.Build.GetPrimaryValue(
                    UpgradeIds.SoulCut);
                if (executionHeal > 0.0f)
                {
                    _player.Heal(executionHeal);
                }
            }

            _enemy.TakeDamage(executionDamage);

            if (_combatState == CombatState.EnemyDead)
            {
                RegisterComboResult(timingResult);
                return;
            }

            if (_pendingBossPhaseIndex >= 0)
            {
                _enemy.PlayExecutionFeedback();
                _executionTargetBeat = -1;
                _combatState = CombatState.Running;
                _statusLabel.Text =
                    "EXECUTION — limite alcançado; preparando a próxima fase.";
                RegisterComboResult(timingResult);
                TryApplyPendingRhythmPhase();
                return;
            }
        }

        if (_enemy.IsDead || _combatState == CombatState.PlayerDead)
        {
            return;
        }

        _enemy.RestorePosture();
        if (timingResult == TimingResult.Perfect)
        {
            _enemy.PlayExecutionFeedback();
        }

        _executionTargetBeat = -1;
        _combatState = CombatState.Running;

        // O evento que ficou imediatamente depois do stagger não deve aparecer
        // colado à execution. O pattern retoma do próximo ponto seguro.
        var safeRecoveryGapBeats = Math.Max(1, ExecutionRecoveryGapBeats);
        _nextPatternSearchBeatPosition = Math.Max(
            _nextPatternSearchBeatPosition,
            _rhythmManager.CurrentBeat + safeRecoveryGapBeats);
        _statusLabel.Text = timingResult == TimingResult.Perfect
            ? "PERFECT EXECUTION — resistência restaurada; combate retomado."
            : "EXECUTION FAILED — oportunidade perdida; resistência restaurada; combate retomado.";
        RegisterComboResult(timingResult);
        TryApplyPendingRhythmPhase();
        TryScheduleNextPrompt();
    }

    private static string GetExecutionResultName(TimingResult result)
    {
        return result switch
        {
            TimingResult.Perfect => "PERFECT EXECUTION",
            _ => "EXECUTION FAILED",
        };
    }

    private void RegisterComboResult(TimingResult timingResult)
    {
        if (timingResult == TimingResult.Perfect)
        {
            _bossController?.NotifyPerfect();
        }

        var okReductionPercent = 50;
        if (timingResult == TimingResult.Ok &&
            _comboManager.CurrentCombo >= 10 &&
            _runManager.HasUpgrade(UpgradeIds.Unbroken) &&
            !_unbrokenUsedThisCombat)
        {
            okReductionPercent = 25;
            _unbrokenUsedThisCombat = true;
        }

        _comboManager.RegisterTimingResult(timingResult, okReductionPercent);
        _battlePerformance.RegisterTimingResult(
            timingResult,
            _comboManager.CurrentCombo,
            _comboManager.HighestCombo);
    }

    private float GetPerfectAttackUpgradeMultiplier()
    {
        if (_runManager == null || !_runManager.IsRunInitialized)
        {
            return 1.0f;
        }

        var build = _runManager.Build;
        var multiplier = 1.0f +
            build.GetPrimaryValue(UpgradeIds.ArcaneEdge) +
            build.GetPrimaryValue(UpgradeIds.SharpRhythm) +
            build.GetPrimaryValue(UpgradeIds.GlassArcana);

        if (_enemy != null &&
            _enemy.MaxHealth > 0.0f &&
            _enemy.CurrentHealth / _enemy.MaxHealth <=
                build.GetSecondaryValue(UpgradeIds.FinalBeat, 0.30f))
        {
            multiplier += build.GetPrimaryValue(UpgradeIds.FinalBeat);
        }

        if (_player != null &&
            _player.MaxHealth > 0.0f &&
            _player.CurrentHealth / _player.MaxHealth <=
                build.GetSecondaryValue(UpgradeIds.BloodTempo, 0.35f))
        {
            multiplier += build.GetPrimaryValue(UpgradeIds.BloodTempo);
        }

        if (_comboManager.CurrentCombo >= 20)
        {
            multiplier += build.GetTertiaryValue(UpgradeIds.Momentum);
        }
        else if (_comboManager.CurrentCombo >= 10)
        {
            multiplier += build.GetSecondaryValue(UpgradeIds.Momentum);
        }
        else if (_comboManager.CurrentCombo >= 5)
        {
            multiplier += build.GetPrimaryValue(UpgradeIds.Momentum);
        }

        if (_riposteReady)
        {
            multiplier += build.GetPrimaryValue(UpgradeIds.Riposte);
        }

        return Math.Max(0.0f, multiplier);
    }

    private float GetOffensivePostureMultiplier(TimingResult timingResult)
    {
        if (timingResult == TimingResult.Miss)
        {
            return 1.0f;
        }

        var build = _runManager.Build;
        var multiplier = 1.0f + build.GetPrimaryValue(UpgradeIds.CrackedSigil);
        if (timingResult == TimingResult.Perfect)
        {
            multiplier *= 1.0f - build.GetSecondaryValue(UpgradeIds.SharpRhythm);
            var relentlessThreshold = build.GetSecondaryValue(
                UpgradeIds.Relentless,
                3.0f);
            if (_comboManager.PerfectStreak + 1 >= relentlessThreshold)
            {
                multiplier += build.GetPrimaryValue(UpgradeIds.Relentless);
            }
        }

        return Math.Max(0.0f, multiplier);
    }

    private float GetDefensivePostureMultiplier(TimingResult timingResult)
    {
        if (timingResult != TimingResult.Perfect)
        {
            return 1.0f;
        }

        var build = _runManager.Build;
        return Math.Max(
            0.0f,
            1.0f +
            build.GetPrimaryValue(UpgradeIds.Pressure) +
            build.GetPrimaryValue(UpgradeIds.CursedRhythm));
    }

    private float GetIncomingDamageMultiplier(TimingResult timingResult)
    {
        var build = _runManager.Build;
        var multiplier = 1.0f;
        if (timingResult == TimingResult.Miss)
        {
            multiplier *= 1.0f - build.GetPrimaryValue(UpgradeIds.ArcaneGuard);
            multiplier *= 1.0f + build.GetSecondaryValue(UpgradeIds.CursedRhythm);
        }
        else if (timingResult == TimingResult.Good)
        {
            multiplier *= 1.0f - build.GetPrimaryValue(UpgradeIds.SteadyHand);
        }

        return Math.Max(0.0f, multiplier);
    }

    private void OnComboChanged(int currentCombo, int highestCombo)
    {
        _runManager.SetComboState(
            currentCombo,
            highestCombo,
            _comboManager.PerfectStreak);

        if (currentCombo > _displayedCombo)
        {
            _comboPanel.PlayComboIncrease();
        }

        _displayedCombo = currentCombo;
        _comboLabelPunchTime = 0.18d;
        UpdateComboUi();
    }

    private void OnPerfectStreakChanged(int perfectStreak)
    {
        _runManager.SetComboState(
            _comboManager.CurrentCombo,
            _comboManager.HighestCombo,
            perfectStreak);

        if (perfectStreak >= 2)
        {
            _perfectStreakPunchTime = 0.18d;
            _comboPanel.PlayPerfectStreak();
            _audioManager.PlayPerfectStreak(perfectStreak);
        }

        UpdateComboUi();
    }

    private void OnDamageMultiplierChanged(float damageMultiplier, int comboTier)
    {
        if (comboTier > _displayedComboTier)
        {
            _comboPanel.PlayTierUp();
        }

        _displayedComboTier = comboTier;
        UpdateComboUi();
    }

    private void OnComboMilestoneReached(int combo, string milestoneText)
    {
        var flowHeal = _runManager.Build.GetPrimaryValue(
            UpgradeIds.FlowState,
            0.0f);
        if (flowHeal > 0.0f)
        {
            _player.Heal(flowHeal);
        }

        var safeMilestoneText = string.IsNullOrWhiteSpace(milestoneText)
            ? "FLOW UP!"
            : milestoneText;
        ShowComboFeedback(
            $"{combo} COMBO\n{safeMilestoneText}",
            new Color(1.0f, 0.82f, 0.34f, 1.0f));
        _audioManager.PlayComboMilestone(_comboManager.CurrentComboTier);
    }

    private void OnComboBroken(int previousCombo)
    {
        _comboPanel.PlayComboBreak();
        ShowComboFeedback(
            "COMBO BREAK",
            new Color(1.0f, 0.38f, 0.38f, 1.0f));
        _audioManager.PlayComboBreak();
    }

    private void OnTimingResultRegistered(
        int result,
        int currentCombo,
        int perfectStreak)
    {
        UpdateComboUi();
    }

    private void UpdateComboUi()
    {
        if (_comboManager == null || _comboLabel == null)
        {
            return;
        }

        _comboLabel.Text = $"COMBO x{_comboManager.CurrentCombo}";
        _comboMultiplierLabel.Text =
            $"DMG x{_comboManager.CurrentDamageMultiplier:0.00}";
        _perfectStreakLabel.Visible = _comboManager.PerfectStreak >= 2;
        _perfectStreakLabel.Text = _perfectStreakLabel.Visible
            ? $"PERFECT ×{_comboManager.PerfectStreak}"
            : string.Empty;

        var tierColor = GetComboTierColor(_comboManager.CurrentComboTier);
        _comboLabel.Modulate = tierColor;
        _comboMultiplierLabel.Modulate = tierColor.Lerp(
            new Color(1.0f, 0.82f, 0.3f, 1.0f),
            0.45f);
        _comboPanel.SetComboState(
            _comboManager.CurrentCombo,
            _comboManager.CurrentComboTier,
            _comboManager.PerfectStreak);
        _arcaneAtmosphere.SetComboState(
            _comboManager.CurrentCombo,
            _comboManager.CurrentComboTier,
            _comboManager.PerfectStreak);
    }

    private void ShowComboFeedback(string message, Color color)
    {
        _comboFeedbackLabel.Text = message;
        _comboFeedbackColor = color;
        _comboFeedbackLabel.Visible = true;
        _comboFeedbackLabel.Modulate = color;
        _comboFeedbackLabel.Scale = Vector2.One * 1.08f;
        _comboFeedbackTime = 1.1d;
        _comboFeedbackTimer.Start();
    }

    private void OnComboFeedbackTimeout()
    {
        _comboFeedbackTime = 0.0d;
        _comboFeedbackLabel.Visible = false;
        _comboFeedbackLabel.Modulate = new Color(
            _comboFeedbackColor.R,
            _comboFeedbackColor.G,
            _comboFeedbackColor.B,
            0.0f);
        _comboFeedbackLabel.Scale = Vector2.One;
    }

    private void UpdateComboFeedback(double delta)
    {
        if (_comboFeedbackTime <= 0.0d || !_comboFeedbackLabel.Visible)
        {
            return;
        }

        _comboFeedbackTime = Math.Max(0.0d, _comboFeedbackTime - delta);
        var fade = _comboFeedbackTime < 0.3d
            ? Mathf.Clamp((float)(_comboFeedbackTime / 0.3d), 0.0f, 1.0f)
            : 1.0f;
        _comboFeedbackLabel.Modulate = new Color(
            _comboFeedbackColor.R,
            _comboFeedbackColor.G,
            _comboFeedbackColor.B,
            fade);
    }

    private void UpdateComboPunches(double delta)
    {
        _comboLabelPunchTime = Math.Max(0.0d, _comboLabelPunchTime - delta);
        var comboPunch = Mathf.Clamp((float)(_comboLabelPunchTime / 0.18d), 0.0f, 1.0f);
        _comboLabel.Scale = Vector2.One * (1.0f + comboPunch * 0.08f);

        _perfectStreakPunchTime = Math.Max(0.0d, _perfectStreakPunchTime - delta);
        var streakPunch = Mathf.Clamp(
            (float)(_perfectStreakPunchTime / 0.18d),
            0.0f,
            1.0f);
        _perfectStreakLabel.Scale = Vector2.One * (1.0f + streakPunch * 0.08f);
    }

    private void UpdateResultPunch(double delta)
    {
        _resultPunchTime = Math.Max(0.0d, _resultPunchTime - delta);
        var punch = Mathf.Clamp((float)(_resultPunchTime / 0.18d), 0.0f, 1.0f);
        _resultLabel.Scale = Vector2.One * (1.0f + punch * 0.1f);
    }

    private static Color GetComboTierColor(int comboTier)
    {
        return comboTier switch
        {
            1 => new Color(0.78f, 0.9f, 1.0f, 1.0f),
            2 => new Color(1.0f, 0.48f, 0.86f, 1.0f),
            3 => new Color(0.7f, 1.0f, 0.3f, 1.0f),
            _ => new Color(0.82f, 0.86f, 0.98f, 1.0f),
        };
    }

    private void OnEnemyPostureBroken()
    {
        if (_enemy == null || _enemy.IsDead || !_enemy.IsStaggered ||
            _combatState == CombatState.PlayerDead ||
            _combatState == CombatState.EnemyDead ||
            _combatState == CombatState.EnemyStaggered ||
            _combatState == CombatState.ExecutionPrompt)
        {
            return;
        }

        _audioManager.PlayPostureBreak();
        _arcaneAtmosphere.PlayPostureBreak();
        _combatState = CombatState.EnemyStaggered;
        _queuedPatternEvent = null;
        _activePatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _executionTargetBeat = -1;
        _executionDelayTimer.Stop();
        ShowImportantResult(
            "STAGGERED",
            "RESISTÊNCIA QUEBRADA — prepare a execução");
        _resultLabel.Modulate = new Color(1.0f, 0.78f, 0.3f, 1.0f);
        _postureDamageLabel.Text = "RESISTÊNCIA: 0";
        _statusLabel.Text = "STAGGERED — preparando EXECUTION...";
        UpdateCurrentActionLabels();
        UpdateCombatDetails();
        UpdatePatternDebug();
        _executionDelayTimer.Start();
    }

    private void OnExecutionDelayTimeout()
    {
        if (_combatState != CombatState.EnemyStaggered ||
            _enemy == null || _enemy.IsDead || !_enemy.IsStaggered ||
            _player.IsDead)
        {
            return;
        }

        var safeDelayBeats = Math.Max(1, ExecutionTargetDelayBeats);
        _executionTargetBeat = Math.Max(
            1,
            _rhythmManager.CurrentBeat + safeDelayBeats);
        _combatState = CombatState.ExecutionPrompt;
        _statusLabel.Text = "EXECUTION se aproxima — prepare o timing";
        UpdateCurrentActionLabels();
        TryScheduleExecutionPrompt();
    }

    private void TryScheduleExecutionPrompt()
    {
        if (_combatState != CombatState.ExecutionPrompt ||
            _executionTargetBeat < 0 || IsPromptActive())
        {
            return;
        }

        var promptStartTime = _rhythmManager.GetBeatTime(_executionTargetBeat) -
            (_rhythmManager.BeatDuration * GetMinimumTelegraphBeats());
        if (_rhythmManager.GetMusicPosition() < promptStartTime)
        {
            return;
        }

        var targetBeat = _executionTargetBeat;
        _executionTargetBeat = -1;
        CreatePrompt(RhythmPromptType.Execution, 0.0f, targetBeat);
    }

    private void OnEnemyDied()
    {
        if (_battleResultQueued)
        {
            return;
        }

        _battleResultQueued = true;
        _combatState = CombatState.EnemyDead;
        _pendingRhythmPhaseIndex = -1;
        _pendingBossPhaseIndex = -1;
        _bossPhaseTransitionActive = false;
        _transitioningBossPhaseIndex = -1;
        _bossPhaseTransitionTimer?.Stop();
        HideBossPhaseTransitionPanel();
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        _queuedPatternEvent = null;
        _activePatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        if (_activePrompt != null && GodotObject.IsInstanceValid(_activePrompt))
        {
            _activePrompt.Resolved -= OnAttackPromptResolved;
            _activePrompt.QueueFree();
            _activePrompt = null;
        }

        ShowImportantResult(
            "INIMIGO DERROTADO",
            "O inimigo não pode mais agir.");
        _resultLabel.Modulate = new Color(1.0f, 0.84f, 0.3f, 1.0f);
        _postureDamageLabel.Text = "RESISTÊNCIA: 0";
        _statusLabel.Text = $"{GetCurrentEnemyName()} foi derrotado. O combate foi encerrado.";
        UpdateCurrentActionLabels();
        CallDeferred(nameof(FinalizeBattleResult));
    }

    private void FinalizeBattleResult()
    {
        if (!_battleResultQueued)
        {
            return;
        }

        var rewardConfig = RewardConfig ?? new BattleRewardConfig();
        var reward = BattleRewardCalculator.Calculate(
            _selectedEnemyData?.BaseEssenceReward ?? 0,
            _battlePerformance,
            rewardConfig,
            1.0f + _runManager.Build.GetPrimaryValue(UpgradeIds.SoulHarvest),
            1.0f + _runManager.Build.GetPrimaryValue(UpgradeIds.Fortune));
        var result = new BattleResultData(
            GetCurrentEnemyName(),
            _battlePerformance.PerfectCount,
            _battlePerformance.GoodCount,
            _battlePerformance.OkCount,
            _battlePerformance.MissCount,
            _battlePerformance.HighestCombo,
            _battlePerformance.SuccessfulExecutions,
            reward);

        _runManager.SetCurrentEncounter(_selectedEnemyIndex);
        _runManager.SetPlayerCurrentHP(_player.CurrentHealth);
        _runManager.SetComboState(
            _comboManager.CurrentCombo,
            _comboManager.HighestCombo,
            _comboManager.PerfectStreak);
        _runManager.SetLastBattleResult(result);
        _runManager.AddEssence(reward.TotalReward);

        if (_selectedBossData != null && VictoryScene != null)
        {
            _sceneTransitionManager.TransitionToPacked(VictoryScene);
            return;
        }

        if (BattleResultScene == null)
        {
            GD.PrintErr("CombatController: BattleResultScene não foi configurada na cena.");
            return;
        }

        _sceneTransitionManager.TransitionToPacked(BattleResultScene);
    }

    private void OnEnemyHealthChanged(float currentHealth, float maxHealth)
    {
        _arcaneAtmosphere.SetEnemyHealth(currentHealth, maxHealth);
        UpdateCombatDetails();
        if (_selectedBossData != null)
        {
            if (_bossController != null && maxHealth > 0.0f && currentHealth > 0.0f &&
                _bossController.TryGetNextPhaseIndex(
                    currentHealth / maxHealth,
                    out var requestedBossPhaseIndex))
            {
                _pendingBossPhaseIndex = requestedBossPhaseIndex;
                TryApplyPendingRhythmPhase();
            }

            return;
        }

        if (_selectedEnemyData == null ||
            !_selectedEnemyData.HasRhythmPhases ||
            maxHealth <= 0.0f ||
            currentHealth <= 0.0f)
        {
            return;
        }

        var healthPercent = currentHealth / maxHealth;
        var profile = _selectedEnemyData.GetRhythmProfileForHealthPercent(
            healthPercent,
            out var requestedPhaseIndex);
        if (profile == null ||
            requestedPhaseIndex < 0 ||
            requestedPhaseIndex <= _activeRhythmPhaseIndex)
        {
            return;
        }

        _pendingRhythmPhaseIndex = requestedPhaseIndex;
        TryApplyPendingRhythmPhase();
    }

    private void TryApplyPendingRhythmPhase()
    {
        if (_bossPhaseTransitionActive ||
            _combatState == CombatState.ResolvingPrompt ||
            _enemy == null ||
            _enemy.IsDead ||
            IsPromptActive() ||
            _combatState == CombatState.ExecutionPrompt ||
            _combatState == CombatState.EnemyStaggered ||
            _combatState == CombatState.PlayerDead ||
            _combatState == CombatState.EnemyDead)
        {
            return;
        }

        if (_selectedBossData != null)
        {
            if (_pendingBossPhaseIndex >= 0)
            {
                BeginBossPhaseTransition(_pendingBossPhaseIndex);
            }

            return;
        }

        if (_pendingRhythmPhaseIndex < 0 ||
            _selectedEnemyData == null ||
            !_selectedEnemyData.HasRhythmPhases)
        {
            return;
        }

        var phaseIndex = _pendingRhythmPhaseIndex;
        var phase = _selectedEnemyData.GetRhythmPhase(phaseIndex);
        if (phase == null || phase.RhythmProfile == null)
        {
            _pendingRhythmPhaseIndex = -1;
            return;
        }

        _pendingRhythmPhaseIndex = -1;
        _activeRhythmPhaseIndex = phaseIndex;
        ClearQueuedPatternEvent();
        BuildEncounterPattern(phase.RhythmProfile, phaseIndex);

        var currentBeatPosition = Math.Max(
            0.0d,
            _rhythmManager.GetMusicPosition() / _rhythmManager.BeatDuration);
        _nextPatternSearchBeatPosition = Math.Max(
            _nextPatternSearchBeatPosition,
            currentBeatPosition + GetMinimumTelegraphBeats());
        _statusLabel.Text =
            $"{phase.PhaseName} — novo padrão preparado para os próximos beats.";
        UpdateCurrentActionLabels();
        UpdatePatternDebug();
    }

    private void BeginBossPhaseTransition(int phaseIndex)
    {
        var phase = _selectedBossData?.GetBossPhase(phaseIndex);
        if (phase == null || _bossController == null ||
            _bossPhaseTransitionActive)
        {
            return;
        }

        _pendingBossPhaseIndex = -1;
        _transitioningBossPhaseIndex = phaseIndex;
        _bossPhaseTransitionActive = true;
        _combatState = CombatState.Running;
        _audioManager.PlayBossPhaseTransition();
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        ClearQueuedPatternEvent();

        if (_bossPhaseTransitionPanel != null)
        {
            _bossPhaseTransitionPanel.Visible = true;
            _bossPhaseTransitionPanel.Modulate = Colors.White;
        }

        if (_bossPhaseTransitionTitle != null)
        {
            _bossPhaseTransitionTitle.Text = phase.PhaseName;
        }

        if (_bossPhaseTransitionSubtitle != null)
        {
            _bossPhaseTransitionSubtitle.Text =
                $"THE RESISTANCE  •  {phase.ThemeLabel}";
        }

        if (_bossPhaseTransitionRules != null)
        {
            _bossPhaseTransitionRules.Text = phase.SpecialRules;
        }

        _bossPhaseTransitionAuraMaterial?.SetShaderParameter(
            "phase_intensity",
            Mathf.Clamp(0.55f + phaseIndex * 0.08f, 0.55f, 1.1f));

        ShowImportantResult(
            phase.PhaseName,
            "A música continua — uma nova forma desperta.");
        _resultLabel.Modulate = new Color(0.82f, 0.54f, 1.0f, 1.0f);
        _statusLabel.Text = $"{phase.PhaseName} — prepare-se.";
        UpdateCurrentActionLabels();

        var transitionDuration = _selectedBossData?.PhaseTransitionDuration ?? 1.0f;
        if (_bossPhaseTransitionTimer != null)
        {
            _bossPhaseTransitionTimer.Start(Mathf.Max(0.25f, transitionDuration));
        }
        else
        {
            GD.PrintErr(
                "CombatController: BossPhaseTransitionTimer não foi configurado na cena.");
            CallDeferred(nameof(OnBossPhaseTransitionTimeout));
        }
    }

    private void OnBossPhaseTransitionTimeout()
    {
        if (!_bossPhaseTransitionActive)
        {
            return;
        }

        var phaseIndex = _transitioningBossPhaseIndex;
        var phase = _selectedBossData?.GetBossPhase(phaseIndex);
        var activated = phase != null &&
            _bossController != null &&
            _bossController.ActivatePhase(phaseIndex);
        if (!activated || phase == null)
        {
            _bossPhaseTransitionActive = false;
            _transitioningBossPhaseIndex = -1;
            HideBossPhaseTransitionPanel();
            ShowError("Não foi possível ativar a fase do boss.");
            return;
        }

        _activeBossPhaseIndex = phaseIndex;
        _activeRhythmPhaseIndex = phaseIndex;
        _pendingBossPhaseIndex = -1;
        ClearQueuedPatternEvent();
        BuildEncounterPattern(phase.RhythmProfile, phaseIndex);

        var currentBeatPosition = Math.Max(
            0.0d,
            _rhythmManager.GetMusicPosition() / _rhythmManager.BeatDuration);
        _nextPatternSearchBeatPosition = Math.Max(
            _nextPatternSearchBeatPosition,
            currentBeatPosition + GetMinimumTelegraphBeats());
        _bossPhaseTransitionActive = false;
        _transitioningBossPhaseIndex = -1;
        HideBossPhaseTransitionPanel();
        _statusLabel.Text =
            $"{phase.PhaseName} — novo padrão preparado. Resistência restaurada.";
        UpdateCurrentActionLabels();
        UpdatePatternDebug();
        TryScheduleNextPrompt();
    }

    private void HideBossPhaseTransitionPanel()
    {
        if (_bossPhaseTransitionPanel == null)
        {
            return;
        }

        _bossPhaseTransitionPanel.Visible = false;
        _bossPhaseTransitionPanel.Modulate = Colors.White;
    }

    private void OnBossRegenerationStarted()
    {
        if (_bossPhaseTransitionActive || _combatState == CombatState.PlayerDead)
        {
            return;
        }

        _audioManager.PlayBossRegeneration();
        ShowImportantResult(
            "RESISTANCE REGENERATING",
            "Acerte PERFECT para interromper a regeneração.");
        _resultLabel.Modulate = new Color(0.68f, 1.0f, 0.46f, 1.0f);
        _statusLabel.Text =
            "RESISTANCE REGENERATING — o próximo PERFECT interrompe a cura.";
    }

    private void OnBossRegenerationStopped()
    {
        if (_combatState == CombatState.PlayerDead || _bossController == null)
        {
            return;
        }

        _statusLabel.Text = _bossController.RegeneratedHealth > 0.0f
            ? "A regeneração da RESISTANCE foi interrompida."
            : "PERFECT — regeneração interrompida.";
    }

    private void OnBossRegenerationTick(float amount)
    {
        if (_bossPhaseTransitionActive || _combatState == CombatState.PlayerDead)
        {
            return;
        }

        _damageLabel.Text = $"RESISTANCE REGENERATING: +{amount:0.0} HP";
        _damageLabel.Modulate = new Color(0.68f, 1.0f, 0.46f, 1.0f);
    }

    private void DetachBossControllerSignals()
    {
        if (_bossController == null ||
            !GodotObject.IsInstanceValid(_bossController))
        {
            return;
        }

        _bossController.RegenerationStarted -= OnBossRegenerationStarted;
        _bossController.RegenerationStopped -= OnBossRegenerationStopped;
        _bossController.RegenerationTick -= OnBossRegenerationTick;
    }

    private void ClearQueuedPatternEvent()
    {
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
    }

    private void OnEnemyPostureChanged(float currentPosture, float maxPosture)
    {
        _arcaneAtmosphere.SetEnemyResistance(currentPosture, maxPosture);
        UpdateCombatDetails();
    }

    private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
    {
        _playerHealthBar.MaxValue = maxHealth;
        _playerHealthBar.Value = currentHealth;
        _playerHealthLabel.Text = $"HP: {currentHealth:0}/{maxHealth:0}";
        _playerHpDebugLabel.Text = $"Player HP: {currentHealth:0}/{maxHealth:0}";
        _runManager.SetPlayerCurrentHP(currentHealth);
    }

    private void OnPlayerDied()
    {
        _runManager.SetPlayerCurrentHP(0.0f);
        _combatState = CombatState.PlayerDead;
        _pendingRhythmPhaseIndex = -1;
        _pendingBossPhaseIndex = -1;
        _bossPhaseTransitionActive = false;
        _transitioningBossPhaseIndex = -1;
        _bossPhaseTransitionTimer?.Stop();
        HideBossPhaseTransitionPanel();
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        _queuedPatternEvent = null;
        _activePatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        ShowImportantResult(
            "GAME OVER",
            "A run foi perdida. Essence, upgrades e progresso foram zerados.");
        _resultLabel.Modulate = GetResultColor(TimingResult.Miss);
        _statusLabel.Text =
            "GAME OVER — pressione R para reiniciar no THE FOOL";
        UpdateCurrentActionLabels();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo)
        {
            return;
        }

        if (_combatState != CombatState.PlayerDead ||
            (keyEvent.Keycode != Key.R && keyEvent.PhysicalKeycode != Key.R))
        {
            return;
        }

        RestartCombat();
        GetViewport().SetInputAsHandled();
    }

    private void RestartCombat()
    {
        _runManager.BeginRun(
            _basePlayerMaxHealth > 0.0f ? _basePlayerMaxHealth : _player.MaxHealth);
        _player.SetMaxHealth(_runManager.GetEffectivePlayerMaxHealth());
        _battlePerformance.Reset();
        _battleResultQueued = false;
        _secondBreathUsedThisCombat = false;
        _unbrokenUsedThisCombat = false;
        _riposteReady = false;
        _combatState = CombatState.Running;
        _activePrompt = null;
        _activePatternEvent = null;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _executionDelayTimer.Stop();
        _bossPhaseTransitionTimer?.Stop();
        _bossPhaseTransitionActive = false;
        _transitioningBossPhaseIndex = -1;
        HideBossPhaseTransitionPanel();
        _executionTargetBeat = -1;
        _lastIncomingDamage = 0.0f;
        _lastHealthDamage = 0.0f;
        _lastRawHealthDamage = 0.0f;
        _lastIncomingDamageLabel.Text = "Last incoming damage: 0.0";
        _lastPostureDamage = 0.0f;
        _postureDamageLabel.Text = "RESISTÊNCIA: --";
        _player.ResetHealth();
        _comboManager.ResetForCombat();
        OnComboFeedbackTimeout();

        if (_enemy != null && GodotObject.IsInstanceValid(_enemy))
        {
            DetachBossControllerSignals();
            _enemy.HealthChanged -= OnEnemyHealthChanged;
            _enemy.PostureChanged -= OnEnemyPostureChanged;
            _enemy.PostureBroken -= OnEnemyPostureBroken;
            _enemy.Died -= OnEnemyDied;
            _enemy.QueueFree();
        }

        _bossController = null;

        // Uma morte sempre reinicia a run no primeiro encontro, inclusive se
        // a morte ocorreu em um inimigo selecionado pelo modo de debug.
        SelectEnemyData(0);
        BuildEncounterPattern();
        CreateEnemy();

        _nextPatternSearchBeatPosition = Math.Max(
            1.0d,
            _rhythmManager.CurrentBeat + 1.0d);
        ShowImportantResult(
            "AGUARDANDO AÇÃO",
            "Aperte SPACE quando o círculo amarelo entrar no azul");
        _damageLabel.Text = "DANO: --";
        _statusLabel.Text = "Combate reiniciado.";
        UpdateCurrentActionLabels();
        BeginEncounterStartDelay();
    }

    private void BeginEncounterStartDelay()
    {
        _encounterStartDelayTimer.Stop();
        _encounterStartDelayActive = true;
        _waitingForTransitionReveal = _sceneTransitionManager.IsTransitioning;

        _sceneTransitionManager.TransitionFinished -= OnSceneTransitionFinished;
        if (_waitingForTransitionReveal)
        {
            _sceneTransitionManager.TransitionFinished += OnSceneTransitionFinished;
            return;
        }

        _encounterStartDelayTimer.Start();
    }

    private void OnSceneTransitionFinished()
    {
        _sceneTransitionManager.TransitionFinished -= OnSceneTransitionFinished;
        if (!_waitingForTransitionReveal)
        {
            return;
        }

        _waitingForTransitionReveal = false;
        _encounterStartDelayTimer.Start();
    }

    private void OnEncounterStartDelayTimeout()
    {
        _encounterStartDelayActive = false;
        var currentBeatPosition = Math.Max(
            0.0d,
            _rhythmManager.GetMusicPosition() / _rhythmManager.BeatDuration);
        _nextPatternSearchBeatPosition = Math.Max(
            _nextPatternSearchBeatPosition,
            currentBeatPosition + GetMinimumTelegraphBeats());
        TryScheduleNextPrompt();
    }

    private void UpdateDebugLabels()
    {
        if (!DebugRhythm)
        {
            return;
        }

        UpdateCurrentActionLabels();
        UpdateCombatDetails();
        UpdatePatternDebug();

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
        else if (_hasQueuedPatternEvent)
        {
            targetTime = _rhythmManager.GetBeatTime(_queuedPatternBeatPosition);
        }
        else if (_combatState == CombatState.ExecutionPrompt && _executionTargetBeat >= 0)
        {
            targetTime = _rhythmManager.GetBeatTime(_executionTargetBeat);
        }

        _bpmLabel.Text =
            $"BPM: {_rhythmManager.Bpm:0.0} | Music: " +
            (_rhythmManager.MusicPlaying ? "ON" : "OFF");
        _beatLabel.Text =
            $"Beat atual: {_rhythmManager.CurrentBeat + 1} | " +
            $"Phase: {_rhythmManager.BeatPhase:0.00}";
        _musicPositionLabel.Text =
            $"Music position: {musicPosition:0.000}s | " +
            $"Offset: {_rhythmManager.MusicOffsetMs:0}ms";
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

    private void UpdateCurrentActionLabels()
    {
        var actionName = _bossPhaseTransitionActive
            ? "BOSS PHASE TRANSITION"
            : IsPromptActive()
            ? GetActionDisplayName(_activePromptType)
            : _combatState == CombatState.EnemyStaggered
                ? "STAGGERED"
                : _combatState == CombatState.ExecutionPrompt
                    ? "EXECUTION"
                    : _hasQueuedPatternEvent && _queuedPatternEvent != null
                ? $"PAUSE -> {GetActionDisplayName(_queuedPatternEvent.GetPromptType())}"
                : _combatState == CombatState.Running
                    ? "WAITING"
                    : _combatState.ToString().ToUpperInvariant();

        _currentActionLabel.Text = $"Current action: {actionName}";
        _enemyBaseDamageLabel.Text = IsPromptActive() && _activePromptType == RhythmPromptType.EnemyAttack
            ? $"Enemy base damage: {_activePromptDamage:0.0}"
            : "Enemy base damage: --";
    }

    private void UpdatePatternDebug()
    {
        var profile = _activeRhythmProfile;
        _enemySelectionLabel.Text =
            $"Current: {GetCurrentEnemyName()} | Seed: {EncounterSeed}";

        if (profile == null)
        {
            _profileDebugLabel.Text = "Rhythm Profile: --";
        }
        else
        {
            var stats = _encounterStats;
            _profileDebugLabel.Text =
                $"Profile: {profile.ProfileName} | Difficulty: {profile.DifficultyLevel}\n" +
                $"Density: {profile.EventDensity:0.00} | Enemy Weight: {profile.EnemyAttackWeight:0.0}\n" +
                $"Half: {profile.AllowHalfBeats} ({profile.HalfBeatChance:P0}) | " +
                $"Burst: {profile.BurstChance:P0} @ {profile.BurstSpacingBeats:0.0}b\n" +
                $"Feint: {profile.FeintChance:P0} | Fade: {profile.VisualFadeChance:P0} | " +
                $"Decoy: {profile.DecoyChance:P0}\n" +
                $"Telegraph: {GetMinimumTelegraphBeats():0.00}b | Phase: {GetCurrentRhythmPhaseName()}\n" +
                (stats == null
                    ? "Generated: --"
                    : $"Generated: {stats.EventCount} | P:{stats.PlayerAttackCount} " +
                      $"E:{stats.EnemyAttackCount} H:{stats.HalfBeatCount} " +
                      $"B:{stats.BurstCount} F:{stats.FeintCount}/{stats.FadeCount} " +
                      $"D:{stats.DecoyCount}\n" +
                      $"Spacing min/avg: {stats.MinimumTargetSpacingBeats:0.00}/" +
                      $"{stats.AverageTargetSpacingBeats:0.00}b | " +
                      $"Active max: {stats.MaximumActiveLogicalPrompts}");
        }

        if (_selectedBossData != null && _bossController != null)
        {
            var activePhase = _bossController.ActivePhase;
            var bossDebug =
                $"\nBoss phase: {_bossController.ActivePhaseIndex + 1}/" +
                $"{_selectedBossData.PhaseCount} | HP gate: " +
                $"{activePhase?.GetMinimumHealthPercent():P0}–" +
                $"{activePhase?.GetMaximumHealthPercent():P0}\n" +
                $"Armor: {_enemy.IsArmorActive} x{_enemy.ArmorHealthDamageMultiplier:0.00} | " +
                $"Regen: {_bossController.IsRegenerating}\n" +
                $"No PERFECT: {_bossController.TimeSinceLastPerfect:0.0}s | " +
                $"Regen HP: {_bossController.RegeneratedHealth:0.0}";
            _profileDebugLabel.Text += bossDebug;
        }

        var preview = new StringBuilder("NEXT EVENTS:\n");
        if (_encounterPattern == null)
        {
            preview.Append("--");
            _patternPreviewLabel.Text = preview.ToString();
            return;
        }

        var searchBeat = _hasQueuedPatternEvent
            ? _queuedPatternBeatPosition
            : Math.Max(
                _nextPatternSearchBeatPosition,
                (_rhythmManager.GetMusicPosition() / _rhythmManager.BeatDuration) + 0.001d);
        for (var index = 0; index < 5; index++)
        {
            if (!_encounterPattern.TryGetNextEvent(
                    searchBeat,
                    out var absoluteBeat,
                    out var rhythmEvent) ||
                rhythmEvent == null)
            {
                break;
            }

            var wholeBeat = Math.Floor(absoluteBeat);
            var subdivision = absoluteBeat - wholeBeat;
            preview.Append(
                $"{absoluteBeat,6:0.0}  " +
                $"{GetActionDisplayName(rhythmEvent.GetPromptType()),-13} " +
                $"{rhythmEvent.GetPromptBehavior().ToString().ToUpperInvariant(),-18}");
            if (rhythmEvent.ChainId > 0)
            {
                preview.Append($" C{rhythmEvent.ChainId}:{rhythmEvent.ChainIndex + 1}/{rhythmEvent.ChainLength}");
            }

            preview.Append($"  sub:{subdivision:0.0}\n");
            searchBeat = absoluteBeat + 0.001d;
        }

        var activeBehavior = _activePrompt?.Behavior ?? RhythmPromptBehavior.Normal;
        preview.Append($"Prompt Behavior: {activeBehavior.ToString().ToUpperInvariant()}");
        _patternPreviewLabel.Text = preview.ToString();
    }

    private void UpdateCombatDetails()
    {
        if (_combatDetailsLabel == null || _enemy == null)
        {
            return;
        }

        var executionActive = _combatState == CombatState.ExecutionPrompt ||
            (IsPromptActive() && _activePromptType == RhythmPromptType.Execution);
        _combatDetailsLabel.Text =
            $"Enemy HP: {_enemy.CurrentHealth:0}/{_enemy.MaxHealth:0}\n" +
            $"Enemy Resistance: {_enemy.CurrentPosture:0}/{_enemy.MaxPosture:0}\n" +
            $"Enemy State: {_enemy.State.ToString().ToUpperInvariant()}\n" +
            $"Armor Active: {_enemy.IsArmorActive.ToString().ToLowerInvariant()}\n" +
            $"Armor HP Multiplier: {_enemy.ArmorHealthDamageMultiplier:0.00}\n" +
            $"Enemy Rhythm: {_activeRhythmProfile?.ProfileName ?? "--"}\n" +
            $"Rhythm Phase: {GetCurrentRhythmPhaseName()}" +
            (_pendingRhythmPhaseIndex >= 0 ? " (PENDING)" : string.Empty) + "\n" +
            $"Combat State: {_combatState.ToString().ToUpperInvariant()}\n" +
            $"Combo: {_comboManager.CurrentCombo} | Highest: {_comboManager.HighestCombo}\n" +
            $"Perfect Streak: {_comboManager.PerfectStreak}\n" +
            $"Combo Tier: {_comboManager.CurrentComboTier}\n" +
            $"Damage Multiplier: x{_comboManager.CurrentDamageMultiplier:0.00}\n" +
            $"Last Combo Result: {GetLastComboResultDisplayName()}\n" +
            $"Last Raw HP Damage: {_lastRawHealthDamage:0.0}\n" +
            $"Last HP Damage: {_lastHealthDamage:0.0}\n" +
            $"Last Resistance Damage: {_lastPostureDamage:0.0}\n" +
            $"Run Build: {_runManager.GetBuildDebugSummary()}\n" +
            $"Last SFX: {_audioManager.LastSfxName}\n" +
            $"Execution Active: {executionActive.ToString().ToLowerInvariant()}" +
            (_selectedBossData == null || _bossController == null
                ? string.Empty
                : $"\nBoss Phase: {_bossController.ActivePhaseIndex + 1}/" +
                  $"{_selectedBossData.PhaseCount}\n" +
                  $"Boss Regen: {_bossController.IsRegenerating.ToString().ToLowerInvariant()}\n" +
                  $"No Perfect: {_bossController.TimeSinceLastPerfect:0.0}s\n" +
                  $"Regenerated HP: {_bossController.RegeneratedHealth:0.0}");
    }

    private string GetCurrentEnemyName()
    {
        if (!string.IsNullOrWhiteSpace(_selectedEnemyData?.EnemyName))
        {
            return _selectedEnemyData.EnemyName;
        }

        return _enemy != null && !string.IsNullOrWhiteSpace(_enemy.EnemyName)
            ? _enemy.EnemyName
            : "THE FOOL";
    }

    private string GetCurrentRhythmPhaseName()
    {
        if (_selectedBossData != null && _activeBossPhaseIndex >= 0)
        {
            return _selectedBossData.GetBossPhase(_activeBossPhaseIndex)?.PhaseName ??
                $"PHASE {_activeBossPhaseIndex + 1}";
        }

        var phase = _selectedEnemyData?.GetRhythmPhase(_activeRhythmPhaseIndex);
        return phase?.PhaseName ?? (_activeRhythmPhaseIndex >= 0
            ? $"PHASE {_activeRhythmPhaseIndex + 1}"
            : "--");
    }

    private string GetLastComboResultDisplayName()
    {
        return _comboManager.LastComboResult.HasValue
            ? TimingJudge.GetDisplayName(_comboManager.LastComboResult.Value)
            : "--";
    }

    private void UpdateWaitingStatus()
    {
        if (_combatState != CombatState.Running || IsPromptActive())
        {
            return;
        }

        if (_hasQueuedPatternEvent)
        {
            var secondsUntilPrompt = Math.Max(
                0.0d,
                GetQueuedPromptStartTime() - _rhythmManager.GetMusicPosition());
            _statusLabel.Text = secondsUntilPrompt > 0.05d
                ? $"PAUSA — próxima ação em {secondsUntilPrompt:0.0}s"
                : "PRÓXIMA AÇÃO — preparando prompt";
        }
        else
        {
            _statusLabel.Text = "Aguardando próximo evento...";
        }
    }

    private static string GetActionDisplayName(RhythmPromptType promptType)
    {
        return promptType switch
        {
            RhythmPromptType.PlayerAttack => "PLAYER_ATTACK",
            RhythmPromptType.EnemyAttack => "ENEMY_ATTACK",
            RhythmPromptType.Execution => "EXECUTION",
            _ => "UNKNOWN",
        };
    }

    private void ShowError(string message)
    {
        _statusLabel.Text = $"ERRO: {message}";
        GD.PrintErr(message);
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

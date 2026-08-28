using System;
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
    public TimingConfig TimingConfig { get; set; } = null!;

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

    [Export]
    public bool DebugComboTrainingDummy { get; set; }

    [Export(PropertyHint.Range, "100.0,9999.0,100.0")]
    public float DebugComboDummyHealth { get; set; } = 1000.0f;

    private RhythmManager _rhythmManager = null!;
    private ResourcePreloader _sceneResources = null!;
    private Node2D _promptContainer = null!;
    private Node2D _enemyContainer = null!;
    private EnemyBase _enemy = null!;
    private PlayerBase _player = null!;
    private RhythmPrompt? _activePrompt;
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
    private Control _comboPanel = null!;
    private Control _debugPanel = null!;
    private Timer _executionDelayTimer = null!;
    private Timer _comboFeedbackTimer = null!;
    private ComboManager _comboManager = null!;
    private AudioManager _audioManager = null!;
    private float _lastHealthDamage;
    private double _comboFeedbackTime;
    private double _comboLabelPunchTime;
    private double _perfectStreakPunchTime;
    private Color _comboFeedbackColor = Colors.White;
    private double _beatFeedbackTime;

    public override void _Ready()
    {
        _rhythmManager = GetNode<RhythmManager>("/root/RhythmManager");
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
        _comboPanel = GetNode<Control>("CanvasLayer/Ui/ComboPanel");
        _comboLabel = GetNode<Label>("CanvasLayer/Ui/ComboPanel/ComboLabel");
        _comboMultiplierLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/ComboMultiplierLabel");
        _perfectStreakLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/PerfectStreakLabel");
        _comboFeedbackLabel = GetNode<Label>(
            "CanvasLayer/Ui/ComboPanel/ComboFeedbackLabel");
        _debugPanel = GetNode<Control>("CanvasLayer/Ui/DebugPanel");
        _debugPanel.Visible = DebugRhythm;

        _comboManager.ComboChanged += OnComboChanged;
        _comboManager.PerfectStreakChanged += OnPerfectStreakChanged;
        _comboManager.DamageMultiplierChanged += OnDamageMultiplierChanged;
        _comboManager.ComboMilestoneReached += OnComboMilestoneReached;
        _comboManager.ComboBroken += OnComboBroken;
        _comboManager.TimingResultRegistered += OnTimingResultRegistered;
        _comboManager.ResetForCombat();
        UpdateComboUi();

        ResolveSceneReferences();
        CreateEnemy();

        _player.HealthChanged += OnPlayerHealthChanged;
        _player.Died += OnPlayerDied;
        OnPlayerHealthChanged(_player.CurrentHealth, _player.MaxHealth);

        _resultLabel.Text = "AGUARDANDO ATAQUE";
        _resultDetailLabel.Text = "Aperte SPACE quando o círculo amarelo entrar no azul";
        _damageLabel.Text = "DANO: --";
        _postureDamageLabel.Text = "POSTURE: --";
        _lastIncomingDamageLabel.Text = "Last incoming damage: 0.0";
        _lastHealthDamage = 0.0f;
        _statusLabel.Text = "THE INITIATE aguarda seu ritmo.";

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
        if (_combatState == CombatState.Running)
        {
            TryScheduleNextPrompt();
            UpdateWaitingStatus();
        }
        else if (_combatState == CombatState.ExecutionPrompt)
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

        if (_rhythmManager != null)
        {
            _rhythmManager.BeatStarted -= OnBeatStarted;
        }

        if (_enemy != null && GodotObject.IsInstanceValid(_enemy))
        {
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
            var preloadedPattern = _sceneResources.GetResource("initiate_pattern") as RhythmPatternResource;
            if (preloadedPattern != null)
            {
                AttackPattern = preloadedPattern;
            }
        }
    }

    private void CreateEnemy()
    {
        if (EnemyScene == null)
        {
            ShowError("PackedScene do inimigo não foi encontrado na cena.");
            return;
        }

        try
        {
            _enemy = EnemyScene.Instantiate<EnemyBase>();
            if (DebugComboTrainingDummy)
            {
                _enemy.MaxHealth = Math.Max(_enemy.MaxHealth, DebugComboDummyHealth);
            }

            _enemyContainer.AddChild(_enemy);
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
        if (_combatState != CombatState.Running || AttackPattern == null || IsPromptActive())
        {
            return;
        }

        if (!_hasQueuedPatternEvent)
        {
            var firstPossibleBeatPosition = Math.Max(
                _nextPatternSearchBeatPosition,
                _rhythmManager.CurrentBeat + 1.0d);
            if (!AttackPattern.TryGetNextEvent(
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
        var safeLeadBeats = Math.Max(0.25d, PromptLeadBeats);
        return _rhythmManager.GetBeatTime(_queuedPatternBeatPosition) -
            (_rhythmManager.BeatDuration * safeLeadBeats);
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
            : BaseAttackDamage;
        CreatePrompt(promptType, promptDamage, targetBeatPosition);
    }

    private void CreatePrompt(
        RhythmPromptType promptType,
        float promptDamage,
        double targetBeatPosition)
    {
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
            }
            else if (_activePromptType == RhythmPromptType.Execution)
            {
                prompt.SetPromptPresentation(
                    "[!] EXECUTION [!]",
                    new Color(0.72f, 0.42f, 1.0f, 1.0f),
                    9.0f,
                    1.35f);
                _audioManager.PlayExecutionReady();
            }
            else
            {
                prompt.SetPromptPresentation(
                    "PLAYER ATTACK",
                    new Color(1.0f, 0.72f, 0.3f, 1.0f));
            }

            prompt.Configure(
                _rhythmManager.GetBeatTime(targetBeatPosition),
                _rhythmManager.BeatDuration);
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
        var damage = DamageCalculator.CalculatePlayerAttackDamage(
            baseDamage,
            timingResult,
            TimingConfig,
            comboMultiplier);
        _lastHealthDamage = damage;

        SetResultFeedback(
            resultName,
            timingResult,
            precisionPercent,
            offsetMilliseconds,
            direction);
        _lastPostureDamage = 0.0f;
        _postureDamageLabel.Text = "POSTURE: 0";

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

            _statusLabel.Text = $"{resultName} — {_enemy.EnemyName} recebeu {damage:0.0} de dano.";
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

        var postureDamage = DamageCalculator.CalculatePostureDamage(
            RhythmPromptType.PlayerAttack,
            timingResult,
            TimingConfig);
        _lastPostureDamage = postureDamage;
        _postureDamageLabel.Text = $"POSTURE: -{postureDamage:0.0}";

        if (postureDamage > 0.0f)
        {
            _enemy.TakePostureDamage(postureDamage);
        }

        if (_combatState == CombatState.EnemyStaggered)
        {
            RegisterComboResult(timingResult);
            return;
        }

        _statusLabel.Text = timingResult switch
        {
            TimingResult.Perfect =>
                $"{resultName} — dano {damage:0.0}; postura -{postureDamage:0.0}.",
            TimingResult.Good or TimingResult.Ok =>
                $"{resultName} — apenas postura -{postureDamage:0.0}; sem dano direto.",
            _ => "MISS — nenhum dano causado.",
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
        var incomingDamage = DamageCalculator.CalculateIncomingDamage(
            baseDamage,
            timingResult,
            TimingConfig);
        var defenseResult = GetDefenseResultName(timingResult);
        _lastIncomingDamage = incomingDamage;
        _lastHealthDamage = incomingDamage;
        _lastIncomingDamageLabel.Text = $"Last incoming damage: {incomingDamage:0.0}";

        var postureDamage = DamageCalculator.CalculatePostureDamage(
            RhythmPromptType.EnemyAttack,
            timingResult,
            TimingConfig);
        _lastPostureDamage = postureDamage;
        _postureDamageLabel.Text = $"POSTURE: -{postureDamage:0.0}";

        _damageLabel.Text = $"DANO RECEBIDO: {incomingDamage:0.0}";
        _damageLabel.Modulate = GetResultColor(timingResult);

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
            _statusLabel.Text =
                $"{defenseResult} — dano recebido: {incomingDamage:0.0}; " +
                $"postura -{postureDamage:0.0}.";
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
        _resultLabel.Text = displayResult;
        _resultLabel.Modulate = GetResultColor(timingResult);
        _resultDetailLabel.Text =
            $"{TimingJudge.FormatOffsetMilliseconds(offsetMilliseconds)}  •  " +
            $"{precisionPercent:0.0}%  •  {direction}";
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
            TimingConfig);
        var executionResult = GetExecutionResultName(timingResult);
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
        _postureDamageLabel.Text = "POSTURE: --";
        _damageLabel.Text = executionDamage > 0.0f
            ? $"EXECUTION DAMAGE: {executionDamage:0.0}"
            : "EXECUTION DAMAGE: 0";
        _damageLabel.Modulate = timingResult == TimingResult.Perfect
            ? GetResultColor(timingResult)
            : GetResultColor(TimingResult.Miss);

        if (executionDamage > 0.0f)
        {
            _enemy.TakeDamage(executionDamage);

            if (_combatState == CombatState.EnemyDead)
            {
                RegisterComboResult(timingResult);
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
            ? "PERFECT EXECUTION — postura restaurada; combate retomado."
            : "EXECUTION FAILED — oportunidade perdida; postura restaurada; combate retomado.";
        RegisterComboResult(timingResult);
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
        _comboManager.RegisterTimingResult(timingResult);
    }

    private void OnComboChanged(int currentCombo, int highestCombo)
    {
        _comboLabelPunchTime = 0.18d;
        UpdateComboUi();
    }

    private void OnPerfectStreakChanged(int perfectStreak)
    {
        if (perfectStreak >= 2)
        {
            _perfectStreakPunchTime = 0.18d;
            _audioManager.PlayPerfectStreak(perfectStreak);
        }

        UpdateComboUi();
    }

    private void OnDamageMultiplierChanged(float damageMultiplier, int comboTier)
    {
        UpdateComboUi();
    }

    private void OnComboMilestoneReached(int combo, string milestoneText)
    {
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

        _comboPanel.Modulate = GetComboTierColor(_comboManager.CurrentComboTier);
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

    private static Color GetComboTierColor(int comboTier)
    {
        return comboTier switch
        {
            1 => new Color(0.78f, 0.9f, 1.0f, 1.0f),
            2 => new Color(1.0f, 0.82f, 0.42f, 1.0f),
            3 => new Color(1.0f, 0.62f, 0.28f, 1.0f),
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
        _combatState = CombatState.EnemyStaggered;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _executionTargetBeat = -1;
        _executionDelayTimer.Stop();
        _resultLabel.Text = "STAGGERED";
        _resultLabel.Modulate = new Color(1.0f, 0.78f, 0.3f, 1.0f);
        _resultDetailLabel.Text = "POSTURE BROKEN — prepare a execução";
        _postureDamageLabel.Text = "POSTURE: 0";
        _statusLabel.Text = "STAGGERED — preparando EXECUTION...";
        UpdateCurrentActionLabels();
        UpdateCombatDetails();
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
            (_rhythmManager.BeatDuration * Math.Max(0.25d, PromptLeadBeats));
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
        _combatState = CombatState.EnemyDead;
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _resultLabel.Text = "INIMIGO DERROTADO";
        _resultLabel.Modulate = new Color(1.0f, 0.84f, 0.3f, 1.0f);
        _resultDetailLabel.Text = "O inimigo não pode mais agir.";
        _postureDamageLabel.Text = "POSTURE: 0";
        _statusLabel.Text = "THE INITIATE foi derrotado. O combate foi encerrado.";
        UpdateCurrentActionLabels();
    }

    private void OnEnemyHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateCombatDetails();
    }

    private void OnEnemyPostureChanged(float currentPosture, float maxPosture)
    {
        UpdateCombatDetails();
    }

    private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
    {
        _playerHealthBar.MaxValue = maxHealth;
        _playerHealthBar.Value = currentHealth;
        _playerHealthLabel.Text = $"HP: {currentHealth:0}/{maxHealth:0}";
        _playerHpDebugLabel.Text = $"Player HP: {currentHealth:0}/{maxHealth:0}";
    }

    private void OnPlayerDied()
    {
        _combatState = CombatState.PlayerDead;
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _resultLabel.Text = "PLAYER DEFEATED";
        _resultLabel.Modulate = GetResultColor(TimingResult.Miss);
        _resultDetailLabel.Text = "O combate foi interrompido.";
        _statusLabel.Text = "PLAYER DEFEATED — pressione R para reiniciar";
        UpdateCurrentActionLabels();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_combatState != CombatState.PlayerDead ||
            @event is not InputEventKey keyEvent ||
            !keyEvent.Pressed ||
            keyEvent.Echo ||
            (keyEvent.Keycode != Key.R && keyEvent.PhysicalKeycode != Key.R))
        {
            return;
        }

        RestartCombat();
        GetViewport().SetInputAsHandled();
    }

    private void RestartCombat()
    {
        _combatState = CombatState.Running;
        _activePrompt = null;
        _queuedPatternEvent = null;
        _queuedPatternBeatPosition = -1.0d;
        _hasQueuedPatternEvent = false;
        _executionDelayTimer.Stop();
        _executionTargetBeat = -1;
        _lastIncomingDamage = 0.0f;
        _lastHealthDamage = 0.0f;
        _lastIncomingDamageLabel.Text = "Last incoming damage: 0.0";
        _lastPostureDamage = 0.0f;
        _postureDamageLabel.Text = "POSTURE: --";
        _player.ResetHealth();
        _comboManager.ResetForCombat();
        OnComboFeedbackTimeout();

        if (_enemy != null && !_enemy.IsDefeated)
        {
            _enemy.RestoreFullHealth();
        }

        _nextPatternSearchBeatPosition = Math.Max(
            1.0d,
            _rhythmManager.CurrentBeat + 1.0d);
        _resultLabel.Text = "AGUARDANDO AÇÃO";
        _resultDetailLabel.Text = "Aperte SPACE quando o círculo amarelo entrar no azul";
        _damageLabel.Text = "DANO: --";
        _statusLabel.Text = "Combate reiniciado.";
        UpdateCurrentActionLabels();
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
        var actionName = IsPromptActive()
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
            $"Enemy Posture: {_enemy.CurrentPosture:0}/{_enemy.MaxPosture:0}\n" +
            $"Enemy State: {_enemy.State.ToString().ToUpperInvariant()}\n" +
            $"Combat State: {_combatState.ToString().ToUpperInvariant()}\n" +
            $"Combo: {_comboManager.CurrentCombo} | Highest: {_comboManager.HighestCombo}\n" +
            $"Perfect Streak: {_comboManager.PerfectStreak}\n" +
            $"Combo Tier: {_comboManager.CurrentComboTier}\n" +
            $"Damage Multiplier: x{_comboManager.CurrentDamageMultiplier:0.00}\n" +
            $"Last Combo Result: {GetLastComboResultDisplayName()}\n" +
            $"Last HP Damage: {_lastHealthDamage:0.0}\n" +
            $"Last Posture Damage: {_lastPostureDamage:0.0}\n" +
            $"Last SFX: {_audioManager.LastSfxName}\n" +
            $"Execution Active: {executionActive.ToString().ToLowerInvariant()}";
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

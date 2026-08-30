using Godot;

/// <summary>
/// Estado mínimo que precisa atravessar as cenas de uma run.
/// </summary>
public partial class RunManager : Node
{
    private const int ShopSlotCount = 3;

    [Signal]
    public delegate void EssenceChangedEventHandler(int currentEssence);

    [Signal]
    public delegate void UpgradeAddedEventHandler(string upgradeId, int stacks);

    [Signal]
    public delegate void ShopStateChangedEventHandler(
        int rerollsRemaining,
        bool purchaseMade);

    public int CurrentEssence { get; private set; }
    public int CurrentEncounterIndex { get; private set; }
    public float PlayerCurrentHP { get; private set; } = 100.0f;
    public float BasePlayerMaxHealth { get; private set; } = 100.0f;
    public int CurrentCombo { get; private set; }
    public int HighestCombo { get; private set; }
    public int PerfectStreak { get; private set; }
    public bool IsRunInitialized { get; private set; }
    public BattleResultData? LastBattleResult { get; private set; }
    public RunBuild Build { get; } = new();
    public int ShopRerollsRemaining
    {
        get
        {
            var available = 0;
            for (var index = 0; index < _shopSlotRerollsAvailable.Length; index++)
            {
                if (_shopSlotRerollsAvailable[index])
                {
                    available++;
                }
            }

            return available;
        }
    }
    public bool ShopPurchaseMade { get; private set; }
    public bool IsShopOpen { get; private set; }
    public int ShopVisitIndex { get; private set; }
    public int CurrentShopSeed { get; private set; }

    private readonly bool[] _shopSlotRerollsAvailable =
        new bool[ShopSlotCount];

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public void EnsureRunStarted(float initialPlayerHealth)
    {
        if (IsRunInitialized)
        {
            return;
        }

        BeginRun(initialPlayerHealth);
    }

    public void BeginRun(float initialPlayerHealth)
    {
        CurrentEssence = 0;
        CurrentEncounterIndex = 0;
        BasePlayerMaxHealth = Mathf.Max(1.0f, initialPlayerHealth);
        PlayerCurrentHP = BasePlayerMaxHealth;
        CurrentCombo = 0;
        HighestCombo = 0;
        PerfectStreak = 0;
        LastBattleResult = null;
        Build.Reset();
        ResetShopSlotRerolls(false);
        ShopPurchaseMade = false;
        IsShopOpen = false;
        ShopVisitIndex = 0;
        CurrentShopSeed = 0;
        IsRunInitialized = true;
        EmitSignal(SignalName.EssenceChanged, CurrentEssence);
    }

    public void AddEssence(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        CurrentEssence += amount;
        EmitSignal(SignalName.EssenceChanged, CurrentEssence);
    }

    public bool CanAfford(int cost)
    {
        return cost >= 0 && CurrentEssence >= cost;
    }

    public bool SpendEssence(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        CurrentEssence -= cost;
        EmitSignal(SignalName.EssenceChanged, CurrentEssence);
        return true;
    }

    public void SetCurrentEncounter(int encounterIndex)
    {
        CurrentEncounterIndex = Mathf.Max(0, encounterIndex);
    }

    public void SetPlayerCurrentHP(float currentHealth)
    {
        PlayerCurrentHP = Mathf.Max(0.0f, currentHealth);
    }

    public void SetComboState(
        int currentCombo,
        int highestCombo,
        int perfectStreak)
    {
        CurrentCombo = Mathf.Max(0, currentCombo);
        HighestCombo = Mathf.Max(CurrentCombo, Mathf.Max(0, highestCombo));
        PerfectStreak = Mathf.Max(0, perfectStreak);
    }

    public float GetEffectivePlayerMaxHealth()
    {
        return Build.GetEffectiveMaxHealth(BasePlayerMaxHealth);
    }

    public int GetUpgradeStacks(string upgradeId)
    {
        return Build.GetStacks(upgradeId);
    }

    public bool HasUpgrade(string upgradeId)
    {
        return Build.HasUpgrade(upgradeId);
    }

    public bool AddUpgrade(UpgradeDataResource upgrade)
    {
        if (!IsRunInitialized || upgrade.IsConsumable || !Build.AddUpgrade(upgrade))
        {
            return false;
        }

        var effectiveMaxHealth = GetEffectivePlayerMaxHealth();
        if (upgrade.SpecialEffectId == UpgradeIds.Vitality)
        {
            var acquireHeal = upgrade.SecondaryValue > 0.0f
                ? upgrade.SecondaryValue
                : 10.0f;
            PlayerCurrentHP = Mathf.Clamp(
                PlayerCurrentHP + acquireHeal,
                0.0f,
                effectiveMaxHealth);
        }
        else
        {
            PlayerCurrentHP = Mathf.Min(PlayerCurrentHP, effectiveMaxHealth);
        }

        EmitSignal(
            SignalName.UpgradeAdded,
            upgrade.Id,
            Build.GetStacks(upgrade.Id));
        return true;
    }

    public string GetBuildDebugSummary()
    {
        return Build.GetDebugSummary();
    }

    public int StartShop(int seed)
    {
        if (IsShopOpen)
        {
            return CurrentShopSeed;
        }

        IsShopOpen = true;
        ResetShopSlotRerolls(true);
        ShopPurchaseMade = false;
        ShopVisitIndex += 1;
        CurrentShopSeed = unchecked(
            seed +
            CurrentEncounterIndex * 7919 +
            ShopVisitIndex * 104729);
        EmitShopStateChanged();
        return CurrentShopSeed;
    }

    public bool IsShopSlotRerollAvailable(int slotIndex)
    {
        return IsShopOpen &&
            slotIndex >= 0 &&
            slotIndex < _shopSlotRerollsAvailable.Length &&
            _shopSlotRerollsAvailable[slotIndex];
    }

    public bool TryUseShopReroll(int slotIndex)
    {
        if (!IsShopSlotRerollAvailable(slotIndex))
        {
            return false;
        }

        _shopSlotRerollsAvailable[slotIndex] = false;
        EmitShopStateChanged();
        return true;
    }

    public bool TryPurchaseUpgrade(UpgradeDataResource upgrade)
    {
        if (!IsShopOpen ||
            ShopPurchaseMade ||
            upgrade == null ||
            !upgrade.IsValid ||
            (!upgrade.IsConsumable && !Build.CanAcquire(upgrade)))
        {
            return false;
        }

        if (!SpendEssence(upgrade.EssenceCost))
        {
            return false;
        }

        var applied = upgrade.IsConsumable
            ? ApplyConsumable(upgrade)
            : AddUpgrade(upgrade);
        if (!applied)
        {
            AddEssence(upgrade.EssenceCost);
            return false;
        }

        ShopPurchaseMade = true;
        EmitShopStateChanged();
        return true;
    }

    private bool ApplyConsumable(UpgradeDataResource upgrade)
    {
        if (upgrade.SpecialEffectId != UpgradeIds.ArcaneRebirth)
        {
            return false;
        }

        var healPercent = upgrade.PrimaryValue > 0.0f
            ? upgrade.PrimaryValue
            : 0.50f;
        var maxHealth = GetEffectivePlayerMaxHealth();
        var healAmount = maxHealth * Mathf.Clamp(healPercent, 0.0f, 1.0f);
        PlayerCurrentHP = Mathf.Clamp(
            PlayerCurrentHP + healAmount,
            0.0f,
            maxHealth);
        return true;
    }

    public bool AdvanceToNextEncounter(int encounterCount)
    {
        if (!IsShopOpen)
        {
            return false;
        }

        IsShopOpen = false;
        ResetShopSlotRerolls(false);
        ShopPurchaseMade = false;
        var nextEncounter = CurrentEncounterIndex + 1;
        var hasNextEncounter = nextEncounter >= 0 && nextEncounter < encounterCount;
        if (hasNextEncounter)
        {
            CurrentEncounterIndex = nextEncounter;
        }

        EmitShopStateChanged();
        return hasNextEncounter;
    }

    public void SetLastBattleResult(BattleResultData result)
    {
        LastBattleResult = result;
    }

    public void ClearLastBattleResult()
    {
        LastBattleResult = null;
    }

    private void EmitShopStateChanged()
    {
        EmitSignal(
            SignalName.ShopStateChanged,
            ShopRerollsRemaining,
            ShopPurchaseMade);
    }

    private void ResetShopSlotRerolls(bool available)
    {
        for (var index = 0; index < _shopSlotRerollsAvailable.Length; index++)
        {
            _shopSlotRerollsAvailable[index] = available;
        }
    }
}

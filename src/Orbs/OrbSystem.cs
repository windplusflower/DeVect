using System;
using System.Collections.Generic;
using DeVect.Combat;
using DeVect.Orbs.Definitions;
using DeVect.Orbs.Runtime;
using DeVect.Visual;
using GlobalEnums;
using HutongGames.PlayMaker;
using UnityEngine;

namespace DeVect.Orbs;

internal readonly struct HeroDamageInterceptionResult
{
    public HeroDamageInterceptionResult(int damageToPassIntoTakeDamage, bool shouldForceFullHitStateWithoutHealthLoss, int absorbedDamage)
    {
        DamageToPassIntoTakeDamage = damageToPassIntoTakeDamage;
        ShouldForceFullHitStateWithoutHealthLoss = shouldForceFullHitStateWithoutHealthLoss;
        AbsorbedDamage = absorbedDamage;
    }

    public int DamageToPassIntoTakeDamage { get; }

    public bool ShouldForceFullHitStateWithoutHealthLoss { get; }

    public int AbsorbedDamage { get; }

    public static HeroDamageInterceptionResult PassThrough(int damageAmount)
    {
        return new HeroDamageInterceptionResult(damageAmount, false, 0);
    }
}

internal sealed class OrbSystem
{
    private const float SpawnIntervalSeconds = OrbRuntime.SlotMoveDurationSeconds;

    private readonly Func<bool> _isEnabled;
    private readonly Func<bool> _isShuttingDown;
    private readonly Func<int> _getCurrentNailDamage;
    private readonly Func<HeroController?> _getHero;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logDebug;
    private readonly OrbRuntime _runtime;
    private readonly OrbCombatService _combatService;
    private readonly OrbVisualService _visualService;
    private readonly IceShieldDisplay _iceShieldDisplay;
    private readonly OrbDefinitionRegistry _definitions;
    private readonly OrbPersistentState _persistentState;
    private readonly IceShieldState _shieldState;
    private readonly Queue<QueuedOrbSpawn> _pendingSpawns = new();
    private readonly List<PendingOrbRelease> _pendingDelayedSpawns = new();
    private bool _spellFsmInjected;
    private bool _pendingZeroHealthLossDamage;
    private FormMode _currentForm = FormMode.Lightning;
    private float _spawnQueueTimer;
    private int _roomGeneratedLightningOrbCount;

    public OrbSystem(OrbSystemDependencies dependencies)
    {
        _isEnabled = dependencies.IsEnabled ?? throw new ArgumentNullException(nameof(dependencies.IsEnabled));
        _isShuttingDown = dependencies.IsShuttingDown ?? throw new ArgumentNullException(nameof(dependencies.IsShuttingDown));
        _getCurrentNailDamage = dependencies.GetCurrentNailDamage ?? throw new ArgumentNullException(nameof(dependencies.GetCurrentNailDamage));
        _getHero = dependencies.GetHero ?? throw new ArgumentNullException(nameof(dependencies.GetHero));
        _logInfo = dependencies.LogInfo ?? throw new ArgumentNullException(nameof(dependencies.LogInfo));
        _logDebug = dependencies.LogDebug ?? throw new ArgumentNullException(nameof(dependencies.LogDebug));
        _persistentState = dependencies.PersistentState ?? throw new ArgumentNullException(nameof(dependencies.PersistentState));
        _shieldState = dependencies.ShieldState ?? throw new ArgumentNullException(nameof(dependencies.ShieldState));

        _visualService = new OrbVisualService();
        _iceShieldDisplay = new IceShieldDisplay();
        _combatService = new OrbCombatService();
        _definitions = new OrbDefinitionRegistry(new IOrbDefinition[]
        {
            new YellowOrbDefinition(),
            new IceOrbDefinition(_shieldState),
            new WhiteOrbDefinition()
        });
        _runtime = new OrbRuntime(_visualService);
    }

    public void OnHeroUpdate(HeroController hero, float deltaTime)
    {
        if (!CanProcess())
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        TickSpawnQueue(hero, deltaTime);
        TickDelayedSpawns(hero, deltaTime);
        _visualService.TickHeroOrbCastAnimation(hero, deltaTime);
        _runtime.TickFollow();
        _runtime.TickAnimations(deltaTime);
        _iceShieldDisplay.Tick(_shieldState.GetPetalCount());
        _visualService.TickHeroFormAura(hero, _currentForm, true);
        _combatService.TickDebugVisuals();
    }

    public HeroDamageInterceptionResult OnHeroTakeDamage(ref int hazardType, int damageAmount)
    {
        HeroController? hero = _getHero();
        if (!CanProcess() || damageAmount <= 0 || hero == null || !CanHeroTakeDamage(hero, hazardType))
        {
            return HeroDamageInterceptionResult.PassThrough(damageAmount);
        }

        int remainingDamage = _shieldState.AbsorbDamage(damageAmount, out int absorbedDamage);
        if (absorbedDamage > 0)
        {
            _logDebug($"Ice shield absorbed {absorbedDamage} damage. Remaining petals={_shieldState.GetPetalCount()}.");
        }

        if (absorbedDamage <= 0)
        {
            return HeroDamageInterceptionResult.PassThrough(damageAmount);
        }

        bool shouldForceFullHitStateWithoutHealthLoss = remainingDamage <= 0;
        int damageToPassIntoTakeDamage = shouldForceFullHitStateWithoutHealthLoss ? damageAmount : remainingDamage;
        return new HeroDamageInterceptionResult(damageToPassIntoTakeDamage, shouldForceFullHitStateWithoutHealthLoss, absorbedDamage);
    }

    public void MarkPendingZeroHealthLossDamage()
    {
        _pendingZeroHealthLossDamage = true;
    }

    public void ClearPendingZeroHealthLossDamage()
    {
        _pendingZeroHealthLossDamage = false;
    }

    public int OnHeroAfterTakeDamage(int hazardType, int damageAmount)
    {
        if (!CanProcess())
        {
            return damageAmount;
        }

        if (_pendingZeroHealthLossDamage)
        {
            _pendingZeroHealthLossDamage = false;
            return 0;
        }

        return damageAmount;
    }

    public void OnHeroTookDamage(int hazardType, int damageAmount)
    {
    }

    public void OnHeroTookShieldedDamage(int hazardType, int damageAmount)
    {
    }

    public void OnHeroNailParry(HeroController hero)
    {
    }

    public void OnHeroShadowDashDodge(HeroController hero, string? debugDetail = null)
    {
    }

    public void OnFireballCast()
    {
        HeroController? hero = _getHero();
        if (hero == null)
        {
            return;
        }

        ToggleForm(hero);
    }

    public void OnShriekCast()
    {
        if (!CanProcess())
        {
            return;
        }

        HeroController? hero = _getHero();
        if (hero == null)
        {
            return;
        }

        if (_currentForm == FormMode.Lightning)
        {
            EnqueueOrbSpawns(OrbTypeId.Yellow, _roomGeneratedLightningOrbCount);
            return;
        }

        List<HealthManager> enemies = _combatService.FindAllEnemiesInRadius(hero, 20f);
        DealIceBigSkillRoomAoe(hero, enemies, _getCurrentNailDamage());
        EnqueueOrbSpawns(OrbTypeId.Black, enemies.Count + 2);
    }

    public void OnDiveCast()
    {
        if (!CanProcess())
        {
            return;
        }

        EnqueueOrbSpawns(GetOrbTypeForCurrentForm(), 1);
    }

    public bool ShouldConsumeFireballSpell()
    {
        return CanProcess() && _getHero() != null;
    }

    public bool ShouldConsumeDiveSpell()
    {
        return CanProcess() && _getHero() != null && CanGenerateOrbForSpell(GetOrbTypeForCurrentForm());
    }

    public bool ShouldConsumeShriekSpell()
    {
        return CanProcess() && _getHero() != null && CanGenerateOrbForSpell(GetOrbTypeForCurrentForm());
    }

    public bool IsAttackLocked()
    {
        return _pendingSpawns.Count > 0 || _pendingDelayedSpawns.Count > 0;
    }

    public int GetShamanStoneBonusFromNailDamage()
    {
        PlayerData? playerData = PlayerData.instance;
        return playerData != null && HasShamanStone(playerData)
            ? Mathf.CeilToInt(_getCurrentNailDamage() * 0.2f)
            : 0;
    }

    public int GetOrbSlotCapacity()
    {
        PlayerData? playerData = PlayerData.instance;
        return playerData != null && HasFlukenest(playerData) ? 4 : 3;
    }

    public void OnSceneChanged()
    {
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _visualService.ClearHeroFormAura();
        _runtime.Dispose();
        _persistentState.Clear();
        _pendingSpawns.Clear();
        _pendingDelayedSpawns.Clear();
        _spawnQueueTimer = 0f;
        _roomGeneratedLightningOrbCount = 0;
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
    }

    public void OnShutdown()
    {
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
        _spawnQueueTimer = 0f;
        _roomGeneratedLightningOrbCount = 0;
        _pendingSpawns.Clear();
        _pendingDelayedSpawns.Clear();
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _visualService.ClearHeroFormAura();
        _runtime.Dispose();
    }

    public void ResetAll()
    {
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
        _currentForm = FormMode.Lightning;
        _spawnQueueTimer = 0f;
        _roomGeneratedLightningOrbCount = 0;
        _pendingSpawns.Clear();
        _pendingDelayedSpawns.Clear();
        _shieldState.Clear();
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _visualService.ClearHeroFormAura();
        _runtime.Dispose();
    }

    public void ClearGeneratedOrbs()
    {
        _pendingSpawns.Clear();
        _pendingDelayedSpawns.Clear();
        _spawnQueueTimer = 0f;
        _persistentState.Clear();
        _runtime.Dispose();
        _logDebug("Cleared all generated orbs on bench save.");
    }

    public void ClearIceShield()
    {
        _shieldState.Clear();
        _logDebug("Cleared ice shield on bench save/load.");
    }

    public bool ShouldInjectSpellFsm(PlayMakerFSM fsm, HeroController hero)
    {
        return CanProcess() && fsm != null && !_spellFsmInjected && fsm.gameObject == hero.gameObject && fsm.FsmName == "Spell Control";
    }

    public bool MarkSpellFsmInjected()
    {
        _spellFsmInjected = true;
        return true;
    }

    public OrbPersistentState SnapshotPersistentState()
    {
        _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
        return _persistentState;
    }

    public bool CanGenerateOrbForSpell(OrbTypeId orbType)
    {
        PlayerData? playerData = PlayerData.instance;
        return playerData != null && HasUnlockedSpellForOrb(playerData, orbType);
    }

    private void HandleSpellCast(HeroController hero, OrbTypeId spawnType)
    {
        if (!CanProcess())
        {
            return;
        }

        if (hero == null || !CanGenerateOrbForSpell(spawnType))
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        OrbTriggerContext context = CreateTriggerContext(hero);
        int slotCapacity = GetOrbSlotCapacity();
        int initialDamage = _definitions.Get(spawnType).GetInitialDamage(context);
        if (_runtime.GetActiveOrbCount() >= slotCapacity)
        {
            TriggerEvocation(hero, spawnType, initialDamage);
            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
            return;
        }

        if (_runtime.TrySpawnOrbInNextAvailableSlot(spawnType, initialDamage, _definitions))
        {
            if (spawnType == OrbTypeId.Yellow)
            {
                _roomGeneratedLightningOrbCount++;
            }

            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
            _logDebug($"Spawned {spawnType} orb. Filled slots={_persistentState.GetFilledCount()}");
        }
    }

    private void ToggleForm(HeroController hero)
    {
        if (!CanProcess() || hero == null)
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        _currentForm = _currentForm == FormMode.Lightning ? FormMode.Ice : FormMode.Lightning;
        TriggerPassiveOrbs(hero);
        _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
    }

    private void EnqueueOrbSpawns(OrbTypeId orbType, int count)
    {
        if (count <= 0)
        {
            return;
        }

        bool queueWasEmpty = _pendingSpawns.Count == 0;
        for (int i = 0; i < count; i++)
        {
            _pendingSpawns.Enqueue(new QueuedOrbSpawn(orbType, 1));
        }

        if (queueWasEmpty)
        {
            _spawnQueueTimer = SpawnIntervalSeconds;
        }
    }

    private void TickSpawnQueue(HeroController hero, float deltaTime)
    {
        if (_pendingSpawns.Count <= 0)
        {
            _spawnQueueTimer = 0f;
            return;
        }

        _spawnQueueTimer -= deltaTime;
        while (_pendingSpawns.Count > 0 && _spawnQueueTimer <= 0f)
        {
            bool queuedDelayedSpawn = TryProcessNextQueuedSpawn(hero);
            if (queuedDelayedSpawn)
            {
                _spawnQueueTimer += SpawnIntervalSeconds;
                continue;
            }

            if (_pendingSpawns.Count > 0)
            {
                _spawnQueueTimer += SpawnIntervalSeconds;
            }
            else
            {
                _spawnQueueTimer = 0f;
            }
        }
    }

    private bool TryProcessNextQueuedSpawn(HeroController hero)
    {
        if (_pendingSpawns.Count <= 0 || hero == null)
        {
            return false;
        }

        QueuedOrbSpawn queuedSpawn = _pendingSpawns.Dequeue();
        bool playedCastAnimation = _visualService.TryPlayHeroOrbCastAnimation(hero);
        if (!playedCastAnimation)
        {
            HandleSpellCast(hero, queuedSpawn.OrbType);
            return false;
        }

        float releaseDelay = _visualService.GetHeroOrbCastReleaseDelay(hero);
        if (releaseDelay <= 0f)
        {
            HandleSpellCast(hero, queuedSpawn.OrbType);
            return false;
        }

        _pendingDelayedSpawns.Add(new PendingOrbRelease(queuedSpawn.OrbType, releaseDelay));
        return true;
    }

    private void TickDelayedSpawns(HeroController hero, float deltaTime)
    {
        if (_pendingDelayedSpawns.Count <= 0 || hero == null)
        {
            return;
        }

        float clampedDeltaTime = Mathf.Max(0f, deltaTime);
        for (int i = 0; i < _pendingDelayedSpawns.Count; i++)
        {
            PendingOrbRelease pendingRelease = _pendingDelayedSpawns[i];
            pendingRelease.RemainingDelaySeconds -= clampedDeltaTime;
            _pendingDelayedSpawns[i] = pendingRelease;
        }

        for (int i = _pendingDelayedSpawns.Count - 1; i >= 0; i--)
        {
            PendingOrbRelease pendingRelease = _pendingDelayedSpawns[i];
            if (pendingRelease.RemainingDelaySeconds > 0f)
            {
                continue;
            }

            _pendingDelayedSpawns.RemoveAt(i);
            HandleSpellCast(hero, pendingRelease.OrbType);
        }
    }

    private struct PendingOrbRelease
    {
        public PendingOrbRelease(OrbTypeId orbType, float remainingDelaySeconds)
        {
            OrbType = orbType;
            RemainingDelaySeconds = remainingDelaySeconds;
        }

        public OrbTypeId OrbType { get; }

        public float RemainingDelaySeconds { get; set; }
    }

    private OrbTypeId GetOrbTypeForCurrentForm()
    {
        return _currentForm == FormMode.Lightning ? OrbTypeId.Yellow : OrbTypeId.Black;
    }

    private void TriggerPassiveOrbs(HeroController hero, bool triggerIceOrbPassives = true)
    {
        if (!_runtime.HasAnyActiveOrb())
        {
            return;
        }

        OrbTriggerContext context = CreateTriggerContext(hero);
        List<OrbInstance> activeOrbs = new(_runtime.EnumerateActiveOrbs());
        int whiteOrbCount = 0;
        List<OrbInstance> eligibleOrbs = new();
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            OrbInstance orb = activeOrbs[i];
            if (orb.TypeId == OrbTypeId.White)
            {
                whiteOrbCount++;
                continue;
            }

            if (!triggerIceOrbPassives && orb.TypeId == OrbTypeId.Black)
            {
                continue;
            }

            eligibleOrbs.Add(orb);
        }

        int triggerCount = 1 + whiteOrbCount;
        for (int i = 0; i < eligibleOrbs.Count; i++)
        {
            OrbInstance orb = eligibleOrbs[i];
            for (int triggerIndex = 0; triggerIndex < triggerCount; triggerIndex++)
            {
                orb.Definition.OnPassive(context, orb);
            }
        }

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (!activeOrbs[i].IsPendingRemoval)
            {
                continue;
            }

            _runtime.RemoveOrb(activeOrbs[i]);
        }
    }

    private void TriggerEvocation(HeroController hero, OrbTypeId spawnType, int initialDamage)
    {
        List<OrbInstance> preExistingOrbs = new(_runtime.EnumerateActiveOrbs());
        int whiteOrbCount = 0;
        for (int i = 0; i < preExistingOrbs.Count; i++)
        {
            if (preExistingOrbs[i].TypeId == OrbTypeId.White)
            {
                whiteOrbCount++;
            }
        }

        if (!_runtime.TryForceInsertOrbFromLeft(spawnType, initialDamage, _definitions, out OrbInstance? evictedOrb) || evictedOrb == null)
        {
            return;
        }

        if (spawnType == OrbTypeId.Yellow)
        {
            _roomGeneratedLightningOrbCount++;
        }

        _logDebug($"TriggerEvocation -> spawnType={spawnType}, evictedOrb.TypeId={evictedOrb.TypeId}; triggering evocation.");
        OrbTriggerContext context = CreateTriggerContext(hero);
        if (evictedOrb.TypeId != OrbTypeId.White)
        {
            evictedOrb.Definition.OnEvocation(context, evictedOrb);
            return;
        }

        for (int i = 0; i < preExistingOrbs.Count; i++)
        {
            OrbInstance orb = preExistingOrbs[i];
            if (ReferenceEquals(orb, evictedOrb) || orb.TypeId == OrbTypeId.White)
            {
                continue;
            }

            for (int triggerIndex = 0; triggerIndex < whiteOrbCount; triggerIndex++)
            {
                orb.Definition.OnEvocation(context, orb);
            }
        }
    }

    private void DealIceBigSkillRoomAoe(HeroController hero, List<HealthManager> enemiesInRadius, int damage)
    {
        for (int i = 0; i < enemiesInRadius.Count; i++)
        {
            _combatService.TryDealOrbDamage(hero, enemiesInRadius[i], damage, AttackTypes.Generic);
        }
    }

    private OrbTriggerContext CreateTriggerContext(HeroController hero)
    {
        return new OrbTriggerContext(hero, _getCurrentNailDamage(), GetShamanStoneBonusFromNailDamage(), GetCurrentSpellLevelForOrb, _combatService, _visualService, _runtime, _logDebug);
    }

    private void RestoreRuntimeIfNeeded(HeroController hero)
    {
        int slotCapacity = GetOrbSlotCapacity();
        if (_runtime.Capacity > 0 && _runtime.IsBoundTo(hero.transform) && _runtime.Capacity != slotCapacity)
        {
            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
        }

        _runtime.EnsureBuilt(hero.transform, slotCapacity, _persistentState.FilledOrbs, _definitions);
    }

    private static bool HasShamanStone(PlayerData playerData)
    {
        return playerData.GetBool("equippedCharm_19");
    }

    private static bool HasFlukenest(PlayerData playerData)
    {
        return playerData.GetBool("equippedCharm_11");
    }

    private int GetCurrentSpellLevelForOrb(OrbTypeId orbType)
    {
        PlayerData? playerData = PlayerData.instance;
        return playerData == null ? 0 : GetSpellLevelForOrb(playerData, orbType);
    }

    private static bool HasUnlockedSpellForOrb(PlayerData playerData, OrbTypeId orbType)
    {
        return GetSpellLevelForOrb(playerData, orbType) > 0;
    }

    private static int GetSpellLevelForOrb(PlayerData playerData, OrbTypeId orbType)
    {
        return orbType switch
        {
            OrbTypeId.Yellow => Math.Max(0, playerData.GetInt("fireballLevel")),
            OrbTypeId.White => Math.Max(0, playerData.GetInt("screamLevel")),
            OrbTypeId.Black => Math.Max(0, playerData.GetInt("quakeLevel")),
            _ => 0
        };
    }

    private static bool CanHeroTakeDamage(HeroController hero, int hazardType)
    {
        PlayerData? playerData = PlayerData.instance;
        if (playerData == null)
        {
            return false;
        }

        bool canTakeDamage = hero.damageMode != DamageMode.NO_DAMAGE
            && hero.transitionState == HeroTransitionState.WAITING_TO_TRANSITION
            && !hero.cState.invulnerable
            && !hero.cState.recoiling
            && !playerData.GetBool("isInvincible")
            && !hero.cState.dead
            && !hero.cState.hazardDeath
            && !BossSceneController.IsTransitioning;

        if (!canTakeDamage)
        {
            return false;
        }

        if (hero.damageMode == DamageMode.HAZARD_ONLY && hazardType == 1)
        {
            return false;
        }

        if (hero.cState.shadowDashing && hazardType == 1)
        {
            return false;
        }

        return !(hero.parryInvulnTimer > 0f && hazardType == 1);
    }

    private bool CanProcess()
    {
        return _isEnabled() && !_isShuttingDown();
    }
}

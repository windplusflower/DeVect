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
    private int _roundCounter;
    private bool _parryWindowConsumed;
    private int _lastShadowDashDodgeAdvanceFrame = -1;
    private bool _spellFsmInjected;
    private bool _pendingZeroHealthLossDamage;

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

        if (hero.parryInvulnTimer <= 0f)
        {
            _parryWindowConsumed = false;
        }

        RestoreRuntimeIfNeeded(hero);
        _runtime.TickFollow();
        _runtime.TickAnimations(deltaTime);
        _iceShieldDisplay.Tick(_shieldState.GetPetalCount());
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
        if (!CanProcess() || !ShouldAdvanceRoundFromHeroDamage(hazardType, damageAmount))
        {
            return;
        }

        HeroController? hero = _getHero();
        if (hero == null)
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        AdvanceRound(hero, RoundAdvanceSource.HeroTookDamage, $"hazardType={hazardType}, damageAmount={damageAmount}");
    }

    public void OnHeroNailParry(HeroController hero)
    {
        if (!CanProcess() || hero == null)
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        if (!ShouldAdvanceRoundFromHeroParry(hero))
        {
            return;
        }

        _parryWindowConsumed = true;
        AdvanceRound(hero, RoundAdvanceSource.HeroNailParry);
    }

    public void OnHeroShadowDashDodge(HeroController hero, string? debugDetail = null)
    {
        if (!CanProcess() || hero == null)
        {
            return;
        }

        RestoreRuntimeIfNeeded(hero);
        if (!ShouldAdvanceRoundFromHeroShadowDashDodge(hero))
        {
            return;
        }

        _lastShadowDashDodgeAdvanceFrame = Time.frameCount;
        AdvanceRound(hero, RoundAdvanceSource.HeroShadowDashDodge, debugDetail);
    }

    public void OnFireballCast()
    {
        HandleSpellCast(OrbTypeId.Yellow);
    }

    public void OnShriekCast()
    {
        HandleSpellCast(OrbTypeId.White);
    }

    public void OnDiveCast()
    {
        HandleSpellCast(OrbTypeId.Black);
    }

    public bool ShouldConsumeFireballSpell()
    {
        return CanConsumeSpellForOrb(OrbTypeId.Yellow);
    }

    public bool ShouldConsumeDiveSpell()
    {
        return CanConsumeSpellForOrb(OrbTypeId.Black);
    }

    public bool ShouldConsumeShriekSpell()
    {
        return CanConsumeSpellForOrb(OrbTypeId.White);
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

    private void HandleSpellCast(OrbTypeId spawnType)
    {
        if (!CanProcess())
        {
            return;
        }

        HeroController? hero = _getHero();
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
            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
            _logDebug($"Spawned {spawnType} orb. Filled slots={_persistentState.GetFilledCount()}");
        }
    }

    public void OnSceneChanged()
    {
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _runtime.SuspendAndRemember(_persistentState);
        _parryWindowConsumed = false;
        _lastShadowDashDodgeAdvanceFrame = -1;
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
    }

    public void OnShutdown()
    {
        _roundCounter = 0;
        _parryWindowConsumed = false;
        _lastShadowDashDodgeAdvanceFrame = -1;
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _runtime.Dispose();
    }

    public void ResetAll()
    {
        _roundCounter = 0;
        _parryWindowConsumed = false;
        _lastShadowDashDodgeAdvanceFrame = -1;
        _spellFsmInjected = false;
        _pendingZeroHealthLossDamage = false;
        _shieldState.Clear();
        _combatService.DisposeDebugVisuals();
        _iceShieldDisplay.Dispose();
        _runtime.Dispose();
    }

    public void ClearGeneratedOrbs()
    {
        _persistentState.Clear();
        _runtime.Dispose();
        _logDebug("Cleared all generated orbs on bench save.");
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

    private void TriggerPassiveOrbs(HeroController hero)
    {
        if (!_runtime.HasAnyActiveOrb())
        {
            return;
        }

        OrbTriggerContext context = CreateTriggerContext(hero);
        List<OrbInstance> activeOrbs = new(_runtime.EnumerateActiveOrbs());
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            activeOrbs[i].Definition.OnPassive(context, activeOrbs[i]);
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
        if (!_runtime.TryForceInsertOrbFromLeft(spawnType, initialDamage, _definitions, out OrbInstance? evictedOrb) || evictedOrb == null)
        {
            return;
        }

        _logDebug($"TriggerEvocation -> spawnType={spawnType}, evictedOrb.TypeId={evictedOrb.TypeId}; triggering evocation.");
        evictedOrb.Definition.OnEvocation(CreateTriggerContext(hero), evictedOrb);
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

    public bool CanGenerateOrbForSpell(OrbTypeId orbType)
    {
        PlayerData? playerData = PlayerData.instance;
        return playerData != null && HasUnlockedSpellForOrb(playerData, orbType);
    }

    private static bool HasFlukenest(PlayerData playerData)
    {
        return playerData.GetBool("equippedCharm_11");
    }

    private bool CanConsumeSpellForOrb(OrbTypeId orbType)
    {
        return CanProcess() && _getHero() != null && CanGenerateOrbForSpell(orbType);
    }

    private void AdvanceRound(HeroController hero, RoundAdvanceSource source, string? debugDetail = null)
    {
        _roundCounter++;
        string detailSuffix = string.IsNullOrEmpty(debugDetail) ? string.Empty : $" ({debugDetail})";
        _logDebug($"Advanced round {_roundCounter} via {source}{detailSuffix}.");
        TriggerPassiveOrbs(hero);
        _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
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

    private static bool ShouldAdvanceRoundFromHeroDamage(int hazardType, int damageAmount)
    {
        return damageAmount > 0 && (hazardType == 0 || hazardType == 1);
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

    private bool ShouldAdvanceRoundFromHeroParry(HeroController hero)
    {
        return !_parryWindowConsumed;
    }

    private bool ShouldAdvanceRoundFromHeroShadowDashDodge(HeroController hero)
    {
        return hero.cState.shadowDashing && Time.frameCount != _lastShadowDashDodgeAdvanceFrame;
    }

    private bool CanProcess()
    {
        return _isEnabled() && !_isShuttingDown();
    }
}

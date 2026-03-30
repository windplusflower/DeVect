using System;
using System.Collections.Generic;
using DeVect.Combat;
using DeVect.Orbs.Definitions;
using DeVect.Orbs.Runtime;
using DeVect.Visual;
using HutongGames.PlayMaker;
using UnityEngine;

namespace DeVect.Orbs;

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
    private readonly OrbDefinitionRegistry _definitions;
    private readonly OrbPersistentState _persistentState;
    private int _roundCounter;
    private bool _spellFsmInjected;

    public OrbSystem(OrbSystemDependencies dependencies)
    {
        _isEnabled = dependencies.IsEnabled ?? throw new ArgumentNullException(nameof(dependencies.IsEnabled));
        _isShuttingDown = dependencies.IsShuttingDown ?? throw new ArgumentNullException(nameof(dependencies.IsShuttingDown));
        _getCurrentNailDamage = dependencies.GetCurrentNailDamage ?? throw new ArgumentNullException(nameof(dependencies.GetCurrentNailDamage));
        _getHero = dependencies.GetHero ?? throw new ArgumentNullException(nameof(dependencies.GetHero));
        _logInfo = dependencies.LogInfo ?? throw new ArgumentNullException(nameof(dependencies.LogInfo));
        _logDebug = dependencies.LogDebug ?? throw new ArgumentNullException(nameof(dependencies.LogDebug));
        _persistentState = dependencies.PersistentState ?? throw new ArgumentNullException(nameof(dependencies.PersistentState));

        _visualService = new OrbVisualService();
        _combatService = new OrbCombatService();
        _definitions = new OrbDefinitionRegistry(new IOrbDefinition[]
        {
            new YellowOrbDefinition(),
            new BlackOrbDefinition(),
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
        _runtime.TickFollow();
        _runtime.TickAnimations(deltaTime);
        _combatService.TickDebugVisuals();
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
        AdvanceRound(hero, RoundAdvanceSource.HeroTookDamage, hazardType, damageAmount);
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
        _runtime.SuspendAndRemember(_persistentState);
        _spellFsmInjected = false;
    }

    public void OnShutdown()
    {
        _roundCounter = 0;
        _spellFsmInjected = false;
        _combatService.DisposeDebugVisuals();
        _runtime.Dispose();
    }

    public void ResetAll()
    {
        _roundCounter = 0;
        _spellFsmInjected = false;
        _combatService.DisposeDebugVisuals();
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

    private void AdvanceRound(HeroController hero, RoundAdvanceSource source, int hazardType, int damageAmount)
    {
        _roundCounter++;
        _logDebug($"Advanced round {_roundCounter} via {source} (hazardType={hazardType}, damageAmount={damageAmount}).");
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
        return damageAmount > 0 && hazardType == 1;
    }

    private bool CanProcess()
    {
        return _isEnabled() && !_isShuttingDown();
    }
}

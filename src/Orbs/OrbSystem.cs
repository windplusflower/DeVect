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
    private const float SlashHitDedupWindowSeconds = 0.08f;

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
    private int _nailHitCounter;
    private int _lastProcessedSlashInstanceId;
    private float _lastProcessedSlashTime;
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

    public void OnSlashHit(Collider2D otherCollider, GameObject? slash)
    {
        if (!CanProcess() || !OrbCombatService.IsEnemyCollider(otherCollider))
        {
            return;
        }

        HeroController? hero = _getHero();
        if (hero == null)
        {
            return;
        }

        int slashInstanceId = slash != null ? slash.GetInstanceID() : 0;
        float now = Time.time;

        if (slash != null)
        {
            if (_lastProcessedSlashInstanceId == slashInstanceId
                && (now - _lastProcessedSlashTime) <= SlashHitDedupWindowSeconds)
            {
                return;
            }

            _lastProcessedSlashInstanceId = slashInstanceId;
            _lastProcessedSlashTime = now;
        }

        _nailHitCounter++;
        bool triggeredPassive = _nailHitCounter % 3 == 0;
        if (triggeredPassive)
        {
            TriggerPassiveOrbs(hero);
        }
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
        return CanProcess() && _getHero() != null;
    }

    public bool ShouldConsumeDiveSpell()
    {
        return CanProcess() && _getHero() != null;
    }

    private void HandleSpellCast(OrbTypeId spawnType)
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

        RestoreRuntimeIfNeeded(hero);
        if (_runtime.GetActiveOrbCount() >= 3)
        {
            TriggerEvocation(hero, spawnType);
            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
            return;
        }

        if (_runtime.TrySpawnOrbInNextAvailableSlot(spawnType, _getCurrentNailDamage(), _definitions))
        {
            _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
            _logDebug($"Spawned {spawnType} orb. Filled slots={_persistentState.GetFilledCount()}");
        }
    }

    public void OnSceneChanged()
    {
        _lastProcessedSlashInstanceId = 0;
        _lastProcessedSlashTime = 0f;
        _combatService.DisposeDebugVisuals();
        _runtime.SuspendAndRemember(_persistentState);
        _spellFsmInjected = false;
    }

    public void OnShutdown()
    {
        _lastProcessedSlashInstanceId = 0;
        _lastProcessedSlashTime = 0f;
        _spellFsmInjected = false;
        _combatService.DisposeDebugVisuals();
        _runtime.Dispose();
    }

    public void ResetAll()
    {
        _nailHitCounter = 0;
        _lastProcessedSlashInstanceId = 0;
        _lastProcessedSlashTime = 0f;
        _spellFsmInjected = false;
        _combatService.DisposeDebugVisuals();
        _runtime.Dispose();
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

        _persistentState.ReplaceFromRuntime(_runtime.SnapshotActiveOrbs());
    }

    private void TriggerEvocation(HeroController hero, OrbTypeId spawnType)
    {
        if (!_runtime.TryForceInsertOrbFromLeft(spawnType, _getCurrentNailDamage(), _definitions, out OrbInstance? evictedOrb) || evictedOrb == null)
        {
            return;
        }

        evictedOrb.Definition.OnEvocation(CreateTriggerContext(hero), evictedOrb);
    }

    private OrbTriggerContext CreateTriggerContext(HeroController hero)
    {
        return new OrbTriggerContext(hero, _getCurrentNailDamage(), _combatService, _visualService, _runtime, _logDebug);
    }

    private void RestoreRuntimeIfNeeded(HeroController hero)
    {
        _runtime.EnsureBuilt(hero.transform, _persistentState.FilledOrbs, _definitions);
    }

    private bool CanProcess()
    {
        return _isEnabled() && !_isShuttingDown();
    }
}

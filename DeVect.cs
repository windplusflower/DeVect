using System;
using System.Collections.Generic;
using DeVect.Combat;
using DeVect.Fsm;
using DeVect.Orbs;
using HutongGames.PlayMaker;
using Modding;
using UnityEngine;

namespace DeVect;

[Serializable]
public class DeVectSettings
{
    public bool Enabled = true;
}

public partial class DeVectMod : Mod, IGlobalSettings<DeVectSettings>, IMenuMod, ITogglableMod
{
    private DeVectSettings _settings = new();
    private OrbSystem? _orbSystem;
    private OrbPersistentState _persistentState = new();
    private int _lastKnownNailDamage;
    private bool _isShuttingDown;

    public DeVectMod() : base("DeVect")
    {
    }

    public override string GetVersion() => "0.1.0";

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        _isShuttingDown = false;
        EnsureOrbSystem();

        ModHooks.BeforeSavegameSaveHook += OnBeforeSavegameSave;
        ModHooks.HeroUpdateHook += OnHeroUpdate;
        ModHooks.SlashHitHook += OnSlashHit;
        On.PlayMakerFSM.OnEnable += OnPlayMakerFsmEnable;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Application.quitting += OnApplicationQuitting;

        LogModInfo("DeVect initialized.");
    }

    public void Unload()
    {
        ModHooks.BeforeSavegameSaveHook -= OnBeforeSavegameSave;
        ModHooks.HeroUpdateHook -= OnHeroUpdate;
        ModHooks.SlashHitHook -= OnSlashHit;
        On.PlayMakerFSM.OnEnable -= OnPlayMakerFsmEnable;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Application.quitting -= OnApplicationQuitting;
        ResetRuntimeState();
    }

    public void OnLoadGlobal(DeVectSettings settings)
    {
        _settings = settings ?? new DeVectSettings();
    }

    public DeVectSettings OnSaveGlobal() => _settings;

    public bool ToggleButtonInsideMenu => true;

    public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? menu)
    {
        return new List<IMenuMod.MenuEntry>
        {
            new IMenuMod.MenuEntry(
                "Enable DeVect",
                new[] { "Off", "On" },
                "Toggle the DeVect orb system.",
                index => _settings.Enabled = index == 1,
                () => _settings.Enabled ? 1 : 0
            )
        };
    }

    private void OnHeroUpdate()
    {
        if (!_settings.Enabled)
        {
            ResetRuntimeState();
            return;
        }

        if (_isShuttingDown)
        {
            return;
        }

        HeroController hero = HeroController.instance;
        if (hero == null || hero.transform == null)
        {
            _orbSystem?.OnSceneChanged();
            return;
        }

        EnsureOrbSystem();
        TryInjectHeroSpellFsm(hero);
        _orbSystem?.OnHeroUpdate(hero, Time.deltaTime);
    }

    private void OnSlashHit(Collider2D otherCollider, GameObject slash)
    {
        if (!_settings.Enabled || _isShuttingDown || !OrbCombatService.IsEnemyCollider(otherCollider))
        {
            return;
        }

        EnsureOrbSystem();
        _orbSystem?.OnSlashHit(otherCollider, slash);
    }

    private void OnPlayMakerFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
    {
        orig(self);

        if (!_settings.Enabled || _isShuttingDown || self == null)
        {
            return;
        }

        HeroController hero = HeroController.instance;
        if (hero == null)
        {
            return;
        }

        EnsureOrbSystem();
        if (!TryInjectSpellFsm(self, hero))
        {
            return;
        }

        LogModInfo("Injected spell detector into Spell Control FSM.");
    }

    private void TryInjectHeroSpellFsm(HeroController hero)
    {
        if (_orbSystem == null || hero == null)
        {
            return;
        }

        PlayMakerFSM? spellFsm = FSMUtility.LocateFSM(hero.gameObject, "Spell Control");
        if (TryInjectSpellFsm(spellFsm, hero))
        {
            LogModInfo("Injected spell detector into existing Spell Control FSM.");
        }
    }

    private bool TryInjectSpellFsm(PlayMakerFSM? fsm, HeroController hero)
    {
        if (_orbSystem == null || fsm == null || !_orbSystem.ShouldInjectSpellFsm(fsm, hero))
        {
            return false;
        }

        bool injected = false;
        injected |= InjectSpellDetector(fsm, "Spell Choice");
        injected |= InjectSpellDetector(fsm, "QC");
        if (injected)
        {
            _orbSystem.MarkSpellFsmInjected();
            return true;
        }

        return false;
    }

    private bool InjectSpellDetector(PlayMakerFSM? fsm, string stateName)
    {
        if (fsm == null)
        {
            return false;
        }

        FsmState state = fsm.Fsm.GetState(stateName);
        if (state == null || state.Actions == null)
        {
            return false;
        }

        for (int i = 0; i < state.Actions.Length; i++)
        {
            if (state.Actions[i] is SpellDetectAction)
            {
                return false;
            }
        }

        FsmStateAction[] newActions = new FsmStateAction[state.Actions.Length + 1];
        newActions[0] = new SpellDetectAction
        {
            OnFireballCast = HandleFireballCast,
            OnDiveCast = HandleDiveCast,
            OnShriekCast = HandleShriekCast,
            ShouldConsumeFireballSpell = ShouldConsumeFireballSpell,
            ShouldConsumeDiveSpell = ShouldConsumeDiveSpell,
            ShouldConsumeShriekSpell = ShouldConsumeShriekSpell
        };

        for (int i = 0; i < state.Actions.Length; i++)
        {
            newActions[i + 1] = state.Actions[i];
        }

        state.Actions = newActions;
        return true;
    }

    private void HandleFireballCast()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return;
        }

        EnsureOrbSystem();
        _orbSystem?.OnFireballCast();
    }

    private void HandleDiveCast()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return;
        }

        EnsureOrbSystem();
        _orbSystem?.OnDiveCast();
    }

    private void HandleShriekCast()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return;
        }

        EnsureOrbSystem();
        _orbSystem?.OnShriekCast();
    }

    private bool ShouldConsumeFireballSpell()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return false;
        }

        EnsureOrbSystem();
        return _orbSystem?.ShouldConsumeFireballSpell() ?? false;
    }

    private bool ShouldConsumeDiveSpell()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return false;
        }

        EnsureOrbSystem();
        return _orbSystem?.ShouldConsumeDiveSpell() ?? false;
    }

    private bool ShouldConsumeShriekSpell()
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return false;
        }

        EnsureOrbSystem();
        return _orbSystem?.ShouldConsumeShriekSpell() ?? false;
    }

    private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return;
        }

        _orbSystem?.OnSceneChanged();
    }

    private void OnApplicationQuitting()
    {
        _isShuttingDown = true;
        _orbSystem?.OnShutdown();
    }

    private void OnBeforeSavegameSave(SaveGameData data)
    {
        if (!_settings.Enabled || _isShuttingDown)
        {
            return;
        }

        PlayerData? playerData = PlayerData.instance;
        if (playerData == null || !playerData.GetBool("atBench"))
        {
            return;
        }

        EnsureOrbSystem();
        _orbSystem?.ClearGeneratedOrbs();
    }

    private void ResetRuntimeState()
    {
        _persistentState.Clear();
        _lastKnownNailDamage = 0;
        _isShuttingDown = false;
        _orbSystem?.ResetAll();
        EnsureOrbSystem();
    }

    private void EnsureOrbSystem()
    {
        if (_orbSystem != null)
        {
            return;
        }

        _orbSystem = new OrbSystem(
            new OrbSystemDependencies
            {
                IsEnabled = () => _settings.Enabled,
                IsShuttingDown = () => _isShuttingDown,
                GetCurrentNailDamage = GetCurrentNailDamage,
                GetHero = () => HeroController.instance,
                LogInfo = LogModInfo,
                LogDebug = LogModDebug,
                PersistentState = _persistentState
            }
        );
    }

    private int GetCurrentNailDamage()
    {
        if (GameManager.instance != null && GameManager.instance.playerData != null)
        {
            PlayerData playerData = GameManager.instance.playerData;
            int baseDamage = Math.Max(1, playerData.nailDamage);
            float multiplier = 1f;

            if (playerData.GetBool("equippedCharm_25"))
            {
                multiplier *= 1.5f;
            }

            if (playerData.GetBool("equippedCharm_6") && playerData.GetInt("health") == 1)
            {
                multiplier *= 1.75f;
            }

            _lastKnownNailDamage = Math.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        return Math.Max(1, _lastKnownNailDamage);
    }

    private static void LogModInfo(string message)
    {
        Modding.Logger.Log($"[DeVectMod] - {message}");
    }

    private static void LogModDebug(string message)
    {
        Modding.Logger.LogDebug($"[DeVectMod] - {message}");
    }
}

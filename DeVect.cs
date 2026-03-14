using System;
using System.Collections.Generic;
using Modding;
using UnityEngine;

namespace DeVect;

[Serializable]
public class DeVectSettings
{
    public bool Enabled = true;
    public int MinBaseHealth = 5;
}

public class DeVectMod : Mod, IGlobalSettings<DeVectSettings>, IMenuMod, ITogglableMod
{
    private DeVectSettings _settings = new();

    public DeVectMod() : base("DeVect")
    {
    }

    public override string GetVersion() => "0.1.0";

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        ModHooks.GetPlayerIntHook += OnGetPlayerInt;
        Log("DeVect initialized.");
    }

    public void Unload()
    {
        ModHooks.GetPlayerIntHook -= OnGetPlayerInt;
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
                "Toggle DeVect knight-body modifications.",
                index => _settings.Enabled = index == 1,
                () => _settings.Enabled ? 1 : 0
            ),
            new IMenuMod.MenuEntry(
                "Min Base Health",
                new[] { "3", "4", "5", "6", "7", "8", "9" },
                "Clamp maxHealthBase to at least this value.",
                index => _settings.MinBaseHealth = index + 3,
                () => Math.Max(0, Math.Min(6, _settings.MinBaseHealth - 3))
            )
        };
    }

    private int OnGetPlayerInt(string key, int value)
    {
        if (!_settings.Enabled)
        {
            return value;
        }

        if (key == "maxHealthBase")
        {
            return Math.Max(value, _settings.MinBaseHealth);
        }

        return value;
    }
}

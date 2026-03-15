using System;
using DeVect.Combat;
using DeVect.Orbs.Definitions;
using DeVect.Orbs.Runtime;
using DeVect.Visual;

namespace DeVect.Orbs;

internal sealed class OrbTriggerContext
{
    public OrbTriggerContext(
        HeroController hero,
        int nailDamage,
        int baseShamanBonus,
        Func<OrbTypeId, int> getSpellLevel,
        OrbCombatService combat,
        OrbVisualService visuals,
        OrbRuntime runtime,
        Action<string> logDebug)
    {
        Hero = hero;
        NailDamage = nailDamage;
        BaseShamanBonus = baseShamanBonus;
        GetSpellLevel = getSpellLevel ?? throw new ArgumentNullException(nameof(getSpellLevel));
        Combat = combat;
        Visuals = visuals;
        Runtime = runtime;
        LogDebug = logDebug;
    }

    public HeroController Hero { get; }

    public int NailDamage { get; }

    public int BaseShamanBonus { get; }

    public Func<OrbTypeId, int> GetSpellLevel { get; }

    public OrbCombatService Combat { get; }

    public OrbVisualService Visuals { get; }

    public OrbRuntime Runtime { get; }

    public Action<string> LogDebug { get; }

    public int GetScaledShamanBonus(float damageScale)
    {
        if (BaseShamanBonus <= 0 || damageScale <= 0f)
        {
            return 0;
        }

        return Math.Max(0, UnityEngine.Mathf.CeilToInt(NailDamage * 0.2f * damageScale));
    }
}

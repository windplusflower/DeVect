using System;
using DeVect.Combat;
using DeVect.Orbs.Runtime;
using DeVect.Visual;

namespace DeVect.Orbs;

internal sealed class OrbTriggerContext
{
    public OrbTriggerContext(
        HeroController hero,
        int nailDamage,
        OrbCombatService combat,
        OrbVisualService visuals,
        OrbRuntime runtime,
        Action<string> logDebug)
    {
        Hero = hero;
        NailDamage = nailDamage;
        Combat = combat;
        Visuals = visuals;
        Runtime = runtime;
        LogDebug = logDebug;
    }

    public HeroController Hero { get; }

    public int NailDamage { get; }

    public OrbCombatService Combat { get; }

    public OrbVisualService Visuals { get; }

    public OrbRuntime Runtime { get; }

    public Action<string> LogDebug { get; }
}

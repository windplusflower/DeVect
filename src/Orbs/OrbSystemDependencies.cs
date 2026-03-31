using System;
using DeVect.Combat;

namespace DeVect.Orbs;

internal sealed class OrbSystemDependencies
{
    public Func<bool>? IsEnabled { get; set; }

    public Func<bool>? IsShuttingDown { get; set; }

    public Func<int>? GetCurrentNailDamage { get; set; }

    public Func<HeroController?>? GetHero { get; set; }

    public Action<string>? LogInfo { get; set; }

    public Action<string>? LogDebug { get; set; }

    public OrbPersistentState? PersistentState { get; set; }

    public IceShieldState? ShieldState { get; set; }
}

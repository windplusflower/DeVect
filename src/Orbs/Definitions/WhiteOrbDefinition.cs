using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class WhiteOrbDefinition : IOrbDefinition
{
    public OrbTypeId TypeId => OrbTypeId.White;

    public string DisplayName => "White";

    public Color OrbColor => new(0.92f, 0.96f, 1f, 1f);

    public int GetInitialDamage(OrbTriggerContext context)
    {
        return 0;
    }

    public void OnPassive(OrbTriggerContext context, OrbInstance instance)
    {
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
    }
}

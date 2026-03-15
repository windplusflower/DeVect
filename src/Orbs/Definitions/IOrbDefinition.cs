using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal interface IOrbDefinition
{
    OrbTypeId TypeId { get; }

    string DisplayName { get; }

    Color OrbColor { get; }

    void OnPassive(OrbTriggerContext context, OrbInstance instance);

    void OnEvocation(OrbTriggerContext context, OrbInstance instance);
}

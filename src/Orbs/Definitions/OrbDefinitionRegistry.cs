using System;
using System.Collections.Generic;

namespace DeVect.Orbs.Definitions;

internal sealed class OrbDefinitionRegistry
{
    private readonly Dictionary<OrbTypeId, IOrbDefinition> _definitions;

    public OrbDefinitionRegistry(IEnumerable<IOrbDefinition> definitions)
    {
        _definitions = new Dictionary<OrbTypeId, IOrbDefinition>();
        foreach (IOrbDefinition definition in definitions)
        {
            _definitions[definition.TypeId] = definition;
        }
    }

    public IOrbDefinition Get(OrbTypeId typeId)
    {
        if (!_definitions.TryGetValue(typeId, out IOrbDefinition definition))
        {
            throw new InvalidOperationException($"Missing orb definition for {typeId}.");
        }

        return definition;
    }

    public bool TryGet(OrbTypeId typeId, out IOrbDefinition? definition)
    {
        return _definitions.TryGetValue(typeId, out definition);
    }

    public OrbTypeId GetDefaultTypeForFireball()
    {
        return OrbTypeId.Yellow;
    }
}

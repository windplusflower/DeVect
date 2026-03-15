using DeVect.Orbs.Definitions;
using UnityEngine;

namespace DeVect.Orbs.Runtime;

internal sealed class OrbInstance
{
    public OrbInstance(OrbTypeId typeId, IOrbDefinition definition, SpriteRenderer renderer)
    {
        TypeId = typeId;
        Definition = definition;
        Renderer = renderer;
    }

    public OrbTypeId TypeId { get; }

    public IOrbDefinition Definition { get; }

    public SpriteRenderer Renderer { get; }
}

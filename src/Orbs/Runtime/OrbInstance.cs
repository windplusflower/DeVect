using DeVect.Orbs.Definitions;
using UnityEngine;

namespace DeVect.Orbs.Runtime;

internal sealed class OrbInstance
{
    public OrbInstance(OrbTypeId typeId, IOrbDefinition definition, SpriteRenderer renderer, int currentDamage = 0)
    {
        TypeId = typeId;
        Definition = definition;
        Renderer = renderer;
        CurrentDamage = currentDamage;
    }

    public OrbTypeId TypeId { get; }

    public IOrbDefinition Definition { get; }

    public SpriteRenderer Renderer { get; }

    public int CurrentDamage { get; set; }

    public bool IsPendingRemoval { get; set; }
}

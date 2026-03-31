using DeVect.Combat;
using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class IceOrbDefinition : IOrbDefinition
{
    private readonly IceShieldState _shieldState;

    public IceOrbDefinition(IceShieldState shieldState)
    {
        _shieldState = shieldState;
    }

    public OrbTypeId TypeId => OrbTypeId.Black;

    public string DisplayName => "Ice";

    public Color OrbColor => new(0.72f, 0.9f, 1f, 1f);

    public int GetInitialDamage(OrbTriggerContext context)
    {
        return 0;
    }

    public void OnPassive(OrbTriggerContext context, OrbInstance instance)
    {
        int gainedPetals = _shieldState.AddShield(1);
        if (gainedPetals > 0)
        {
            context.Visuals.SpawnIcePetalEffect(context.Hero.transform.position + new Vector3(0f, 1.1f, 0f), 1);
        }

        context.LogDebug($"Ice passive granted {gainedPetals}/4 shield. Current petals={_shieldState.GetPetalCount()}.");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        int gainedPetals = _shieldState.AddShield(3);
        if (gainedPetals > 0)
        {
            context.Visuals.SpawnIcePetalEffect(context.Hero.transform.position + new Vector3(0f, 1.15f, 0f), IceShieldState.PetalsPerShield);
        }

        context.LogDebug($"Ice evocation granted {gainedPetals}/4 shield. Current petals={_shieldState.GetPetalCount()}.");
    }
}

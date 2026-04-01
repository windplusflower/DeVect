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
        ApplyShieldGain(context, 1, 1, 1.1f, "passive");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        ApplyShieldGain(context, 3, IceShieldState.PetalsPerShield, 1.15f, "evocation");
    }

    private void ApplyShieldGain(OrbTriggerContext context, int petalsToAdd, int effectPetalCount, float effectYOffset, string sourceLabel)
    {
        int gainedPetals = _shieldState.AddShield(petalsToAdd);
        if (gainedPetals > 0)
        {
            context.Visuals.SpawnIcePetalEffect(context.Hero.transform.position + new Vector3(0f, effectYOffset, 0f), effectPetalCount);
        }

        context.LogDebug($"Ice {sourceLabel} requested {petalsToAdd} petals, granted {gainedPetals} petals. Current petals={_shieldState.GetPetalCount()}.");
    }
}

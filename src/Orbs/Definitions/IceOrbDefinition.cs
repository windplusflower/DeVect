using DeVect.Combat;
using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class IceOrbDefinition : IOrbDefinition
{
    private const float PassiveShieldEffectYOffset = -0.98f;
    private const float EvocationShieldEffectYOffset = -0.96f;

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
        ApplyShieldGain(context, 1, 1, PassiveShieldEffectYOffset, "passive");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        ApplyShieldGain(context, 3, IceShieldState.PetalsPerShield, EvocationShieldEffectYOffset, "evocation");
    }

    private void ApplyShieldGain(OrbTriggerContext context, int petalsToAdd, int effectPetalCount, float effectYOffset, string sourceLabel)
    {
        int gainedPetals = _shieldState.AddShield(petalsToAdd);
        if (gainedPetals > 0)
        {
            context.Visuals.SpawnIcePetalEffect(context.Visuals.GetHeroChestEffectPosition(context.Hero, effectYOffset), effectPetalCount);
        }

        context.LogDebug($"Ice {sourceLabel} requested {petalsToAdd} petals, granted {gainedPetals} petals. Current petals={_shieldState.GetPetalCount()}.");
    }
}

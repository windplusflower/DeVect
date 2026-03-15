using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class BlackOrbDefinition : IOrbDefinition
{
    public OrbTypeId TypeId => OrbTypeId.Black;

    public string DisplayName => "Black";

    public Color OrbColor => new(0.2f, 0.09f, 0.28f, 1f);

    public void OnPassive(OrbTriggerContext context, OrbInstance instance)
    {
        int bonusDamage = Mathf.Max(1, context.NailDamage);
        instance.CurrentDamage += bonusDamage;
        context.LogDebug($"Black passive stored +{bonusDamage} damage. Total={instance.CurrentDamage}.");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        int damage = instance.CurrentDamage;
        if (damage <= 0)
        {
            context.LogDebug("Black evocation skipped because stored damage <= 0.");
            return;
        }

        HealthManager? target = context.Combat.TryPickLowestHpEnemyInRange(context.Hero);
        if (target == null)
        {
            context.LogDebug($"Black evocation found no target. Stored damage={damage}.");
            return;
        }

        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic))
        {
            context.LogDebug($"Black evocation failed to damage target {target.name}. Stored damage={damage}.");
            return;
        }

        context.Visuals.SpawnVoidImpactVisual(context.Combat.GetVoidImpactVisualPosition(target));
        context.LogDebug($"Black evocation hit target {target.name} for {damage}. Remaining hp={target.hp}.");
    }
}

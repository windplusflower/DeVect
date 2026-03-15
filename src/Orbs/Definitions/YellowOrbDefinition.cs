using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class YellowOrbDefinition : IOrbDefinition
{
    public OrbTypeId TypeId => OrbTypeId.Yellow;

    public string DisplayName => "Yellow";

    public Color OrbColor => new(1f, 0.85f, 0.15f, 1f);

    public int GetInitialDamage(OrbTriggerContext context)
    {
        return 0;
    }

    public void OnPassive(OrbTriggerContext context, OrbInstance instance)
    {
        HealthManager? target = context.Combat.TryPickRandomEnemyInRange(context.Hero);
        if (target == null)
        {
            return;
        }

        int damage = DeVect.Combat.OrbCombatService.GetCeilThirdDamage(context.NailDamage) + context.FocusBonus;
        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic))
        {
            return;
        }

        context.Visuals.SpawnLightningVisual(context.Combat.GetLightningImpactVisualPosition(target));
        context.LogDebug($"Yellow passive hit target {target.name} for {damage}.");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        HealthManager? target = context.Combat.TryPickRandomEnemyInRange(context.Hero);
        if (target == null)
        {
            return;
        }

        int damage = Mathf.Max(1, context.NailDamage) + context.FocusBonus;
        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic))
        {
            return;
        }

        context.Visuals.SpawnLightningVisual(context.Combat.GetLightningImpactVisualPosition(target));
        context.LogDebug($"Yellow evocation hit target {target.name} for {damage}.");
    }
}

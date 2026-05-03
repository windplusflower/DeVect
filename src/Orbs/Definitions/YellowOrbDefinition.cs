using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class YellowOrbDefinition : IOrbDefinition
{
    private const float PassiveScale = 1f / 3f;
    private const float EvocationScale = 2f / 3f;

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

        int damage = GetScaledDamage(context, PassiveScale);
        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic, bypassCustomHitCooldown: true))
        {
            return;
        }

        context.Visuals.SpawnLightningVisual(context.Combat.GetLightningImpactVisualPosition(target), false);
        context.LogDebug($"Yellow passive hit target {target.name} for {damage}.");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        HealthManager? target = context.Combat.TryPickRandomEnemyInRange(context.Hero);
        if (target == null)
        {
            return;
        }

        int damage = GetScaledDamage(context, EvocationScale);
        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic, bypassCustomHitCooldown: true))
        {
            return;
        }

        context.Visuals.SpawnLightningVisual(context.Combat.GetLightningImpactVisualPosition(target), true);
        context.LogDebug($"Yellow evocation hit target {target.name} for {damage}.");
    }

    private static int GetScaledDamage(OrbTriggerContext context, float scale)
    {
        int baseDamage = Mathf.Max(1, Mathf.CeilToInt(context.NailDamage * scale));
        return baseDamage + context.GetScaledShamanBonus(scale);
    }
}

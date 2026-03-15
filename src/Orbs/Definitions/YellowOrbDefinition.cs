using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class YellowOrbDefinition : IOrbDefinition
{
    private const float WhitePassiveScale = 0.25f;
    private const float BlackPassiveScale = 1f / 3f;
    private const float WhiteEvocationScale = 0.75f;
    private const float BlackEvocationScale = 1f;

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

        int damage = GetScaledDamage(context, GetPassiveScale(context));
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

        int damage = GetScaledDamage(context, GetEvocationScale(context));
        if (!context.Combat.TryDealOrbDamage(context.Hero, target, damage, AttackTypes.Generic))
        {
            return;
        }

        context.Visuals.SpawnLightningVisual(context.Combat.GetLightningImpactVisualPosition(target));
        context.LogDebug($"Yellow evocation hit target {target.name} for {damage}.");
    }

    private static float GetPassiveScale(OrbTriggerContext context)
    {
        return context.GetSpellLevel(OrbTypeId.Yellow) >= 2 ? BlackPassiveScale : WhitePassiveScale;
    }

    private static float GetEvocationScale(OrbTriggerContext context)
    {
        return context.GetSpellLevel(OrbTypeId.Yellow) >= 2 ? BlackEvocationScale : WhiteEvocationScale;
    }

    private static int GetScaledDamage(OrbTriggerContext context, float scale)
    {
        int baseDamage = Mathf.Max(1, Mathf.CeilToInt(context.NailDamage * scale));
        return baseDamage + context.GetScaledShamanBonus(scale);
    }
}

using System.Collections.Generic;
using DeVect.Orbs.Runtime;
using UnityEngine;

namespace DeVect.Orbs.Definitions;

internal sealed class WhiteOrbDefinition : IOrbDefinition
{
    public OrbTypeId TypeId => OrbTypeId.White;

    public string DisplayName => "White";

    public Color OrbColor => new(0.92f, 0.96f, 1f, 1f);

    public int GetInitialDamage(OrbTriggerContext context)
    {
        return Mathf.Max(1, DeVect.Combat.OrbCombatService.GetCeilThirdDamage(context.NailDamage) + context.FocusBonus);
    }

    public void OnPassive(OrbTriggerContext context, OrbInstance instance)
    {
        if (instance.CurrentDamage <= 0)
        {
            return;
        }

        List<HealthManager> targets = context.Combat.FindAllEnemiesInRange(context.Hero);
        int damage = instance.CurrentDamage;
        for (int i = 0; i < targets.Count; i++)
        {
            if (!context.Combat.TryDealOrbDamage(context.Hero, targets[i], damage, AttackTypes.Generic))
            {
                continue;
            }

            context.Visuals.SpawnGlassShatterVisual(context.Combat.GetWhiteImpactVisualPosition(targets[i]));
        }

        instance.CurrentDamage = Mathf.Max(0, instance.CurrentDamage - 1);
        if (instance.CurrentDamage <= 0)
        {
            instance.IsPendingRemoval = true;
        }

        context.LogDebug($"White passive hit {targets.Count} target(s) for {damage}. Remaining damage={instance.CurrentDamage}.");
    }

    public void OnEvocation(OrbTriggerContext context, OrbInstance instance)
    {
        int damage = instance.CurrentDamage * 2;
        if (damage <= 0)
        {
            return;
        }

        List<HealthManager> targets = context.Combat.FindAllEnemiesInRange(context.Hero);
        for (int i = 0; i < targets.Count; i++)
        {
            if (!context.Combat.TryDealOrbDamage(context.Hero, targets[i], damage, AttackTypes.Generic))
            {
                continue;
            }

            context.Visuals.SpawnGlassShatterVisual(context.Combat.GetWhiteImpactVisualPosition(targets[i]));
        }

        context.LogDebug($"White evocation hit {targets.Count} target(s) for {damage}.");
    }
}

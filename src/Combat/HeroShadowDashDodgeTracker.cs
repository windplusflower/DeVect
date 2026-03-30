using GlobalEnums;
using HutongGames.PlayMaker;
using UnityEngine;

namespace DeVect.Combat;

internal sealed class HeroShadowDashDodgeTracker
{
    private const int RequiredDashPhysicsSteps = 3;

    private bool _sessionActive;
    private bool _sessionConsumed;
    private int _dashPhysicsSteps;
    private int _dashStartFrame = -1;

    public void OnDashStarted(HeroController hero)
    {
        if (!CanStartSession(hero))
        {
            Reset();
            return;
        }

        _sessionActive = true;
        _sessionConsumed = false;
        _dashPhysicsSteps = 0;
        _dashStartFrame = Time.frameCount;
    }

    public void OnDashPhysicsStep(HeroController hero)
    {
        if (!_sessionActive)
        {
            return;
        }

        if (hero == null || hero != HeroController.instance)
        {
            Reset();
            return;
        }

        _dashPhysicsSteps++;
    }

    public bool TryDetectGhostDashDodge(HeroController hero, Collider2D otherCollider, out string? debugDetail)
    {
        debugDetail = null;

        if (!_sessionActive || _sessionConsumed || _dashPhysicsSteps != RequiredDashPhysicsSteps)
        {
            return false;
        }

        if (!IsHeroEligible(hero) || otherCollider == null)
        {
            if (hero == null || hero != HeroController.instance || !hero.cState.shadowDashing)
            {
                Reset();
            }

            return false;
        }

        if (!TryResolveDamageInfo(otherCollider, out int damageAmount, out int hazardType, out string sourceName, out string sourceDetail))
        {
            return false;
        }

        if (damageAmount <= 0 || hazardType != 1)
        {
            return false;
        }

        _sessionConsumed = true;
        debugDetail = $"source={sourceName}, {sourceDetail}, damageAmount={damageAmount}, hazardType={hazardType}, dashPhysicsSteps={_dashPhysicsSteps}, dashStartFrame={_dashStartFrame}, frame={Time.frameCount}";
        return true;
    }

    public void Reset()
    {
        _sessionActive = false;
        _sessionConsumed = false;
        _dashPhysicsSteps = 0;
        _dashStartFrame = -1;
    }

    private static bool CanStartSession(HeroController hero)
    {
        return hero != null && hero == HeroController.instance && hero.cState.shadowDashing;
    }

    private static bool IsHeroEligible(HeroController hero)
    {
        if (hero == null || hero != HeroController.instance)
        {
            return false;
        }

        if (!hero.cState.dashing || !hero.cState.shadowDashing)
        {
            return false;
        }

        if (hero.cState.invulnerable || hero.cState.recoiling || hero.cState.dead || hero.cState.hazardDeath)
        {
            return false;
        }

        if (hero.transitionState != HeroTransitionState.WAITING_TO_TRANSITION || hero.parryInvulnTimer > 0f)
        {
            return false;
        }

        PlayerData? playerData = hero.playerData ?? PlayerData.instance;
        return playerData == null || !playerData.GetBool("isInvincible");
    }

    private static bool TryResolveDamageInfo(
        Collider2D otherCollider,
        out int damageAmount,
        out int hazardType,
        out string sourceName,
        out string sourceDetail)
    {
        damageAmount = 0;
        hazardType = 0;
        sourceName = string.Empty;
        sourceDetail = string.Empty;

        DamageHero? damageHero = FindDamageHero(otherCollider);
        if (damageHero != null)
        {
            damageAmount = damageHero.damageDealt;
            hazardType = damageHero.hazardType;
            sourceName = nameof(DamageHero);
            sourceDetail = $"collider={otherCollider.name}, owner={damageHero.gameObject.name}, shadowDashHazard={damageHero.shadowDashHazard}";
            return true;
        }

        PlayMakerFSM? damageFsm = FindDamageFsm(otherCollider);
        if (damageFsm == null)
        {
            return false;
        }

        FsmInt? damageVar = damageFsm.FsmVariables.FindFsmInt("damageDealt");
        FsmInt? hazardVar = damageFsm.FsmVariables.FindFsmInt("hazardType");
        if (damageVar == null || hazardVar == null)
        {
            return false;
        }

        damageAmount = damageVar.Value;
        hazardType = hazardVar.Value;
        sourceName = "damages_hero";
        sourceDetail = $"collider={otherCollider.name}, owner={damageFsm.gameObject.name}, fsm={damageFsm.FsmName}";
        return true;
    }

    private static DamageHero? FindDamageHero(Collider2D otherCollider)
    {
        DamageHero? damageHero = otherCollider.GetComponent<DamageHero>();
        if (damageHero != null)
        {
            return damageHero;
        }

        return otherCollider.GetComponentInParent<DamageHero>();
    }

    private static PlayMakerFSM? FindDamageFsm(Collider2D otherCollider)
    {
        PlayMakerFSM[] localFsms = otherCollider.GetComponents<PlayMakerFSM>();
        for (int i = 0; i < localFsms.Length; i++)
        {
            if (IsDamageFsm(localFsms[i]))
            {
                return localFsms[i];
            }
        }

        PlayMakerFSM[] parentFsms = otherCollider.GetComponentsInParent<PlayMakerFSM>();
        for (int i = 0; i < parentFsms.Length; i++)
        {
            if (IsDamageFsm(parentFsms[i]))
            {
                return parentFsms[i];
            }
        }

        return null;
    }

    private static bool IsDamageFsm(PlayMakerFSM? fsm)
    {
        return fsm != null && fsm.FsmName == "damages_hero";
    }
}

using System;
using HutongGames.PlayMaker;

namespace DeVect.Fsm;

public sealed class SpellDetectAction : FsmStateAction
{
    public Action? OnNeutralSpellCast { get; set; }

    public Action? OnSmallSkillCast { get; set; }

    public Action? OnBigSkillCast { get; set; }

    public Func<bool>? ShouldHandleNeutralSpell { get; set; }

    public Func<bool>? ShouldHandleSmallSpell { get; set; }

    public Func<bool>? ShouldHandleBigSpell { get; set; }

    public override void OnEnter()
    {
        HeroActions? inputActions = InputHandler.Instance?.inputActions;
        bool upPressed = inputActions != null && inputActions.up.IsPressed;
        bool downPressed = inputActions != null && inputActions.down.IsPressed;

        if (upPressed)
        {
            if (ShouldHandleBigSpell?.Invoke() ?? false)
            {
                TryHandleSpellCast(OnBigSkillCast, 3);
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }

            Finish();
            return;
        }

        if (downPressed)
        {
            if (ShouldHandleSmallSpell?.Invoke() ?? false)
            {
                TryHandleSpellCast(OnSmallSkillCast, 1);
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }

            Finish();
            return;
        }

        if (ShouldHandleNeutralSpell?.Invoke() ?? false)
        {
            TryHandleSpellCast(OnNeutralSpellCast, 1);
            Fsm.Event("FSM CANCEL");
            Finish();
            return;
        }

        Finish();
    }

    private static void TryHandleSpellCast(Action? callback, int costMultiplier)
    {
        if (callback != null && HasEnoughSoul(costMultiplier))
        {
            callback.Invoke();
            ConsumeSpellCost(costMultiplier);
        }
    }

    private static bool HasEnoughSoul(int costMultiplier)
    {
        PlayerData? playerData = PlayerData.instance;
        if (playerData == null)
        {
            return false;
        }

        return Math.Max(0, playerData.GetInt("MPCharge")) >= GetSpellSoulCost(costMultiplier, playerData);
    }

    private static void ConsumeSpellCost(int costMultiplier)
    {
        HeroController? hero = HeroController.instance;
        PlayerData? playerData = PlayerData.instance;
        if (playerData == null || hero == null)
        {
            return;
        }

        int totalCost = GetSpellSoulCost(costMultiplier, playerData);
        if (Math.Max(0, playerData.GetInt("MPCharge")) < totalCost)
        {
            return;
        }

        hero.TakeMP(totalCost);
    }

    private static int GetSpellSoulCost(int costMultiplier, PlayerData playerData)
    {
        int singleCastCost = playerData.GetBool("equippedCharm_33") ? 24 : 33;
        return singleCastCost * Math.Max(1, costMultiplier);
    }
}

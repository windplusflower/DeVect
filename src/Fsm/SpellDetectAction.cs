using System;
using HutongGames.PlayMaker;
using UnityEngine;

namespace DeVect.Fsm;

public sealed class SpellDetectAction : FsmStateAction
{
    public Action? OnFireballCast { get; set; }

    public Action? OnDiveCast { get; set; }

    public Action? OnShriekCast { get; set; }

    public Func<bool>? ShouldConsumeFireballSpell { get; set; }

    public Func<bool>? ShouldConsumeDiveSpell { get; set; }

    public Func<bool>? ShouldConsumeShriekSpell { get; set; }

    public override void OnEnter()
    {
        float verticalInput = Input.GetAxisRaw("Vertical");
        if (verticalInput > 0.1f)
        {
            bool shouldConsumeShriek = ShouldConsumeShriekSpell?.Invoke() ?? false;
            if (shouldConsumeShriek)
            {
                OnShriekCast?.Invoke();
                ConsumeSpellCost();
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }

            Finish();
            return;
        }

        if (verticalInput < -0.1f)
        {
            bool shouldConsumeDive = ShouldConsumeDiveSpell?.Invoke() ?? false;
            if (shouldConsumeDive)
            {
                OnDiveCast?.Invoke();
                ConsumeSpellCost();
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }

            Finish();
            return;
        }

        if (verticalInput >= -0.1f)
        {
            bool shouldConsumeFireball = ShouldConsumeFireballSpell?.Invoke() ?? false;
            if (shouldConsumeFireball)
            {
                OnFireballCast?.Invoke();
                ConsumeSpellCost();
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }
        }

        Finish();
    }

    private static void ConsumeSpellCost()
    {
        PlayerData? playerData = PlayerData.instance;
        if (playerData == null)
        {
            return;
        }

        bool hasSpellTwister = playerData.GetBool("equippedCharm_33");
        playerData.TakeMP(hasSpellTwister ? 24 : 33);
        GameCameras.instance?.soulOrbFSM?.SendEvent("MP LOSE");
    }
}

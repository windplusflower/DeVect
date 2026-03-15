using System;
using HutongGames.PlayMaker;
using UnityEngine;

namespace DeVect.Fsm;

public sealed class FireballDetectAction : FsmStateAction
{
    public Action? OnFireballCast { get; set; }
    public Func<bool>? ShouldConsumeSpell { get; set; }

    public override void OnEnter()
    {
        float verticalInput = Input.GetAxisRaw("Vertical");
        if (verticalInput <= 0.1f && verticalInput >= -0.1f)
        {
            bool shouldConsume = ShouldConsumeSpell?.Invoke() ?? false;
            if (shouldConsume)
            {
                OnFireballCast?.Invoke();
                ConsumeFireballCost();
                Fsm.Event("FSM CANCEL");
                Finish();
                return;
            }

            OnFireballCast?.Invoke();
        }

        Finish();
    }

    private static void ConsumeFireballCost()
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

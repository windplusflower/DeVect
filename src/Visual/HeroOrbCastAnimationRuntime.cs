using UnityEngine;

namespace DeVect.Visual;

internal sealed class HeroOrbCastAnimationRuntime
{
    private const string ScreamStartClip = "Scream Start";
    private const string ScreamLoopClip = "Scream";
    private const string ScreamEndClip = "Scream End";

    private PlaybackStage _stage;
    private float _stageTimeRemaining;
    private HeroController? _activeHero;
    private HeroAnimationController? _activeAnimationController;
    private tk2dSpriteAnimator? _activeAnimator;

    public bool TryPlay(HeroController hero)
    {
        if (hero == null)
        {
            return false;
        }

        HeroAnimationController? animationController = hero.GetComponent<HeroAnimationController>();
        if (animationController == null || animationController.animator == null)
        {
            return false;
        }

        tk2dSpriteAnimator animator = animationController.animator;
        float screamStartDuration = GetClipDuration(animator, ScreamStartClip);
        float screamLoopDuration = GetClipDuration(animator, ScreamLoopClip);
        float screamEndDuration = GetClipDuration(animator, ScreamEndClip);
        if (screamStartDuration <= 0f || screamLoopDuration <= 0f || screamEndDuration <= 0f)
        {
            return false;
        }

        if (!ReferenceEquals(_activeHero, hero))
        {
            Cancel();
        }

        _activeHero = hero;
        _activeAnimationController = animationController;
        _activeAnimator = animator;

        _activeHero.StopAnimationControl();
        PlayStage(PlaybackStage.Start, screamStartDuration);
        return true;
    }

    public float GetReleaseDelay(HeroController hero)
    {
        if (hero == null)
        {
            return 0f;
        }

        HeroAnimationController? animationController = hero.GetComponent<HeroAnimationController>();
        if (animationController == null || animationController.animator == null)
        {
            return 0f;
        }

        float delay = GetClipDuration(animationController.animator, ScreamStartClip);
        return delay > 0f ? delay : 0f;
    }

    public void Tick(HeroController hero, float deltaTime)
    {
        if (_stage == PlaybackStage.None)
        {
            return;
        }

        if (hero == null || !ReferenceEquals(_activeHero, hero) || _activeAnimationController == null || _activeAnimator == null)
        {
            Cancel();
            return;
        }

        _stageTimeRemaining -= Mathf.Max(0f, deltaTime);
        if (_stageTimeRemaining > 0f)
        {
            return;
        }

        switch (_stage)
        {
            case PlaybackStage.Start:
                PlayStage(PlaybackStage.Loop, GetClipDuration(_activeAnimator, ScreamLoopClip));
                break;
            case PlaybackStage.Loop:
                PlayStage(PlaybackStage.End, GetClipDuration(_activeAnimator, ScreamEndClip));
                break;
            default:
                Cancel();
                break;
        }
    }

    public void Cancel()
    {
        HeroController? hero = _activeHero;
        _stage = PlaybackStage.None;
        _stageTimeRemaining = 0f;
        _activeHero = null;
        _activeAnimationController = null;
        _activeAnimator = null;

        if (hero != null)
        {
            hero.StartAnimationControl();
        }
    }

    private void PlayStage(PlaybackStage stage, float duration)
    {
        if (_activeAnimator == null)
        {
            Cancel();
            return;
        }

        string clipName = stage switch
        {
            PlaybackStage.Start => ScreamStartClip,
            PlaybackStage.Loop => ScreamLoopClip,
            PlaybackStage.End => ScreamEndClip,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(clipName) || duration <= 0f)
        {
            Cancel();
            return;
        }

        _stage = stage;
        _stageTimeRemaining = duration;
        _activeAnimator.Play(clipName);
    }

    private static float GetClipDuration(tk2dSpriteAnimator animator, string clipName)
    {
        if (animator == null)
        {
            return -1f;
        }

        tk2dSpriteAnimationClip clip = animator.GetClipByName(clipName);
        if (clip == null || clip.frames == null || clip.frames.Length == 0 || clip.fps <= 0f)
        {
            return -1f;
        }

        return clip.frames.Length / clip.fps;
    }

    private enum PlaybackStage
    {
        None = 0,
        Start = 1,
        Loop = 2,
        End = 3
    }
}

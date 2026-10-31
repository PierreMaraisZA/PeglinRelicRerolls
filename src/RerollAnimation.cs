using DG.Tweening;
using PeglinUI;
using UI;
using UnityEngine;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// The "card flip" when relics are rerolled: each new relic scales in from edge-on, one after another.
    /// Purely visual: the relic is already swapped before the flip starts, so clicks during the animation
    /// always act on the new relic.
    /// </summary>
    internal static class RerollAnimation
    {
        private const float FlipSeconds = 0.22f;
        private const float StaggerSeconds = 0.06f;

        /// <param name="target">The card (or image) to flip.</param>
        /// <param name="order">Position in the row, used to stagger the flips left to right.</param>
        public static void FlipIn(Transform target, int order)
        {
            if (target == null)
            {
                return;
            }
            // Finish any flip still running from a quick previous reroll, so we read the card's resting size.
            target.DOKill(complete: true);
            Vector3 resting = target.localScale;
            target.localScale = new Vector3(0f, resting.y, resting.z);
            target.DOScaleX(resting.x, FlipSeconds)
                .SetDelay(order * StaggerSeconds)
                .SetEase(Ease.OutBack)
                // Real time: relic screens can run with the game paused.
                .SetUpdate(isIndependentUpdate: true);
        }

        /// <summary>Plays one of the game's own UI sounds, so it follows the player's volume settings.</summary>
        public static void PlaySound()
        {
            UIButtonClickEventDispatcher.OnUIButtonClicked?.Invoke(ButtonSfx.Open);
        }
    }
}

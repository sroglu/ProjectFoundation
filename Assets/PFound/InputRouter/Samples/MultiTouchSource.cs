using System.Collections.Generic;
using UnityEngine;

namespace PFound.InputRouter.Samples
{
    /// <summary>
    /// Sample multi-input intent: one screen-space touch point. Register with
    /// <c>router.RegisterMulti&lt;TouchPointIntent&gt;(source, IntentGroup.Gameplay)</c> and subscribe
    /// with <c>SubscribeMulti&lt;TouchPointIntent&gt;(phase, ctx =&gt; ...)</c>. Each finger/player then
    /// runs its OWN Started/Held/Ended lifecycle, keyed by <see cref="InputId"/> — so N simultaneous
    /// inputs (e.g. sumo-bumo's two players tapping at once) all flow independently.
    /// </summary>
    public struct TouchPointIntent : IIntent
    {
        public Vector2 Position;

        public TouchPointIntent(Vector2 position)
        {
            Position = position;
        }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    /// <summary>
    /// Legacy Input Manager multi-touch source: one reading per active finger, keyed by
    /// <c>fingerId</c>. A finger left out of the buffer (Ended/Canceled) is released by the channel.
    /// Lives in the sample (caller) assembly — the router core never references UnityEngine.
    /// </summary>
    public sealed class LegacyMultiTouchSource : IMultiIntentSource<TouchPointIntent>
    {
        public void Read(List<KeyedIntentReading<TouchPointIntent>> active)
        {
            int count = Input.touchCount;
            for (int i = 0; i < count; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    continue; // omit → the channel resolves Ended for this fingerId
                }

                active.Add(new KeyedIntentReading<TouchPointIntent>(
                    new InputId(touch.fingerId),
                    new TouchPointIntent(touch.position)));
            }
        }
    }
#endif

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// New Input System multi-touch source over EnhancedTouch, keyed by <c>touchId</c>. Call
    /// <c>UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable()</c> once at startup so
    /// <c>Touch.activeTouches</c> is populated.
    /// </summary>
    public sealed class InputSystemMultiTouchSource : IMultiIntentSource<TouchPointIntent>
    {
        public void Read(List<KeyedIntentReading<TouchPointIntent>> active)
        {
            var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var touch = touches[i];
                active.Add(new KeyedIntentReading<TouchPointIntent>(
                    new InputId(touch.touchId),
                    new TouchPointIntent(touch.screenPosition)));
            }
        }
    }
#endif
}

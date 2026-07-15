#if ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;

namespace PFound.InputRouter.Samples
{
    /// <summary>
    /// Example adapter reading the legacy Input Manager. Emits <see cref="MoveIntent"/> from the
    /// "Horizontal"/"Vertical" axes; active while either axis is meaningfully off-centre. Lives
    /// in the sample (caller) assembly — the router core never references UnityEngine.
    /// Guarded by ENABLE_LEGACY_INPUT_MANAGER so it compiles out when the old backend is disabled.
    /// </summary>
    public sealed class LegacyMoveSource : IIntentSource<MoveIntent>
    {
        private const float DeadZone = 0.001f;

        private readonly string _horizontalAxis;
        private readonly string _verticalAxis;

        public LegacyMoveSource(string horizontalAxis = "Horizontal", string verticalAxis = "Vertical")
        {
            _horizontalAxis = horizontalAxis;
            _verticalAxis = verticalAxis;
        }

        public IntentReading<MoveIntent> Read()
        {
            float h = Input.GetAxisRaw(_horizontalAxis);
            float v = Input.GetAxisRaw(_verticalAxis);
            bool active = Mathf.Abs(h) > DeadZone || Mathf.Abs(v) > DeadZone;
            return active
                ? IntentReading<MoveIntent>.Active(new MoveIntent(h, v))
                : IntentReading<MoveIntent>.Inactive;
        }
    }
}
#endif

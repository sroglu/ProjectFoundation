#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace PFound.InputRouter.Samples
{
    /// <summary>
    /// Example adapter reading the Input System package. Emits the same <see cref="MoveIntent"/>
    /// as <c>LegacyMoveSource</c>, but from a Vector2-valued <see cref="InputAction"/> (e.g. a
    /// WASD / stick composite). Active while the read vector is off-centre. Guarded by
    /// ENABLE_INPUT_SYSTEM (auto-defined when com.unity.inputsystem is present); the asmdef also
    /// maps the package to PFOUND_INPUTSYSTEM for explicit version gating.
    /// </summary>
    public sealed class InputSystemMoveSource : IIntentSource<MoveIntent>
    {
        private const float DeadZoneSqr = 0.0001f;

        private readonly InputAction _moveAction;

        public InputSystemMoveSource(InputAction moveAction)
        {
            _moveAction = moveAction;
            if (_moveAction != null && !_moveAction.enabled)
            {
                _moveAction.Enable();
            }
        }

        public IntentReading<MoveIntent> Read()
        {
            if (_moveAction == null)
            {
                return IntentReading<MoveIntent>.Inactive;
            }

            Vector2 value = _moveAction.ReadValue<Vector2>();
            bool active = value.sqrMagnitude > DeadZoneSqr;
            return active
                ? IntentReading<MoveIntent>.Active(new MoveIntent(value.x, value.y))
                : IntentReading<MoveIntent>.Inactive;
        }
    }
}
#endif

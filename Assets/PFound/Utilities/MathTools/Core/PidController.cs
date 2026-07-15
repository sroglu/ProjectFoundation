using System;

namespace PFound.Utilities.MathTools
{
    /// <summary>
    /// A classic proportional-integral-derivative controller. Holds the accumulated
    /// integral and the previous error between calls, so it is stateful: create one
    /// instance per controlled signal and call <see cref="Update"/> every tick with the
    /// current error and elapsed time.
    /// </summary>
    public sealed class PidController
    {
        public float ProportionalGain;
        public float IntegralGain;
        public float DerivativeGain;

        /// <summary>Optional symmetric clamp on the accumulated integral term to curb wind-up. Zero disables it.</summary>
        public float IntegralClamp;

        private float _integral;
        private float _previousError;
        private bool _hasPreviousError;

        public PidController(float proportionalGain, float integralGain, float derivativeGain, float integralClamp = 0f)
        {
            ProportionalGain = proportionalGain;
            IntegralGain = integralGain;
            DerivativeGain = derivativeGain;
            IntegralClamp = integralClamp;
        }

        /// <summary>Current accumulated integral term (exposed for inspection/serialization).</summary>
        public float Integral => _integral;

        /// <summary>
        /// Advances the controller by <paramref name="deltaTime"/> seconds and returns the
        /// control output for the supplied <paramref name="error"/> (setpoint - measurement).
        /// </summary>
        public float Update(float error, float deltaTime)
        {
            _integral += error * deltaTime;
            if (IntegralClamp > 0f)
            {
                _integral = Math.Min(IntegralClamp, Math.Max(-IntegralClamp, _integral));
            }

            float derivative = 0f;
            if (_hasPreviousError && deltaTime > 0f)
            {
                derivative = (error - _previousError) / deltaTime;
            }

            _previousError = error;
            _hasPreviousError = true;

            return ProportionalGain * error
                   + IntegralGain * _integral
                   + DerivativeGain * derivative;
        }

        /// <summary>Clears the integral accumulator and derivative history.</summary>
        public void Reset()
        {
            _integral = 0f;
            _previousError = 0f;
            _hasPreviousError = false;
        }
    }
}

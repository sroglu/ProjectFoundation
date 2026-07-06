using UnityEngine;
using PFound.ECS;

namespace PFound.ECS.Samples.ParticleModule
{
    /// <summary>ECS-driven emission state for a bound ParticleSystem.</summary>
    public struct EmitterControl
    {
        public float RateOverTime;
        public bool Enabled;
    }

    /// <summary>Managed binding to the ParticleSystem this entity controls.</summary>
    public struct ParticleBinding
    {
        public ParticleSystem System;
    }

    /// <summary>Applies each entity's emission state onto its bound ParticleSystem.</summary>
    [ECSSystem(ECSPhase.OnRender)]
    public sealed class ParticleControlSystem : SystemBase
    {
        protected override void Execute()
        {
            World.Query<EmitterControl, ParticleBinding>().ForEach((World w, Entity e, ref EmitterControl ctrl, ref ParticleBinding bind) =>
            {
                if (bind.System == null) return;
                var emission = bind.System.emission; // module struct: writes apply to the system
                emission.enabled = ctrl.Enabled;
                emission.rateOverTime = ctrl.RateOverTime;
            });
        }
    }
}

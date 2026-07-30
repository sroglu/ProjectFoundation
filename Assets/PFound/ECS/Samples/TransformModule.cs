using UnityEngine;
using PFound.ECS;

namespace PFound.ECS.Samples.TransformModule
{
    /// <summary>ECS-side transform state, synced onto a bound <see cref="UnityEngine.Transform"/>.</summary>
    public struct LocalTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    /// <summary>Managed binding from an entity to the GameObject transform it drives.</summary>
    public struct TransformBinding
    {
        public Transform Target;
    }

    /// <summary>Constant angular velocity, degrees/second per axis.</summary>
    public struct Spin
    {
        public Vector3 DegreesPerSecond;
    }

    /// <summary>Integrates spinning entities' rotation each frame (scaled time).</summary>
    [ECSSystem(ECSPhase.OnUpdate)]
    public sealed class SpinSystem : SystemBase
    {
        protected override void Execute()
        {
            World.Query<LocalTransform, Spin>().ForEach((World w, Entity e, ref LocalTransform lt, ref Spin spin) =>
            {
                lt.Rotation = Quaternion.Euler(spin.DegreesPerSecond * w.DeltaTime) * lt.Rotation;
            });
        }
    }

    /// <summary>Pushes ECS transform state onto the bound GameObject transform.</summary>
    [ECSSystem(ECSPhase.PostUpdate)]
    public sealed class TransformSyncSystem : SystemBase
    {
        protected override void Execute()
        {
            World.Query<LocalTransform, TransformBinding>().ForEach((World w, Entity e, ref LocalTransform lt, ref TransformBinding bind) =>
            {
                if (bind.Target == null) return;
                bind.Target.SetPositionAndRotation(lt.Position, lt.Rotation);
                bind.Target.localScale = lt.Scale;
            });
        }
    }
}

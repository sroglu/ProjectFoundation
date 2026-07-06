using UnityEngine;
using PFound.ECS;
using PFound.ECS.Samples.TransformModule;

namespace PFound.ECS.Samples.CameraModule
{
    /// <summary>Makes a camera trail an entity's <see cref="LocalTransform"/> at an offset.</summary>
    public struct CameraFollow
    {
        public Entity Target;
        public Vector3 Offset;
        public float Smooth; // approach rate per second; <= 0 snaps
    }

    /// <summary>Managed binding to the camera transform this rig drives.</summary>
    public struct CameraBinding
    {
        public Transform Camera;
    }

    /// <summary>Eases the bound camera toward its target + offset, then looks at the target.</summary>
    [ECSSystem(ECSPhase.PostUpdate)]
    [DependsOn(typeof(TransformSyncSystem))]
    public sealed class CameraFollowSystem : SystemBase
    {
        protected override void Execute()
        {
            World.Query<CameraFollow, CameraBinding>().ForEach((World w, Entity e, ref CameraFollow follow, ref CameraBinding bind) =>
            {
                if (bind.Camera == null) return;
                if (!w.TryGet<LocalTransform>(follow.Target, out var target)) return;

                Vector3 desired = target.Position + follow.Offset;
                float t = follow.Smooth <= 0f ? 1f : Mathf.Clamp01(follow.Smooth * w.DeltaTime);
                bind.Camera.position = Vector3.Lerp(bind.Camera.position, desired, t);
                bind.Camera.LookAt(target.Position);
            });
        }
    }
}

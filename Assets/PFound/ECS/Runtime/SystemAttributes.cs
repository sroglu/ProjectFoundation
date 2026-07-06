using System;

namespace PFound.ECS
{
    /// <summary>When in the frame a system runs. Systems execute in this declaration order.</summary>
    public enum ECSPhase
    {
        PreUpdate,
        OnUpdate,
        PostUpdate,
        PreRender,
        OnRender,
        PostRender,
        PostFrame
    }

    /// <summary>Which clock a system's <c>DeltaTime</c> reflects.</summary>
    public enum UpdateTime
    {
        Scaled,
        Unscaled
    }

    /// <summary>
    /// Declares a system's frame phase and time mode. Optional — a system with no attribute
    /// defaults to <see cref="ECSPhase.OnUpdate"/> on the scaled clock.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ECSSystemAttribute : Attribute
    {
        public ECSPhase Phase { get; }
        public UpdateTime Time { get; }

        public ECSSystemAttribute(ECSPhase phase = ECSPhase.OnUpdate, UpdateTime time = UpdateTime.Scaled)
        {
            Phase = phase;
            Time = time;
        }
    }

    /// <summary>
    /// Orders a system after the named systems within the same phase (topological sort).
    /// Dependencies in other phases are already satisfied by phase order.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DependsOnAttribute : Attribute
    {
        public Type[] Systems { get; }
        public DependsOnAttribute(params Type[] systems) { Systems = systems ?? Array.Empty<Type>(); }
    }

    /// <summary>
    /// Restricts a system to run only while one of the named scenes is active (see
    /// <see cref="World.SceneChanged"/>). Absent ⇒ the system runs in every scene.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ActiveInSceneAttribute : Attribute
    {
        public string[] Scenes { get; }
        public ActiveInSceneAttribute(params string[] scenes) { Scenes = scenes ?? Array.Empty<string>(); }
    }

    /// <summary>
    /// Marks a system to be kept by IL2CPP / managed-code stripping. No-op at runtime; mirrors
    /// <c>UnityEngine.Scripting.PreserveAttribute</c> so the pure-core compiles without Unity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor)]
    public sealed class PreserveAttribute : Attribute { }
}

using System;

namespace PFound.ECS
{
    /// <summary>
    /// Base class for ECS systems. Override <see cref="Execute"/> (required) and optionally
    /// <see cref="OnInitialize"/> / <see cref="CreateQueries"/>. Frame phase, time mode, scene
    /// scope and ordering come from <see cref="ECSSystemAttribute"/>, <see cref="DependsOnAttribute"/>
    /// and <see cref="ActiveInSceneAttribute"/>, read once when the world attaches the system.
    /// </summary>
    public abstract class SystemBase
    {
        /// <summary>The world this system is registered to. Set on attach, before lifecycle hooks.</summary>
        protected World World { get; private set; }

        internal ECSPhase Phase { get; private set; }
        internal UpdateTime Time { get; private set; }
        internal Type[] Dependencies { get; private set; } = Array.Empty<Type>();
        internal string[] ActiveScenes { get; private set; } // null ⇒ active in every scene

        /// <summary>Delta time on this system's clock (scaled or unscaled per its attribute).</summary>
        protected float DeltaTime => Time == UpdateTime.Unscaled ? World.UnscaledDeltaTime : World.DeltaTime;

        internal void Attach(World world)
        {
            World = world;
            ReadMetadata();
            OnInitialize();
            CreateQueries();
        }

        internal void Run() => Execute();

        internal bool IsActiveIn(string scene) =>
            ActiveScenes == null || Array.IndexOf(ActiveScenes, scene) >= 0;

        private void ReadMetadata()
        {
            var type = GetType();

            var sys = (ECSSystemAttribute)Attribute.GetCustomAttribute(type, typeof(ECSSystemAttribute));
            Phase = sys?.Phase ?? ECSPhase.OnUpdate;
            Time = sys?.Time ?? UpdateTime.Scaled;

            var dep = (DependsOnAttribute)Attribute.GetCustomAttribute(type, typeof(DependsOnAttribute));
            Dependencies = dep?.Systems ?? Array.Empty<Type>();

            var scene = (ActiveInSceneAttribute)Attribute.GetCustomAttribute(type, typeof(ActiveInSceneAttribute));
            ActiveScenes = scene?.Scenes; // null when unscoped ⇒ always active
        }

        /// <summary>Called once when registered, after <see cref="World"/> is set. Optional.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called once after <see cref="OnInitialize"/>; build &amp; cache QueryIds here.</summary>
        protected virtual void CreateQueries() { }

        /// <summary>Per-tick work. Called by <see cref="World.Update"/> / <see cref="World.RunPhase"/>.</summary>
        protected abstract void Execute();
    }
}

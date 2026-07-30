using System;
using System.Collections.Generic;

namespace PFound.ECS
{
    /// <summary>
    /// Base class for ECS systems. Override <see cref="Execute"/> (required) and optionally
    /// <see cref="OnInitialize"/> / <see cref="CreateQueries"/> / <see cref="OnDispose"/>. Frame
    /// phase, time mode, scene scope and ordering come from <see cref="ECSSystemAttribute"/>,
    /// <see cref="DependsOnAttribute"/> and <see cref="ActiveInSceneAttribute"/>, read once when the
    /// world attaches the system.
    ///
    /// Each system owns a deferred <see cref="CommandBuffer"/> that is played back automatically
    /// after <see cref="Execute"/>, and lifecycle-managed event subscriptions (<see cref="Subscribe{T}"/>)
    /// that are auto-unsubscribed on teardown.
    /// </summary>
    public abstract class SystemBase
    {
        /// <summary>The world this system is registered to. Set on attach, before lifecycle hooks.</summary>
        protected World World { get; private set; }

        /// <summary>
        /// Per-system deferred command buffer. Record structural changes during <see cref="Execute"/>;
        /// the world plays it back automatically right after <see cref="Execute"/> returns.
        /// </summary>
        protected CommandBuffer CommandBuffer { get; private set; }

        internal ECSPhase Phase { get; private set; }
        internal UpdateTime Time { get; private set; }
        internal Type[] Dependencies { get; private set; } = Array.Empty<Type>();
        internal string[] ActiveScenes { get; private set; } // null ⇒ active in every scene

        // Lifecycle-managed event subscriptions; disposed on teardown so handlers never outlive the system.
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        /// <summary>Delta time on this system's clock (scaled or unscaled per its attribute).</summary>
        protected float DeltaTime => Time == UpdateTime.Unscaled ? World.UnscaledDeltaTime : World.DeltaTime;

        internal void Attach(World world)
        {
            World = world;
            ReadMetadata();
            CommandBuffer = world.CreateCommandBuffer();
            OnInitialize();
            CreateQueries();
        }

        /// <summary>Re-runs the init hooks after a scene reload (world already set); fresh command buffer.</summary>
        internal void Reinitialize()
        {
            CommandBuffer = World.CreateCommandBuffer();
            OnInitialize();
            CreateQueries();
        }

        internal void Run()
        {
            Execute();
            if (CommandBuffer.Count > 0) CommandBuffer.Playback();
        }

        internal bool IsActiveIn(string scene) =>
            ActiveScenes == null || Array.IndexOf(ActiveScenes, scene) >= 0;

        /// <summary>
        /// Tears the system down: drops managed subscriptions, then runs <see cref="OnDispose"/>.
        /// Called automatically by <see cref="World.Dispose"/> / <see cref="World.ReloadScene"/>;
        /// safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++) _subscriptions[i].Dispose();
            _subscriptions.Clear();
            OnDispose();
        }

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

        /// <summary>Subscribes to an event for this system's lifetime; auto-unsubscribed on teardown.</summary>
        protected void Subscribe<T>(Action<T> handler) where T : struct
            => _subscriptions.Add(World.Events.Subscribe(handler));

        /// <summary>Subscribes a batch (Span) event handler for this system's lifetime.</summary>
        protected void SubscribeBatch<T>(SpanAction<T> handler) where T : struct
            => _subscriptions.Add(World.Events.SubscribeBatch(handler));

        /// <summary>Called once when registered, after <see cref="World"/> is set. Optional.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called once after <see cref="OnInitialize"/>; build &amp; cache QueryIds here.</summary>
        protected virtual void CreateQueries() { }

        /// <summary>Called on teardown (World.Dispose / scene reload), after subscriptions drop. Optional.</summary>
        protected virtual void OnDispose() { }

        /// <summary>Per-tick work. Called by <see cref="World.Update"/> / <see cref="World.RunPhase"/>.</summary>
        protected abstract void Execute();
    }
}

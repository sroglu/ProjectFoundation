using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using PFound.LoopScheduler;

namespace PFound.Render.ShaderWarmup
{
    /// <summary>
    /// Concrete <see cref="IShaderWarmupController"/>. Owns the per-frame Tick callback registered
    /// with <c>PFound.LoopScheduler</c> BeforeRender, the live list of active sessions, and the
    /// captured original value of <c>GraphicsSettings.logWhenShaderIsCompiled</c>.
    /// </summary>
    public sealed class ShaderWarmupController : IShaderWarmupController
    {
        private readonly List<WarmupSession> _activeSessions = new List<WarmupSession>(4);
        private readonly Action _tickCallback;
        private readonly bool _originalLogWhenShaderIsCompiled;
        private GameObject _gameLoopOwner;
        private bool _diagnosticOn;
        private bool _disposed;

        /// <summary>
        /// Constructs the controller. Captures the current <c>logWhenShaderIsCompiled</c> value so
        /// <see cref="Dispose"/> can restore it. Does NOT create the GameLoop owner GameObject
        /// yet — that happens lazily on the first <see cref="BeginSession(WarmupBatch[])"/> call.
        /// </summary>
        public ShaderWarmupController()
        {
            _originalLogWhenShaderIsCompiled = GraphicsSettings.logWhenShaderIsCompiled;
            _tickCallback = Tick;
        }

        /// <inheritdoc />
        public IShaderWarmupSession BeginSession(params WarmupBatch[] batches)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderWarmupController));
            if (batches == null) throw new ArgumentNullException(nameof(batches));

            // Defensive copy; the controller owns the array lifetime hereafter.
            var copy = new WarmupBatch[batches.Length];
            for (int i = 0; i < batches.Length; i++)
            {
                // WarmupBatch constructor already eagerly validates; re-validate here in case
                // the caller produced default(WarmupBatch) (Collection == null).
                if (batches[i].Collection == null)
                {
                    throw new ArgumentNullException(nameof(batches),
                        $"Batches[{i}].Collection is null. Construct each WarmupBatch via the public ctor for eager validation.");
                }
                copy[i] = batches[i];
            }

            var session = new WarmupSession(copy);
            _activeSessions.Add(session);
            EnsureOwnerAndRegister();
            return session;
        }

        /// <inheritdoc />
        public IShaderWarmupSession BeginSession(IEnumerable<WarmupBatch> batches)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderWarmupController));
            if (batches == null) throw new ArgumentNullException(nameof(batches));

            // Materialize once to count + copy.
            if (batches is WarmupBatch[] arr) return BeginSession(arr);
            if (batches is IReadOnlyCollection<WarmupBatch> roc)
            {
                var tmp = new WarmupBatch[roc.Count];
                int idx = 0;
                foreach (var b in batches) tmp[idx++] = b;
                return BeginSession(tmp);
            }
            // Fallback path: enumerate twice (rare; not perf-critical for BeginSession).
            int count = 0;
            foreach (var _ in batches) count++;
            var buffer = new WarmupBatch[count];
            int j = 0;
            foreach (var b in batches) buffer[j++] = b;
            return BeginSession(buffer);
        }

        /// <inheritdoc />
        public bool DiagnosticMode
        {
            get => _diagnosticOn;
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ShaderWarmupController));
                _diagnosticOn = value;
                GraphicsSettings.logWhenShaderIsCompiled = value;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IShaderWarmupSession> ActiveSessions => _activeSessions;

        /// <summary>
        /// Per-frame tick (BeforeRender). Iterates active sessions, advances each, and swap-and-pops
        /// completed sessions. Public so tests can drive it without GameLoop.
        /// </summary>
        public void Tick()
        {
            if (_disposed) return;
            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var session = _activeSessions[i];
                if (session.IsComplete)
                {
                    // swap-and-pop
                    int last = _activeSessions.Count - 1;
                    _activeSessions[i] = _activeSessions[last];
                    _activeSessions.RemoveAt(last);
                    continue;
                }
                session.Advance();
            }
        }

        private void EnsureOwnerAndRegister()
        {
            if (_gameLoopOwner != null) return;
            _gameLoopOwner = new GameObject("[ShaderWarmupController.Owner]") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_gameLoopOwner);
            }
            PFound.LoopScheduler.LoopScheduler.RegisterBeforeRenderLoop(_tickCallback, _gameLoopOwner);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Cancel + dispose all sessions.
            for (int i = 0; i < _activeSessions.Count; i++) _activeSessions[i].Dispose();
            _activeSessions.Clear();

            // Unregister GameLoop + destroy owner.
            if (_gameLoopOwner != null)
            {
                PFound.LoopScheduler.LoopScheduler.DeregisterBeforeRenderLoop(_tickCallback);
                if (Application.isPlaying) UnityEngine.Object.Destroy(_gameLoopOwner);
                else UnityEngine.Object.DestroyImmediate(_gameLoopOwner);
                _gameLoopOwner = null;
            }

            // Restore captured original.
            GraphicsSettings.logWhenShaderIsCompiled = _originalLogWhenShaderIsCompiled;
        }
    }
}

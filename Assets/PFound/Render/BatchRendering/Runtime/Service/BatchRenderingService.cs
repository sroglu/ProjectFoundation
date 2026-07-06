using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Pure-C# implementation of <see cref="IBatchRenderingService"/>. Construct directly
    /// (<c>new BatchRenderingService()</c>) — there is no static <c>Instance</c> and no required
    /// <c>PFound.DependencyContainer</c> dependency.
    /// </summary>
    /// <remarks>
    /// The constructor allocates a hidden <c>DontDestroyOnLoad</c> owner GameObject and subscribes
    /// to <c>PFound.LoopScheduler.LoopScheduler.RegisterBeforeRenderLoop</c>. Per active camera per frame
    /// the service runs cull + dispatch for each registered batch.
    /// <para>
    /// <b>Owner-managed lifecycle.</b> Per CODING-STYLE.md §8, the consumer who calls
    /// <see cref="RegisterBatch"/> is solely responsible for calling <see cref="IBatchHandle.Dispose"/>
    /// at the matching close hook. The service does NOT subscribe to <c>SceneManager</c> events,
    /// does NOT auto-clear, does NOT track scene-of-origin.
    /// </para>
    /// </remarks>
    public sealed class BatchRenderingService : IBatchRenderingService
    {
        // Owner GO + lifecycle plumbing.
        private GameObject _ownerGameObject;
        private bool _disposed;

        // Per-batch registry. List + re-entrance pattern mirrors AnchorResizeWatcher.
        private readonly List<BatchHandle> _handles = new();
        private readonly List<BatchHandle> _pendingRemove = new();
        private bool _ticking;

        // Cull + visibility infrastructure.
        private readonly VisibilityBuffer _visibilityBuffer;
        // Scratch list used as the intermediate buffer when both frustum + distance are enabled
        // (frustum writes here, distance reads here and writes the final result to _visibilityBuffer).
        // Allocated lazily when the first distance-enabled batch is registered.
        private NativeList<int> _distanceStageScratch;
        private bool _distanceStageScratchCreated;
        private readonly Plane[] _frustumPlanesScratch = new Plane[6];

        // Service-level one-shot diagnostics (FR-025). Per-batch gating uses a fresh OneShotGate
        // per handle; service-wide diagnostics (currently none in Phase 2 shell) would use this gate.
        private readonly OneShotGate _serviceDiagnostics = new OneShotGate();
        private readonly Guid _serviceId = Guid.NewGuid();

        // Test diagnostics — exposed via internal accessors for the EditMode tests.
        internal int LiveHandleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _handles.Count; i++)
                    if (_handles[i].IsAlive) n++;
                return n;
            }
        }

        internal Guid ServiceId => _serviceId;
        internal OneShotGate ServiceDiagnostics => _serviceDiagnostics;
        internal VisibilityBuffer VisibilityBuffer => _visibilityBuffer;

        public BatchRenderingService()
        {
            _ownerGameObject = new GameObject("[BatchRenderingService]")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_ownerGameObject);

            _visibilityBuffer = new VisibilityBuffer();

            PFound.LoopScheduler.LoopScheduler.RegisterBeforeRenderLoop(OnBeforeRender, _ownerGameObject);
        }

        // ---------------- IBatchRenderingService ----------------

        public IBatchHandle RegisterBatch(BatchRenderingBatch batch)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BatchRenderingService));

            // FR-026: null-arg guards.
            if (batch.mesh == null) throw new ArgumentNullException(nameof(batch) + "." + nameof(batch.mesh));
            if (batch.material == null) throw new ArgumentNullException(nameof(batch) + "." + nameof(batch.material));
            if (batch.source == null) throw new ArgumentNullException(nameof(batch) + "." + nameof(batch.source));

            // Range check for subMeshIndex.
            int subMeshCount = batch.mesh.subMeshCount;
            if (batch.subMeshIndex < 0 || batch.subMeshIndex >= subMeshCount)
                throw new ArgumentOutOfRangeException(
                    nameof(batch) + "." + nameof(batch.subMeshIndex),
                    batch.subMeshIndex,
                    $"subMeshIndex must be in [0, {subMeshCount}); mesh has {subMeshCount} sub-mesh(es).");

            // Build the handle. Per-batch one-shot gate is fresh per handle.
            var id = Guid.NewGuid();
            var gate = new OneShotGate();

            // Backend selection.
            //   - Classic     → ClassicBackendState (US1 / T033)
            //   - Indirect    → IndirectBackendState (US2 / T043) — only when platform supports
            //                   compute + indirect args; otherwise degraded and routed to no-op
            //   - Procedural  → NullBackendState (real lands at T063 / T064)
            // Capability-based degradation (next block below) decides whether Indirect / Procedural
            // are unsupported before the backend state is instantiated, so we instantiate the
            // expensive states (which allocate GraphicsBuffers) only when the platform supports them.
            bool isIndirectFamily = batch.backend == BackendKind.Indirect || batch.backend == BackendKind.Procedural;
            bool indirectDegraded = isIndirectFamily && !BackendCapabilityProbe.SupportsIndirect;
            IBackendState backendState = indirectDegraded
                ? (IBackendState)new NullBackendState()
                : batch.backend switch
                {
                    BackendKind.Classic => new ClassicBackendState(),
                    BackendKind.Indirect => new IndirectBackendState(batch.mesh, batch.subMeshIndex),
                    BackendKind.Procedural => new ProceduralBackendState(),
                    _ => (IBackendState)new NullBackendState(),
                };
            var handle = new BatchHandle(id, batch, backendState, gate, this);

            // Register-time degradation detection — limited to what we can decide without backends:
            // 1. Indirect / Procedural on an unsupported platform (FR-023a). Fires regardless of
            //    whether the backend implementation has landed yet so consumers writing code against
            //    the shell get the correct degraded behavior.
            if (batch.backend == BackendKind.Indirect || batch.backend == BackendKind.Procedural)
            {
                if (!BackendCapabilityProbe.SupportsIndirect)
                {
                    handle.SetDegradedReason(BatchDegradedReason.BackendUnsupported);
                    OneShotWarnings.WarnBackendUnsupported(gate, id, batch.backend, BackendCapabilityProbe.MissingCapability);
                }
            }

            // 2. Occlusion stub (FR-018). Sets the reason but does NOT mark IsDegraded on its own.
            if (batch.culling.occlusion)
            {
                handle.SetDegradedReason(BatchDegradedReason.OcclusionStubActive);
                OneShotWarnings.WarnOcclusionStub(gate, id);
            }

            // 3. Material.enableInstancing check for Classic backend (FR-023). Real check lands at
            //    T034 alongside the Classic backend wiring; we already wire it here so the degraded
            //    handle path is uniform across backends.
            if (batch.backend == BackendKind.Classic && !batch.material.enableInstancing)
            {
                handle.SetDegradedReason(BatchDegradedReason.MissingEnableInstancing);
                OneShotWarnings.WarnMissingEnableInstancing(gate, id, batch.material);
            }

            _handles.Add(handle);
            return handle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Detach from the loop scheduler first so no further ticks fire.
            PFound.LoopScheduler.LoopScheduler.DeregisterBeforeRenderLoop(OnBeforeRender);

            // Invalidate all live handles. Disposing each handle calls back into OnHandleDisposed,
            // which mutates _handles; iterate over a snapshot to avoid re-entrance.
            var snapshot = _handles.ToArray();
            foreach (var h in snapshot)
            {
                try { h.Dispose(); } catch { /* defensive */ }
            }
            _handles.Clear();
            _pendingRemove.Clear();

            _visibilityBuffer.Dispose();
            if (_distanceStageScratchCreated && _distanceStageScratch.IsCreated)
            {
                _distanceStageScratch.Dispose();
                _distanceStageScratchCreated = false;
            }

            if (_ownerGameObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_ownerGameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_ownerGameObject);
                _ownerGameObject = null;
            }
        }

        // ---------------- Handle callback ----------------

        /// <summary>
        /// Invoked by <see cref="BatchHandle.Dispose"/> after the handle has marked itself disposed.
        /// Defers removal during a tick to <see cref="_pendingRemove"/> to keep <see cref="_handles"/>
        /// stable mid-enumeration.
        /// </summary>
        internal void OnHandleDisposed(BatchHandle handle)
        {
            if (_disposed) return;
            if (_ticking)
                _pendingRemove.Add(handle);
            else
                _handles.Remove(handle);
        }

        // ---------------- Tick loop ----------------

        internal void OnBeforeRender()
        {
            if (_disposed) return;
            if (_handles.Count == 0) return;

            // No active cameras → nothing to draw (edge case: between scene loads, EditMode without
            // a Game view). Skip the tick cleanly.
            int cameraCount = Camera.allCamerasCount;
            if (cameraCount == 0) return;

            _ticking = true;
            try
            {
                // Camera.allCameras allocates a fresh array each call. Phase 11 accepts this cost
                // (acceptable for our typical 1-3 camera scenes); a future optimization may cache it
                // via Camera.GetAllCameras(Camera[]) with a service-owned scratch buffer.
                var cameras = Camera.allCameras;
                for (int c = 0; c < cameras.Length; c++)
                {
                    var camera = cameras[c];
                    if (camera == null) continue;
                    TickCamera(camera);
                }
            }
            finally
            {
                _ticking = false;
                // Drain pending removes from in-tick handle disposals.
                if (_pendingRemove.Count > 0)
                {
                    for (int i = 0; i < _pendingRemove.Count; i++)
                        _handles.Remove(_pendingRemove[i]);
                    _pendingRemove.Clear();
                }
            }
        }

        private void TickCamera(Camera camera)
        {
            FrustumPlanes.FromCamera(camera, _frustumPlanesScratch, out var planes);

            for (int i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                if (!handle.IsAlive) continue;
                if (handle.IsDegraded) continue;

                var desc = handle.Descriptor;

                // RenderGraph batches are dispatched by BatchRenderingFeature inside the URP
                // pipeline; skip the direct-draw path entirely here to avoid double-draw (FR-021).
                if (desc.participatesInRenderGraph) continue;

                // Per-tick mesh/material liveness check (owner-managed contract — FR detected on tick).
                if (desc.mesh == null)
                {
                    handle.SetDegradedReason(BatchDegradedReason.MeshDestroyed);
                    OneShotWarnings.WarnMeshDestroyed(handle.Diagnostics, handle.Id);
                    continue;
                }
                if (desc.material == null)
                {
                    handle.SetDegradedReason(BatchDegradedReason.MaterialDestroyed);
                    OneShotWarnings.WarnMaterialDestroyed(handle.Diagnostics, handle.Id);
                    continue;
                }

                int count = desc.source.Count;
                if (count < 0) count = 0;

                handle.RecordTick(count, 0);

                if (count == 0)
                {
                    OneShotWarnings.WarnZeroCountFirstSeen(handle.Diagnostics, handle.Id);
                    continue;
                }

                // Source prepares (Transform[] flatten lands at T060; pure-data sources pass through).
                desc.source.OnTickBegin(default, out JobHandle producedHandle);

                // Resolve source view. Mutually-exclusive contract: exactly one returns true.
                bool hasMatrices = desc.source.TryGetNativeArrayView(out NativeArray<float4x4> matrices);
                bool hasBuffer = desc.source.TryGetComputeBuffer(out ComputeBuffer computeBuffer, out int stride);

                if (!hasMatrices && !hasBuffer)
                {
                    handle.SetDegradedReason(BatchDegradedReason.InvalidSource);
                    OneShotWarnings.WarnInvalidSource(handle.Diagnostics, handle.Id);
                    producedHandle.Complete();
                    continue;
                }

                _visibilityBuffer.EnsureCapacity(count);
                _visibilityBuffer.Reset();

                int visibleCount = RunCull(count, camera, planes, hasMatrices ? matrices : default, in desc, producedHandle);

                handle.RecordTick(count, visibleCount);
                if (visibleCount == 0) continue;

                var ctx = new DispatchContext(
                    camera,
                    visibleCount,
                    _visibilityBuffer.VisibleIndices.AsArray(),
                    hasMatrices ? matrices : default,
                    hasBuffer ? computeBuffer : null,
                    hasBuffer ? stride : 0,
                    in desc);

                try
                {
                    handle.BackendState?.Dispatch(in ctx);
                }
                catch (Exception ex)
                {
                    // Defensive — backend implementations should not throw. Log once if they do.
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Runs the per-batch cull pipeline. Honors <see cref="CullingPolicy.None"/> as
        /// pass-through; otherwise schedules the Burst <see cref="FrustumCullJob"/> and, when
        /// distance culling is enabled, chains <see cref="DistanceCullJob"/> after it.
        /// </summary>
        private int RunCull(int count, Camera camera, FrustumPlanes planes, NativeArray<float4x4> matrices, in BatchRenderingBatch desc, JobHandle dependency)
        {
            // CullingPolicy.None → pass-through all instances. Only CPU-side sources have matrices
            // here; GPU-side (ComputeBuffer) sources cull on the GPU and use CullingPolicy.None.
            if (!desc.culling.frustum && !desc.culling.distance.enabled)
            {
                dependency.Complete();
                FillAllVisible(count);
                return count;
            }

            // Frustum cull requires CPU-side matrices. GPU-side sources should set CullingPolicy.None
            // (their compute pipeline owns culling). If a GPU source sneaks in with frustum=true,
            // skip to "all visible" rather than throwing — the indirect args buffer will draw
            // whatever count the consumer authored.
            if (!matrices.IsCreated)
            {
                dependency.Complete();
                FillAllVisible(count);
                return count;
            }

            // Mesh bounds → sphere center + radius for the conservative Burst cull.
            Bounds meshBounds = desc.mesh.bounds;
            var meshCenter = new float3(meshBounds.center.x, meshBounds.center.y, meshBounds.center.z);
            float meshRadius = meshBounds.extents.magnitude;

            bool needDistance = desc.culling.distance.enabled && desc.culling.distance.maxDistance > 0f;

            // Single-stage frustum cull → writes directly to _visibilityBuffer (current behavior).
            if (!needDistance)
            {
                var job = new FrustumCullJob
                {
                    Matrices = matrices,
                    Planes = planes,
                    MeshLocalCenter = meshCenter,
                    MeshLocalRadius = meshRadius,
                    VisibleIndices = _visibilityBuffer.VisibleIndices.AsParallelWriter(),
                };
                job.Schedule(count, 64, dependency).Complete();
                return _visibilityBuffer.VisibleCount;
            }

            // Two-stage cull: frustum → distance scratch → final visibility buffer.
            EnsureDistanceStageScratch(count);
            _distanceStageScratch.Clear();

            // Stage 1 — frustum to scratch.
            var frustumJob = new FrustumCullJob
            {
                Matrices = matrices,
                Planes = planes,
                MeshLocalCenter = meshCenter,
                MeshLocalRadius = meshRadius,
                VisibleIndices = _distanceStageScratch.AsParallelWriter(),
            };
            JobHandle frustumHandle = desc.culling.frustum
                ? frustumJob.Schedule(count, 64, dependency)
                : default;

            // If frustum is disabled but distance is on, fill scratch with all indices.
            if (!desc.culling.frustum)
            {
                dependency.Complete();
                _distanceStageScratch.Length = count;
                for (int i = 0; i < count; i++) _distanceStageScratch[i] = i;
                frustumHandle = default;
            }
            else
            {
                frustumHandle.Complete();
            }

            int frustumVisibleCount = _distanceStageScratch.Length;
            if (frustumVisibleCount == 0) return 0;

            // Stage 2 — distance from active camera.
            float3 camPos = camera != null
                ? new float3(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z)
                : float3.zero;
            float maxDistSq = desc.culling.distance.maxDistance * desc.culling.distance.maxDistance;

            var distJob = new DistanceCullJob
            {
                Matrices = matrices,
                FrustumVisibleIndices = _distanceStageScratch.AsArray(),
                FrustumVisibleCount = frustumVisibleCount,
                CameraPosition = camPos,
                MaxDistanceSq = maxDistSq,
                FinalVisibleIndices = _visibilityBuffer.VisibleIndices.AsParallelWriter(),
            };
            distJob.Schedule(frustumVisibleCount, 64).Complete();

            return _visibilityBuffer.VisibleCount;
        }

        /// <summary>
        /// Allocates the distance-stage scratch list lazily on first need; grows it lazily.
        /// </summary>
        private void EnsureDistanceStageScratch(int required)
        {
            if (!_distanceStageScratchCreated)
            {
                _distanceStageScratch = new NativeList<int>(System.Math.Max(required, 64), Allocator.Persistent);
                _distanceStageScratchCreated = true;
                return;
            }
            if (_distanceStageScratch.Capacity < required)
            {
                _distanceStageScratch.Capacity = required;
            }
        }

        /// <summary>
        /// Pass-through fill — used for <see cref="CullingPolicy.None"/> and GPU-source paths.
        /// Writes indices [0..count) into the visibility buffer.
        /// </summary>
        private void FillAllVisible(int count)
        {
            // Set length first so the AsArray() consumers see the right slice.
            _visibilityBuffer.SetVisibleCount(count);
            var list = _visibilityBuffer.VisibleIndices;
            for (int i = 0; i < count; i++)
            {
                list[i] = i;
            }
        }

        // ---------------- RenderGraph integration ----------------

        /// <summary>
        /// Called by <c>BatchRenderingFeature</c>'s pass body. Walks registered batches with
        /// <see cref="BatchRenderingBatch.participatesInRenderGraph"/> = <c>true</c>, runs cull for
        /// <paramref name="camera"/>, and records dispatches onto <paramref name="cmd"/>.
        /// </summary>
        /// <remarks>
        /// Phase 11 re-runs the cull per pass call (rather than caching the BeforeRender result).
        /// Documented as a deferred optimization in plan.md notes — for now the duplicate Burst
        /// cost is acceptable and the per-camera correctness is simpler than threading per-camera
        /// state through the visibility buffer.
        /// </remarks>
        internal void ExecuteRenderGraphBatches(UnityEngine.Rendering.RasterCommandBuffer cmd, Camera camera)
        {
            if (_disposed) return;
            if (camera == null) return;
            if (_handles.Count == 0) return;

            FrustumPlanes.FromCamera(camera, _frustumPlanesScratch, out var planes);

            for (int i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                if (!handle.IsAlive) continue;
                if (handle.IsDegraded) continue;

                var desc = handle.Descriptor;
                if (!desc.participatesInRenderGraph) continue;
                if (desc.mesh == null || desc.material == null) continue;

                int count = desc.source.Count;
                if (count <= 0) continue;

                desc.source.OnTickBegin(default, out JobHandle producedHandle);
                bool hasMatrices = desc.source.TryGetNativeArrayView(out NativeArray<float4x4> matrices);
                bool hasBuffer = desc.source.TryGetComputeBuffer(out ComputeBuffer computeBuffer, out int stride);

                if (!hasMatrices && !hasBuffer)
                {
                    producedHandle.Complete();
                    continue;
                }

                _visibilityBuffer.EnsureCapacity(count);
                _visibilityBuffer.Reset();
                int visibleCount = RunCull(count, camera, planes, hasMatrices ? matrices : default, in desc, producedHandle);

                handle.RecordTick(count, visibleCount);
                if (visibleCount == 0) continue;

                var ctx = new DispatchContext(
                    camera,
                    visibleCount,
                    _visibilityBuffer.VisibleIndices.AsArray(),
                    hasMatrices ? matrices : default,
                    hasBuffer ? computeBuffer : null,
                    hasBuffer ? stride : 0,
                    in desc);

                try { handle.BackendState?.DispatchRasterCmd(cmd, in ctx); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // ---------------- Null backend (Phase 2 placeholder) ----------------

        /// <summary>
        /// No-op backend state used during Phase 2 foundational shell. Replaced per-batch by the
        /// real backend states (Classic / Indirect / Procedural) when their respective user-story
        /// phases land (T035 / T045 / T064).
        /// </summary>
        private sealed class NullBackendState : IBackendState
        {
            public void Dispatch(in DispatchContext ctx)
            {
                // intentionally empty — degraded / unsupported / placeholder batches issue no draws
            }

            public void DispatchRasterCmd(UnityEngine.Rendering.RasterCommandBuffer cmd, in DispatchContext ctx)
            {
                // intentionally empty — same reason as Dispatch
            }

            public void Dispose()
            {
                // nothing to release
            }
        }
    }
}

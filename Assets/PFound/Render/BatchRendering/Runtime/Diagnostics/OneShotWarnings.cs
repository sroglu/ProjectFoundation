using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.Render.BatchRendering
{
    /// <summary>
    /// Categories of one-shot diagnostics emitted by <see cref="BatchRenderingService"/>. Gated per
    /// <c>(batchId, kind)</c> tuple via <see cref="OneShotGate"/> so each category emits at most once
    /// per affected batch (FR-025).
    /// </summary>
    internal enum DiagnosticKind
    {
        MissingEnableInstancing,
        BackendUnsupported,
        OcclusionStubActive,
        ZeroCountFirstSeen,
        InvalidSource,
        MeshDestroyed,
        MaterialDestroyed,
        FeatureWithoutService,
        TransformArrayNullEntry,
    }

    /// <summary>
    /// Per-batch (or per-service when used with the service's own GUID) emission gate. Each
    /// <c>(batchId, kind)</c> pair fires <see cref="TryEmit"/> once and returns <c>false</c> on every
    /// subsequent call with the same pair.
    /// </summary>
    /// <remarks>
    /// Main-thread only. The internal <see cref="HashSet{T}"/> is not synchronized; tick path is
    /// single-threaded by design (see <see cref="IBatchRenderingService"/> thread-safety note).
    /// </remarks>
    internal sealed class OneShotGate
    {
        private readonly HashSet<(Guid batchId, DiagnosticKind kind)> _emitted = new();

        /// <summary>
        /// Returns <c>true</c> the first time this <c>(batchId, kind)</c> pair is seen; <c>false</c>
        /// thereafter. Callers should branch on the result and only log when <c>true</c>.
        /// </summary>
        public bool TryEmit(Guid batchId, DiagnosticKind kind)
        {
            return _emitted.Add((batchId, kind));
        }

        /// <summary>
        /// Removes all gating state for the given batch — used when the service finalizes a batch
        /// dispose so re-registering with the same identity would re-fire (in practice each batch
        /// gets a fresh GUID, so this is a defensive method only).
        /// </summary>
        public void Forget(Guid batchId)
        {
            _emitted.RemoveWhere(t => t.batchId == batchId);
        }

        /// <summary>Total recorded (batch, kind) pairs — exposed for tests.</summary>
        internal int RecordedCount => _emitted.Count;
    }

    /// <summary>
    /// Stable-format emission helpers for each <see cref="DiagnosticKind"/>. Centralized so message
    /// shape is one source of truth (testable via <c>LogAssert.Expect</c>).
    /// </summary>
    internal static class OneShotWarnings
    {
        internal const string LogPrefix = "[BatchRendering]";

        internal static void WarnMissingEnableInstancing(OneShotGate gate, Guid batchId, Material material)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.MissingEnableInstancing)) return;
            string matName = material != null ? material.name : "<null>";
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' material '{matName}' is missing enableInstancing. Batch degraded to no-op. Toggle enableInstancing in the material importer and re-register.");
        }

        internal static void WarnBackendUnsupported(OneShotGate gate, Guid batchId, BackendKind requested, string missingCapability)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.BackendUnsupported)) return;
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' requested {requested} backend, but host platform lacks {missingCapability}. Batch degraded to no-op. Consider falling back to Classic backend manually.");
        }

        internal static void WarnOcclusionStub(OneShotGate gate, Guid batchId)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.OcclusionStubActive)) return;
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' enabled CullingPolicy.occlusion = true, but occlusion culling is not implemented in Phase 11. Flag treated as no-op.");
        }

        internal static void WarnZeroCountFirstSeen(OneShotGate gate, Guid batchId)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.ZeroCountFirstSeen)) return;
            Debug.Log($"{LogPrefix} Batch '{batchId}' source returned Count == 0 — skipping dispatch (first occurrence). This note is informational and will not repeat.");
        }

        internal static void WarnInvalidSource(OneShotGate gate, Guid batchId)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.InvalidSource)) return;
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' instance source returned false from both TryGetNativeArrayView and TryGetComputeBuffer, or its underlying container is no longer valid. Batch degraded to no-op. This typically means the owner forgot to dispose the batch handle before disposing the underlying NativeArray / ComputeBuffer (owner-managed contract violation — see CODING-STYLE.md §8).");
        }

        internal static void WarnMeshDestroyed(OneShotGate gate, Guid batchId)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.MeshDestroyed)) return;
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' referenced Mesh was destroyed externally. Batch degraded to no-op. Owner-managed contract violation — see CODING-STYLE.md §8.");
        }

        internal static void WarnMaterialDestroyed(OneShotGate gate, Guid batchId)
        {
            if (!gate.TryEmit(batchId, DiagnosticKind.MaterialDestroyed)) return;
            Debug.LogWarning($"{LogPrefix} Batch '{batchId}' referenced Material was destroyed externally. Batch degraded to no-op. Owner-managed contract violation — see CODING-STYLE.md §8.");
        }

        internal static void WarnFeatureWithoutService(OneShotGate gate, Guid featureId, string rendererAssetName)
        {
            if (!gate.TryEmit(featureId, DiagnosticKind.FeatureWithoutService)) return;
            Debug.LogWarning($"{LogPrefix} BatchRenderingFeature on renderer asset '{rendererAssetName}' has no service attached. Call BatchRenderingFeature.AttachService(...) from your consumer code, or remove the feature from this renderer asset.");
        }

        internal static void WarnTransformArrayNullEntry(OneShotGate gate, Guid sourceId, int index)
        {
            // Per-(sourceId, index) gating would require a different shape; reuse (sourceId, kind) and
            // include the index in the message — first null index reports, later null indices do not.
            // This matches the spec edge-case note: "one-shot warn per source", not per index.
            if (!gate.TryEmit(sourceId, DiagnosticKind.TransformArrayNullEntry)) return;
            Debug.LogWarning($"{LogPrefix} TransformArrayInstanceSource '{sourceId}' contains a null Transform at index {index}. That slot is silently skipped (zero matrix). Further null indices in this source will not be reported.");
        }
    }
}

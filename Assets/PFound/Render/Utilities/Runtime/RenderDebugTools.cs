using System.Diagnostics;
using UnityEngine;

namespace PFound.Render.Utilities
{
    /// <summary>
    /// Debug-draw helpers that compile to no-ops in non-development player builds.
    /// All public methods are <see cref="ConditionalAttribute"/>-gated to
    /// <c>UNITY_EDITOR</c> + <c>DEVELOPMENT_BUILD</c>, so call sites are stripped
    /// at compile time in release builds (SC-009).
    /// </summary>
    public static class RenderDebugTools
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawWorldLine(Vector3 from, Vector3 to, Color color, float duration = 0f)
            => UnityEngine.Debug.DrawLine(from, to, color, duration);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawWorldRay(Vector3 origin, Vector3 direction, Color color, float duration = 0f)
            => UnityEngine.Debug.DrawRay(origin, direction, color, duration);

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawWorldBox(Vector3 center, Vector3 size, Quaternion rotation, Color color, float duration = 0f)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + rotation * new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + rotation * new Vector3( h.x, -h.y, -h.z);
            Vector3 p010 = center + rotation * new Vector3(-h.x,  h.y, -h.z);
            Vector3 p110 = center + rotation * new Vector3( h.x,  h.y, -h.z);
            Vector3 p001 = center + rotation * new Vector3(-h.x, -h.y,  h.z);
            Vector3 p101 = center + rotation * new Vector3( h.x, -h.y,  h.z);
            Vector3 p011 = center + rotation * new Vector3(-h.x,  h.y,  h.z);
            Vector3 p111 = center + rotation * new Vector3( h.x,  h.y,  h.z);

            UnityEngine.Debug.DrawLine(p000, p100, color, duration);
            UnityEngine.Debug.DrawLine(p010, p110, color, duration);
            UnityEngine.Debug.DrawLine(p001, p101, color, duration);
            UnityEngine.Debug.DrawLine(p011, p111, color, duration);

            UnityEngine.Debug.DrawLine(p000, p010, color, duration);
            UnityEngine.Debug.DrawLine(p100, p110, color, duration);
            UnityEngine.Debug.DrawLine(p001, p011, color, duration);
            UnityEngine.Debug.DrawLine(p101, p111, color, duration);

            UnityEngine.Debug.DrawLine(p000, p001, color, duration);
            UnityEngine.Debug.DrawLine(p100, p101, color, duration);
            UnityEngine.Debug.DrawLine(p010, p011, color, duration);
            UnityEngine.Debug.DrawLine(p110, p111, color, duration);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawWorldArrow(Vector3 from, Vector3 to, Color color, float headSize = 0.25f, float duration = 0f)
        {
            UnityEngine.Debug.DrawLine(from, to, color, duration);
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            dir /= len;
            Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 150f, 0) * Vector3.forward * headSize;
            Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, -150f, 0) * Vector3.forward * headSize;
            UnityEngine.Debug.DrawLine(to, to + left, color, duration);
            UnityEngine.Debug.DrawLine(to, to + right, color, duration);
        }
    }
}
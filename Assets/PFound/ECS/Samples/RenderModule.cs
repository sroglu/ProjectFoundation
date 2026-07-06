using UnityEngine;
using PFound.ECS;

namespace PFound.ECS.Samples.RenderModule
{
    /// <summary>Oscillates an entity's color between two endpoints.</summary>
    public struct ColorPulse
    {
        public Color ColorA;
        public Color ColorB;
        public float Speed;
        public float Phase;
    }

    /// <summary>Managed binding to the renderer this entity tints.</summary>
    public struct RendererBinding
    {
        public Renderer Renderer;
    }

    /// <summary>Writes the pulsed color via a MaterialPropertyBlock (no per-entity material instances).</summary>
    [ECSSystem(ECSPhase.OnRender)]
    public sealed class ColorPulseSystem : SystemBase
    {
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor"); // URP/HDRP lit
        private static readonly int s_color = Shader.PropertyToID("_Color");         // built-in
        private MaterialPropertyBlock _mpb;
        private RefAction<ColorPulse, RendererBinding> _apply;

        protected override void OnInitialize()
        {
            _mpb = new MaterialPropertyBlock();
            _apply = Apply; // cache the delegate once so the ForEach hot path stays zero-alloc
        }

        protected override void Execute() =>
            World.Query<ColorPulse, RendererBinding>().ForEach(_apply);

        private void Apply(World w, Entity e, ref ColorPulse pulse, ref RendererBinding bind)
        {
            if (bind.Renderer == null) return;
            pulse.Phase += pulse.Speed * w.DeltaTime;
            float k = (Mathf.Sin(pulse.Phase) + 1f) * 0.5f;
            Color c = Color.Lerp(pulse.ColorA, pulse.ColorB, k);

            bind.Renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(s_baseColor, c);
            _mpb.SetColor(s_color, c);
            bind.Renderer.SetPropertyBlock(_mpb);
        }
    }
}

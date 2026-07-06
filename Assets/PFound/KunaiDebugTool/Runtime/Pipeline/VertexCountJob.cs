using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Kunai
{
    [BurstCompile]
    internal struct VertexCountJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<KuiDrawCommand> Commands;
        [WriteOnly] public NativeArray<int> VertexCounts;

        [BurstCompile]
        public void Execute(int index)
        {
            var cmd = Commands[index];

            switch (cmd.CmdType)
            {
                case KuiDrawCommand.Type.Rect:
                {
                    float x0 = Unity.Mathematics.math.max(cmd.Rect.x, cmd.ClipRect.x);
                    float y0 = Unity.Mathematics.math.max(cmd.Rect.y, cmd.ClipRect.y);
                    float x1 = Unity.Mathematics.math.min(cmd.Rect.x + cmd.Rect.z, cmd.ClipRect.x + cmd.ClipRect.z);
                    float y1 = Unity.Mathematics.math.min(cmd.Rect.y + cmd.Rect.w, cmd.ClipRect.y + cmd.ClipRect.w);

                    VertexCounts[index] = (x1 > x0 && y1 > y0) ? 4 : 0;
                    break;
                }
                case KuiDrawCommand.Type.Label:
                {
                    VertexCounts[index] = cmd.TextLength * 4;
                    break;
                }
                case KuiDrawCommand.Type.Line:
                {
                    // Bounding-box cull: line bbox = endpoint bbox padded by
                    // half-thickness on every side. Entirely-outside → 0 verts.
                    float halfT = cmd.Thickness * 0.5f;
                    float minX = Unity.Mathematics.math.min(cmd.Rect.x, cmd.Rect.z) - halfT;
                    float maxX = Unity.Mathematics.math.max(cmd.Rect.x, cmd.Rect.z) + halfT;
                    float minY = Unity.Mathematics.math.min(cmd.Rect.y, cmd.Rect.w) - halfT;
                    float maxY = Unity.Mathematics.math.max(cmd.Rect.y, cmd.Rect.w) + halfT;

                    bool visible = maxX > cmd.ClipRect.x
                                && minX < cmd.ClipRect.x + cmd.ClipRect.z
                                && maxY > cmd.ClipRect.y
                                && minY < cmd.ClipRect.y + cmd.ClipRect.w;

                    VertexCounts[index] = visible ? 4 : 0;
                    break;
                }
                default:
                    VertexCounts[index] = 0;
                    break;
            }
        }
    }
}

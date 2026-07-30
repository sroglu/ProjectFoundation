using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Kunai
{
    [BurstCompile]
    internal struct PrefixSumJob : IJob
    {
        [ReadOnly] public NativeArray<int> VertexCounts;
        public NativeArray<int> VertexOffsets;
        public NativeArray<int> TotalVertexCount;

        [BurstCompile]
        public void Execute()
        {
            int count = VertexCounts.Length;
            int running = 0;

            for (int i = 0; i < count; i++)
            {
                VertexOffsets[i] = running;
                running += VertexCounts[i];
            }

            TotalVertexCount[0] = running;
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Kunai
{
    [BurstCompile]
    internal struct LayerSortJob : IJob
    {
        public NativeArray<KuiDrawCommand> Commands;

        [BurstCompile]
        public void Execute()
        {
            int count = Commands.Length;
            if (count <= 1) return;

            const int bucketCount = 3;
            var bucketCounts = new NativeArray<int>(bucketCount, Allocator.Temp);
            var temp = new NativeArray<KuiDrawCommand>(count, Allocator.Temp);

            for (int i = 0; i < count; i++)
                bucketCounts[Commands[i].Layer]++;

            var offsets = new NativeArray<int>(bucketCount, Allocator.Temp);
            offsets[0] = 0;
            for (int i = 1; i < bucketCount; i++)
                offsets[i] = offsets[i - 1] + bucketCounts[i - 1];

            for (int i = 0; i < count; i++)
            {
                byte layer = Commands[i].Layer;
                int idx = offsets[layer];
                temp[idx] = Commands[i];
                offsets[layer] = idx + 1;
            }

            NativeArray<KuiDrawCommand>.Copy(temp, Commands);

            temp.Dispose();
            offsets.Dispose();
            bucketCounts.Dispose();
        }
    }
}

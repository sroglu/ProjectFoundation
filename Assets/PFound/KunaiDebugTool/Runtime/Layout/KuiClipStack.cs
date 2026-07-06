using Unity.Mathematics;
using Unity.Collections;

namespace Kunai
{
    internal struct KuiClipStack : System.IDisposable
    {
        NativeList<float4> _stack;

        public KuiClipStack(int initialCapacity)
        {
            _stack = new NativeList<float4>(initialCapacity, Allocator.Persistent);
        }

        public float4 Current => _stack.Length > 0 ? _stack[_stack.Length - 1] : new float4(0, 0, 99999, 99999);

        public void Push(float4 clipRect)
        {
            var parent = Current;
            float x0 = math.max(clipRect.x, parent.x);
            float y0 = math.max(clipRect.y, parent.y);
            float x1 = math.min(clipRect.x + clipRect.z, parent.x + parent.z);
            float y1 = math.min(clipRect.y + clipRect.w, parent.y + parent.w);
            _stack.Add(new float4(x0, y0, math.max(0, x1 - x0), math.max(0, y1 - y0)));
        }

        public void Pop()
        {
            if (_stack.Length > 0)
                _stack.Length--;
        }

        public void Clear()
        {
            _stack.Clear();
        }

        public void Dispose()
        {
            if (_stack.IsCreated) _stack.Dispose();
        }
    }
}

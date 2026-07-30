using System;

namespace PFound.Render.Core.ShaderParameters
{
    /// <summary>
    /// Internal registration entry. Compared first by <see cref="Priority"/>
    /// (ascending) then by <see cref="InsertionOrder"/> (FIFO tiebreaker).
    /// </summary>
    internal struct PriorityRegistration : IComparable<PriorityRegistration>
    {
        public IGlobalShaderParameterProvider Provider;
        public int Priority;
        public int InsertionOrder;
        public int LastPublishedFrame;

        public int CompareTo(PriorityRegistration other)
        {
            int c = Priority.CompareTo(other.Priority);
            if (c != 0) return c;
            return InsertionOrder.CompareTo(other.InsertionOrder);
        }
    }
}
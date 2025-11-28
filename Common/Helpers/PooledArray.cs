using System.Buffers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers
{
    // https://www.c-sharpcorner.com/article/mastering-arraypoolt-in-c-sharp-net-to-cut-down-allocations/
    internal readonly ref struct PooledArray<T>
    {
        public T[] Array { get; }
        public int Length { get; }

        public PooledArray(int minLength)
        {
            Array = ArrayPool<T>.Shared.Rent(minLength);
            Length = minLength;
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(Array);
        }
    }
}

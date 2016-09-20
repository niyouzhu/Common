using System;
using System.Threading;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    internal class BufferSegment
    {
#if DEBUG
        private static int NextId = 0;
        private int _segmentId;
#endif

        public ArraySegment<byte> Buffer { get; private set; }
        public BufferSegment Next { get; set; }

        public BufferCursor Start => new BufferCursor(this, 0);
        public BufferCursor End => new BufferCursor(this, Count);
        public int Count => Buffer.Count;

        public bool IsEmpty => Buffer.Count == 0;


        public BufferSegment() : this(default(ArraySegment<byte>)) { }

        public BufferSegment(ArraySegment<byte> buffer)
        {
            Buffer = buffer;

#if DEBUG
            _segmentId = Interlocked.Increment(ref NextId);
#endif
        }

        public void Truncate(int offset)
        {
            if (offset > 0)
            {
                Buffer = new ArraySegment<byte>(Buffer.Array, Buffer.Offset + offset, Buffer.Count - offset);
            }
        }

        public void Clear()
        {
            Buffer = default(ArraySegment<byte>);
        }

        public void Replace(ArraySegment<byte> buffer)
        {
            Buffer = buffer;
        }

        public void CopyTo(byte[] dest, int destOffset, int count)
        {
            var remainingSpace = dest.Length - destOffset;
            if(remainingSpace < count)
            {
                throw new ArgumentOutOfRangeException(nameof(dest), "Not enough space in the destination to hold the data");
            }
            System.Buffer.BlockCopy(Buffer.Array, Buffer.Offset, dest, destOffset, count);
        }
    }
}

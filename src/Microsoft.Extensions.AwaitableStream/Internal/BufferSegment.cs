using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class BufferSegment
    {
#if DEBUG
        // Really useful to know in debugging :)
        private static int NextId = 0;
        private int _segmentId;
#endif

        public bool Owned { get; private set; }
        public ArraySegment<byte> Buffer { get; private set; }
        public BufferSegment Next { get; set; }

        public int Count => Buffer.Count;

        public bool IsEmpty => Buffer.Count == 0;

        private string DebuggerDisplay
        {
            get
            {
                var str = "{" + string.Join(",", Buffer.Array.Skip(Buffer.Offset).Take(Buffer.Count).Select(b => "0x" + b.ToString("X"))) + "}";
#if DEBUG
                str = $"[#{_segmentId}] {str} -> #{Next._segmentId}";
#endif
                return str;
            }
        }

        public BufferSegment() : this(default(ArraySegment<byte>), owned: false) { }

        public BufferSegment(ArraySegment<byte> buffer, bool owned)
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
            Owned = false;
            Next = null;
        }

        public void Replace(ArraySegment<byte> buffer, bool owned)
        {
            Buffer = buffer;
            Owned = owned;
            Next = null;
        }

        /// <summary>
        /// Copies this entire buffer to the specified array, starting at the specified offset in the destination buffer.
        /// The destination array must have enough space to hold the entire buffer.
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="destOffset"></param>
        public void CopyTo(byte[] dest, int destOffset) => CopyTo(dest, destOffset, Buffer.Count);

        /// <summary>
        /// Copies the specified number of bytes out of the buffer to the specified array, starting at the specified offset
        /// in the destination buffer. The destination array must have enough space to hold the data
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="destOffset"></param>
        /// <param name="count"></param>
        public void CopyTo(byte[] dest, int destOffset, int count)
        {
            var remainingSpace = dest.Length - destOffset;
            if (remainingSpace < count)
            {
                throw new ArgumentOutOfRangeException(nameof(dest), "Not enough space in the destination to hold the data");
            }
            System.Buffer.BlockCopy(Buffer.Array, Buffer.Offset, dest, destOffset, count);
        }
    }
}

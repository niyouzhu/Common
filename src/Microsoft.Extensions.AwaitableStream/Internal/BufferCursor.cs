using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    public struct BufferCursor : IEquatable<BufferCursor>
    {
        internal int Index { get; set; }
        internal BufferSegment Segment { get; set; }

        internal bool IsEnd
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var segment = Segment;

                if (segment == null)
                {
                    return true;
                }
                else if (Index < segment.Count)
                {
                    return false;
                }
                else if (segment.Next == null)
                {
                    return true;
                }
                else
                {
                    return IsEndMultiSegment();
                }
            }
        }

        internal BufferCursor(BufferSegment segment, int index)
        {
            Segment = segment;
            Index = index;
        }

        public override bool Equals(object obj) => obj is BufferCursor && Equals((BufferCursor)obj);

        public override int GetHashCode() => Segment.GetHashCode() ^ Index;

        public bool Equals(BufferCursor other) => other.Segment == Segment && other.Index == Index;

        public static bool operator ==(BufferCursor l, BufferCursor r) => Equals(l, r);

        public static bool operator !=(BufferCursor l, BufferCursor r) => !Equals(l, r);

        public static BufferCursor operator+(BufferCursor l, int offset) => l.Seek(offset);

        // Note: Distance is called Distance(start, end) but '-' would be used 'end - start'.
        public static int operator-(BufferCursor l, BufferCursor r) => Distance(r, l);

        public static int Distance(BufferCursor start, BufferCursor end)
        {
            var distance = 0;
            var current = start.Segment;
            var offset = start.Index;
            while(current != null)
            {
                if (current == end.Segment)
                {
                    // This is the last segment
                    return distance + current.Count - offset - end.Index;
                }
                else
                {
                    distance += current.Count - offset;
                    current = current.Next;
                    offset = 0;
                }
            }
            throw new InvalidOperationException("Start and end cursors are not part of the same chain!");
        }

        public BufferCursor Seek(int bytes)
        {
            int _;
            return Seek(bytes, out _);
        }

        public BufferCursor Seek(int bytes, out int bytesSeeked)
        {
            if (IsEnd)
            {
                bytesSeeked = 0;
                return this;
            }

            var wasLastSegment = Segment.Next == null;
            var following = Segment.Count - Index;

            if (following >= bytes)
            {
                bytesSeeked = bytes;
                return new BufferCursor(Segment, Index + bytes);
            }

            var segment = Segment;
            var index = Index;
            while (true)
            {
                if (wasLastSegment)
                {
                    bytesSeeked = following;
                    return new BufferCursor(segment, index + following);
                }
                else
                {
                    bytes -= following;
                    segment = segment.Next;
                    index = 0;
                }

                wasLastSegment = segment.Next == null;

                if (segment.Count >= bytes)
                {
                    bytesSeeked = bytes;
                    return new BufferCursor(segment, index + bytes);
                }
            }
        }

        private bool IsEndMultiSegment()
        {
            var segment = Segment.Next;
            while (segment != null)
            {
                if (segment.Count > 0)
                {
                    return false; // subsequent block has data - IsEnd is false
                }
                segment = segment.Next;
            }
            return true;
        }
    }
}
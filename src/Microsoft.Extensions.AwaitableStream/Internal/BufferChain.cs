using System;
using System.Diagnostics;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    /// <summary>
    /// Represents a chain of buffers, where the last buffer is owned by some external process.
    /// Automatically handles copying the external buffer locally when needed.
    /// </summary>
    public class BufferChain
    {
        private BufferSegment _head;
        private BufferSegment _tail;
        private BufferCursor _consumed;
        private BufferCursor _owned;

        public bool IsEmpty => _head.IsEmpty && _head == _tail;

        public BufferChain()
        {
            // Initialize to an empty chain
            var node = new BufferSegment();
            _head = node;
            _tail = node;

            _consumed = new BufferCursor(node, 0);
            _owned = _consumed;
        }

        public void Append(ArraySegment<byte> buffer)
        {
            // TODO: If segment we're appending to is owned consider appending data into that segment rather
            // than adding a new node.
            // We need to measure the difference in copying versus using the exising buffer for that
            // scenario.

            if (IsEmpty)
            {
                _head.Replace(buffer);
                _tail = _head;
            }
            else
            {
                var node = new BufferSegment(buffer);
                _tail.Next = node;
                _tail = node;
            }
        }

        public void Consumed(int count)
        {
            // Advance the consumed cursor.
            _consumed += count;

            // Truncate the segments to that new cursor
            TruncateSegments(_consumed);

            // Now take ownership of the remainder of the buffer chain
            TakeOwnership();
        }

        public ByteBuffer GetBuffer() => new ByteBuffer(_head, _tail);

        /// <summary>
        /// Copies data out of the buffer chain, from <paramref name="start"/> to <paramref name="end"/>, into the specified buffer.
        /// </summary>
        public void CopyTo(byte[] buffer, BufferCursor start, BufferCursor end)
        {
            if(start == end)
            {
                // Nothing to copy
                return;
            }

            var current = start;
            var bufferPosition = 0;

            while (current.Segment != null)
            {
                if(current.Segment == end.Segment)
                {
                    // We're at the end
                    var toRead = end.Index - current.Index;
                    current.Segment.CopyTo(buffer, bufferPosition, toRead);
                    return;
                }
                else
                {
                    var toRead = current.Segment.Count - current.Index;
                    current.Segment.CopyTo(buffer, bufferPosition, toRead);
                    current += toRead;
                    bufferPosition += toRead;
                }
            }
        }

        /// <summary>
        /// Takes ownership of the remainder of the buffer.
        /// </summary>
        private void TakeOwnership()
        {
            var length = _tail.End - _owned;
            var buffer = new byte[length];
            CopyTo(buffer, _owned, _tail.End);

            // Now replace all the segments after _owned with this
            // In practice, Owned will be pointing at the end of a segment, so Truncate is a no-op
            var lastOwnedSegment = _owned.Segment;
            _owned.Segment.Truncate(_owned.Index);
            _owned.Segment.Next = new BufferSegment(new ArraySegment<byte>(buffer));
            _owned += buffer.Length;
        }

        /// <summary>
        /// Remove/truncate segments up to <paramref name="end"/>.
        /// </summary>
        /// <param name="end"></param>
        private void TruncateSegments(BufferCursor end)
        {
            var current = _head;
            while(current != null && current != end.Segment)
            {
                current = current.Next;
            }

            if(current == null)
            {
                // We're truncating all buffers, but we DO NOT deallocate the list
                _head.Clear();
                _tail = _head;
            }
            else
            {
                // Truncate the segment
                end.Segment.Truncate(end.Index);

                // Advance the head pointer
                _head = end.Segment;
            }
        }
    }
}

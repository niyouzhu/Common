using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    /// <summary>
    /// Represents a chain of buffers, where the last buffer is owned by some external process.
    /// Automatically handles copying the external buffer locally when needed.
    /// </summary>
    public class BufferChain : IEnumerable<BufferSegment>
    {
        public BufferSegment Head { get; private set; }
        public BufferSegment Tail { get; private set; }

        public bool IsEmpty => Head.IsEmpty && Head == Tail;

        public BufferChain()
        {
            // Initialize to an empty chain
            var node = new BufferSegment();
            Head = node;
            Tail = node;
        }

        public void Append(ArraySegment<byte> buffer)
        {
            // TODO: If segment we're appending to is owned consider appending data into that segment rather
            // than adding a new node.
            // We need to measure the difference in copying versus using the exising buffer for that
            // scenario.

            if (IsEmpty)
            {
                Head.Replace(buffer, owned: false);
                Tail = Head;
            }
            else
            {
                var node = new BufferSegment(buffer, owned: false);
                Tail.Next = node;
                Tail = node;
            }
        }

        public ByteBuffer GetBuffer() => new ByteBuffer(Head, Tail);

        public BufferSegmentEnumerator GetEnumerator() => new BufferSegmentEnumerator(Head);

        /// <summary>
        /// Takes ownership of the unowned segments at the end of the buffer.
        /// </summary>
        public void TakeOwnership()
        {
            // Seek to the first unowned segment
            var current = Head;
            while(current != null && current.Owned)
            {
                current = current.Next;
            }

            if(current == null)
            {
                // Everything is owned!
                return;
            }

            var firstUnownedBuffer = current;

            // Count the size of the remaining unowned segments
            var length = 0;
            do
            {
                length += current.Buffer.Count;
                current = current.Next;
                if(current.Owned)
                {
                    throw new InvalidOperationException("Did not expect an owned segment to come after an unowned segment!");
                }
            } while (current != null);

            if(length == 0)
            {
                // Only empty buffers left? Whatever...
                return;
            }

            // Allocate a new buffer to hold the data
            var buffer = new byte[length];

            // Copy each new segment in
            current = firstUnownedBuffer;
            var bufferPosition = 0;
            do
            {
                current.CopyTo(buffer, bufferPosition);
                bufferPosition += current.Count;
                current = current.Next;
            } while (current != null);

            // Replace the firstUnownedBuffer with this new data and make it the tail
            firstUnownedBuffer.Replace(new ArraySegment<byte>(buffer), owned: true);
            Tail = firstUnownedBuffer;
        }

        /// <summary>
        /// Remove/truncate segments up to <paramref name="count"/>.
        /// </summary>
        /// <param name="count"></param>
        public void Truncate(int count)
        {
            int remaining = count;
            var current = Head;
            while(current != null && remaining > 0)
            {
                if(current.Count > remaining)
                {
                    // This segment will be the new head, just truncate it
                    current.Truncate(remaining);
                    remaining = 0;
                }
                else
                {
                    // Need to move to the next segment
                    current = current.Next;
                    remaining -= current.Count;
                }
            }

            if (current == null)
            {
                // We truncated everything! Just clear the current head segment
                Head.Clear();
            }
            else
            {
                // Whatever segment we're at is the new head
                Head = current;
            }
        }

        IEnumerator<BufferSegment> IEnumerable<BufferSegment>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

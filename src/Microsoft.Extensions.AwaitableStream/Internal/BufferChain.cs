using System;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    /// <summary>
    /// Represents a chain of buffers, where the last buffer is owned by some external process.
    /// Automatically handles copying the external buffer locally when needed.
    /// </summary>
    public class BufferChain
    {
        private ArraySegment<byte> _singleItem;
        private BufferSegment _head;
        private BufferSegment _tail;

        public void Append(ArraySegment<byte> buffer)
        {
            // TODO: If segment we're appending to is owned consider appending data into that segment rather
            // than adding a new node.
            // We need to measure the difference in copying versus using the exising buffer for that
            // scenario.

            if (_head == null)
            {
                // The list is empty, we just store the current write
                _singleItem = buffer;
            }
            else
            {
                // Otherwise add this segment to the end of the list
                var segment = new BufferSegment();
                segment.Buffer = buffer;
                _tail.Next = segment;
                _tail = segment;
            }
        }

        public void Consumed(int count)
        {
            // We didn't consume everything
            if (count < _singleItem.Count)
            {
                // Make a list with the buffer in it and mark the right bytes as consumed
                if (_head == null)
                {
                    _head = new BufferSegment();
                    _head.Buffer = _singleItem;
                    _tail = _head;
                }
            }
            else if (_head == null)
            {
                // We consumed everything and there was no list
                _singleItem = default(ArraySegment<byte>);
                return;
            }

            var segment = _head;
            var segmentOffset = segment.Buffer.Offset;

            while (count > 0)
            {
                var consumed = Math.Min(segment.Buffer.Count, count);

                count -= consumed;
                segmentOffset += consumed;

                if (segmentOffset == segment.End && _head != _tail)
                {
                    // Move to the next node
                    segment = segment.Next;
                    segmentOffset = segment.Buffer.Offset;
                }

                // End of the list stop
                if (_head == _tail)
                {
                    break;
                }
            }

            // Reset the head to the unconsumed buffer
            _head = segment;
            _head.Buffer = new ArraySegment<byte>(segment.Buffer.Array, segmentOffset, segment.End - segmentOffset);

            // Loop from head to tail and copy unconsumed data into buffers we own, this
            // is important because after the call the WriteAsync returns, the stream can reuse these
            // buffers for anything else
            int length = 0;

            segment = _head;
            while (true)
            {
                if (!segment.Owned)
                {
                    length += segment.Buffer.Count;
                }

                if (segment == _tail)
                {
                    break;
                }

                segment = segment.Next;
            }

            // This can happen for 2 reasons:
            // 1. We consumed everything
            // 2. We own all the buffers with data, so no need to copy again.
            if (length == 0)
            {
                return;
            }

            // REVIEW: Use array pool here?
            // Possibly use fixed size blocks here and just fill them so we can avoid a byte[] per call to write
            var buffer = new byte[length];

            // This loop does 2 things
            // 1. Finds the first owned buffer in the list
            // 2. Copies data into the buffer we just allocated
            BufferSegment owned = null;
            segment = _head;
            var offset = 0;

            while (true)
            {
                if (!segment.Owned)
                {
                    Buffer.BlockCopy(segment.Buffer.Array, segment.Buffer.Offset, buffer, offset, segment.Buffer.Count);
                    offset += segment.Buffer.Count;
                }
                else if (owned == null)
                {
                    owned = segment;
                }

                if (segment == _tail)
                {
                    break;
                }

                segment = segment.Next;
            }

            var data = new BufferSegment
            {
                Buffer = new ArraySegment<byte>(buffer),
                Owned = true
            };

            // We didn't own anything in the backlog so replace the entire list
            // with the same data, but into buffers we own
            if (owned == null)
            {
                _head = data;
            }
            else
            {
                // Otherwise append the new data to the Next of the first owned block
                owned.Next = data;
            }

            // Update tail to point to data
            _tail = data;
        }

        public ByteBuffer GetBuffer()
        {
            if (_head == null)
            {
                return new ByteBuffer(_singleItem);
            }

            return new ByteBuffer(_head, _tail);
        }
    }
}

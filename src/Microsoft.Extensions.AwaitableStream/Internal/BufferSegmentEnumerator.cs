using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Extensions.AwaitableStream.Internal
{
    public struct BufferSegmentEnumerator : IEnumerator<BufferSegment>
    {
        private BufferSegment _current;
        private BufferSegment _start;
        private BufferSegment _end;

        public BufferSegment Current => _current;
        object IEnumerator.Current => Current;

        public BufferSegmentEnumerator(BufferSegment start) : this(start, end: null) { }

        public BufferSegmentEnumerator(BufferSegment start, BufferSegment end)
        {
            _start = start;
            _end = end;
            _current = null;
        }

        public bool MoveNext()
        {
            if (_current == null)
            {
                _current = _start;
            }
            else if(_end != null && _current == _end)
            {
                // We're at the end.
                return false;
            }
            else
            {
                _current = _current.Next;
            }

            // If _current == null, it's because _start was null or _current.Next was null. Either way, we're done!
            return _current != null;
        }

        public void Reset()
        {
            _current = null;
        }

        public void Dispose()
        {
        }
    }
}

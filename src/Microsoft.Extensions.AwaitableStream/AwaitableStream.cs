using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AwaitableStream.Internal;

namespace Microsoft.Extensions.AwaitableStream
{
    /// <summary>
    /// Meant to be used with CopyToAsync for bufferless reads
    /// </summary>
    public class AwaitableStream : Stream
    {
        private static readonly Action _completed = () => { };

        private BufferChain _bufferChain = new BufferChain();

        private Action _continuation;
        private CancellationTokenRegistration _registration;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // Set when the first read happens
        private TaskCompletionSource<object> _initialRead = new TaskCompletionSource<object>();
        private Gate _readWaiting = new Gate();

        // Set when this stream is disposed
        private TaskCompletionSource<object> _producing = new TaskCompletionSource<object>();

        // Set when consumed is called during the continuation
        private bool _consumeCalled;

        internal bool HasData => _producing.Task.IsCompleted;

        public Task Completion => _producing.Task;

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;
            // Already cancelled to just throw
            cancellation.ThrowIfCancellationRequested();

            // Cancel the WriteAsync if the provided CancellationToken is fired.
            if (cancellationToken.CanBeCanceled && _registration == default(CancellationTokenRegistration))
            {
                // We can register the very first time write is called since the same token is passed into
                // CopyToAsync
                _registration = cancellationToken.Register(state => ((AwaitableStream)state).Cancel(), this);
            }

            // Wait for the first read operation.
            // This is important because the call to write async wants to call the continuation directly
            // so that the continuation can consume the buffers directly without worrying about where
            // ownership lies. Once the call to WriteAsync returns, the caller owns the buffer so it can't
            // be stashed away without copying.
            await _initialRead.Task;

            _bufferChain.Append(new ArraySegment<byte>(buffer, offset, count));

            // Call the continuation
            Complete();

            // Wait for the next read
            await _readWaiting;
            Debug.Assert(!_readWaiting.IsCompleted, "The gate didn't close behind us!");

            // Check that we haven't been cancelled
            cancellation.ThrowIfCancellationRequested();

            if (!_consumeCalled)
            {
                // Call it on the user's behalf
                Consumed(count);
            }

            // Reset the state
            _consumeCalled = false;
        }

        public StreamAwaitable ReadAsync() => new StreamAwaitable(this);

        /// <summary>
        /// Tell the awaitable stream how many bytes were consumed. This needs to be called from
        /// the continuation.
        /// </summary>
        /// <param name="count">Number of bytes consumed by the continuation</param>
        public void Consumed(int count)
        {
            _consumeCalled = true;
            _bufferChain.Consumed(count);
        }

        protected override void Dispose(bool disposing)
        {
            // Tell the consumer we're done
            if (_producing.TrySetResult(null))
            {
                // Open the read waiting gate
                _readWaiting.Open();

                // Trigger the callback so user code can react to this state change
                Complete();
            }

            _registration.Dispose();

            // Cancel all ongoing/future writes
            _cancellationTokenSource.Cancel();
        }

        public void Cancel()
        {
            // Tell the consumer we're cancelled
            if (_producing.TrySetCanceled())
            {
                // Open the read waiting gate
                _readWaiting.Open();

                // Trigger the callback so user code can react to this state change
                Complete();
            }

            // Cancel all ongoing/future writes
            _cancellationTokenSource.Cancel();
        }

        internal void OnCompleted(Action continuation)
        {
            if (_continuation == _completed ||
                Interlocked.CompareExchange(ref _continuation, continuation, null) == _completed)
            {
                continuation();
            }

            // For the first read, we open the _initialRead TCS, but NOT the readWaiting gate since we want that to block
            // until the second read.
            if(!_initialRead.TrySetResult(null))
            {
                // If we're here, it means initialRead was already RanToCompletion, so we should open the ReadWaiting gate instead.
                _readWaiting.Open();
            }
        }

        private void Complete()
        {
            (_continuation ?? Interlocked.CompareExchange(ref _continuation, _completed, null))?.Invoke();
        }

        internal ByteBuffer GetBuffer()
        {
            _continuation = null;
            return _bufferChain.GetBuffer();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

}

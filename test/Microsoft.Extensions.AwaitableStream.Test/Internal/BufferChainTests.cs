using System;
using System.Linq;
using Microsoft.Extensions.AwaitableStream.Internal;
using Xunit;

namespace Microsoft.Extensions.AwaitableStream.Test.Internal
{
    public class BufferChainTests
    {
        [Fact]
        public void OnInitialization_ItHasOneEmptyBufferSegment()
        {
            var chain = new BufferChain();
            var segments = chain.ToList();
            Assert.Same(chain.Head, chain.Tail);
            Assert.Equal(1, segments.Count);
            Assert.Equal(0, segments[0].Count);
            Assert.Null(segments[0].Buffer.Array);
            Assert.False(segments[0].Owned);
        }

        [Fact]
        public void FirstAppendReplacesExistingHead()
        {
            var payload = new byte[] { 1, 2, 3, 4 };
            var chain = CreateChain(payload);
            var initialHead = chain.Head;
            chain.Append(new ArraySegment<byte>(payload));
            Assert.Same(initialHead, chain.Head);
            Assert.Same(initialHead, chain.Tail);
            Assert.Equal(payload, chain.Head.Buffer.Array);
            Assert.False(chain.Head.Owned);
        }

        [Fact]
        public void SecondAppendAttachesNewSegment()
        {
            var payload1 = new byte[] { 1, 2, 3, 4 };
            var payload2 = new byte[] { 5, 6 };
            var chain = CreateChain(payload1, payload2);
            Assert.Equal(payload1, chain.Head.Buffer.Array);
            Assert.False(chain.Head.Owned);
            Assert.Equal(payload2, chain.Head.Next.Buffer.Array);
            Assert.False(chain.Head.Owned);
            Assert.Same(chain.Head.Next, chain.Tail);
        }

        [Fact]
        public void TruncateFromFirstSegment()
        {
            var payload1 = new byte[] { 1, 2, 3, 4 };
            var payload2 = new byte[] { 5, 6 };
            var payload3 = new byte[] { 7, 8, 9 };
            var chain = CreateChain(payload1, payload2, payload3);
            chain.Truncate(2);
            Assert.Equal(new byte[] { 3, 4 }, chain.Head.Buffer.ToArray());
        }

        private BufferChain CreateChain(params byte[][] payloads)
        {
            var chain = new BufferChain();
            foreach(var payload in payloads)
            {
                chain.Append(new ArraySegment<byte>(payload));
            }
            return chain;
        }
    }
}

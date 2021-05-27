using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// This class wraps the Sslstream so write can be done safely and concurrently on multiple threads.
    /// This was done so that chat rooms can send to the same user, possibly at the same time.
    /// </summary>
    public class ConcurrentStreamWriter : IDisposable
    {
        private readonly SslStream _stream;
        private readonly BlockingCollection<byte> _buffer;

        private readonly object _writeBufferLock;
        private Task _flusher;
        private volatile bool _disposed;

        public ConcurrentStreamWriter(SslStream stream)
        {
            _stream = stream;
            _buffer = new BlockingCollection<byte>(new ConcurrentQueue<byte>());
            _writeBufferLock = new object();
            _disposed = false;
        }

        private void FlushBuffer()
        {
            //keep writing to the stream, and block when the buffer is empty
            while (!_disposed)
                _stream.WriteByte(_buffer.Take());

            //when this instance has been disposed, flush any residue left in the ConcurrentStreamWriter and exit
            while (_buffer.TryTake(out byte b))
                _stream.WriteByte(b);
        }

        public void Write(byte[] data)
        {
            if (_disposed)
                throw new ObjectDisposedException("ConcurrentStreamWriter");

            lock (_writeBufferLock)
                foreach (byte b in data)
                    _buffer.Add(b);

            InitFlusher();
        }

        public void InitFlusher()
        {
            //safely create a new flusher task if one hasn't been created yet
            if (_flusher == null)
            {
                Task newFlusher = new Task(FlushBuffer);
                if (Interlocked.CompareExchange(ref _flusher, newFlusher, null) == null)
                    newFlusher.Start();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_flusher != null)
                _flusher.Wait();

            _buffer.Dispose();
        }
    }
}
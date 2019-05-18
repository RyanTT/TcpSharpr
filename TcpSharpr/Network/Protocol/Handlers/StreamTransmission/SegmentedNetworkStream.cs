using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.Threading;

namespace TcpSharpr.Network.Protocol.Handlers.StreamTransmission {
    public class SegmentedNetworkStream : MemoryStream, IDisposable {
        private long _readPosition;
        private long _writePosition;
        private AwaitableManualResetEvent _newDataAvailableEvent = new AwaitableManualResetEvent();

        public long DataAvailable {
            get {
                return Length - _readPosition;
            }
        }

        public bool DataComplete { get; private set; }
        public long PromisedLength { get; private set; }

        public void MarkAsComplete() {
            DataComplete = true;
        }

        public void SetPromisedLength(long promisedLength) {
            PromisedLength = promisedLength;
        } 

        public new void Dispose() {
            base.Dispose();
        }

        public async Task<long> WaitUntilDataAvailableAsync(int timeout) {
            return await WaitUntilDataAvailableAsync(TimeSpan.FromMilliseconds(timeout));
        }

        public async Task<long> WaitUntilDataAvailableAsync(TimeSpan timeout) {
            return await WaitUntilDataAvailableAsync(new CancellationTokenSource(timeout));
        }

        public async Task<long> WaitUntilDataAvailableAsync() {
            return await WaitUntilDataAvailableAsync(new CancellationTokenSource());
        }

        public async Task<long> WaitUntilDataAvailableAsync(CancellationTokenSource cancellationTokenSource) {
            try {
                await _newDataAvailableEvent.WaitAsync()
                    .WithCancellation(cancellationTokenSource.Token);
            } catch {
                return -1;
            }

            return DataAvailable;
        }

        public void SignalDataAvailable(bool isTransmissionDone) {
            _newDataAvailableEvent.Set(!isTransmissionDone);
        }

        // ------------- Read
        public override int Read(byte[] buffer, int offset, int count) {
            Position = _readPosition;
            var temp = base.Read(buffer, offset, count);
            _readPosition = Position;
            return temp;
        }

        public new Task<int> ReadAsync(byte[] buffer, int offset, int count) {
            return ReadAsync(buffer, offset, count, CancellationToken.None);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            Position = _readPosition;
            var temp =  base.ReadAsync(buffer, offset, count, cancellationToken);
            _readPosition = Position;
            return temp;
        }

        // ------------- Write
        public override void Write(byte[] buffer, int offset, int count) {
            Position = _writePosition;
            base.Write(buffer, offset, count);
            _writePosition = Position;
        }

        public new Task WriteAsync(byte[] buffer, int offset, int count) {
            return WriteAsync(buffer, offset, count, CancellationToken.None);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
            Position = _writePosition;
            var temp = base.WriteAsync(buffer, offset, count, cancellationToken);
            _writePosition = Position;
            return temp;
        }
    }
}

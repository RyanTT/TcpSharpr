using System;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.Network.Protocol.RemoteExceptions;
using TcpSharpr.Threading;

namespace TcpSharpr.Network {
    public class NetworkRequest : NetworkMessage {
        public object Result { get; private set; }

        private AwaitableManualResetEvent _responseReceivedEvent = new AwaitableManualResetEvent();

        public NetworkRequest(short networkId, string command, object[] parameters) : base(networkId, command, parameters) {
        }

        public override void Cancel() {
            _responseReceivedEvent.Cancel();
        }

        public async Task<T> GetResultAsync<T>(int timeout) {
            return await GetResultAsync<T>(TimeSpan.FromMilliseconds(timeout));
        }

        public async Task<T> GetResultAsync<T>(TimeSpan timeout) {
            return await GetResultAsync<T>(new CancellationTokenSource(timeout));
        }

        public async Task<T> GetResultAsync<T>() {
            return await GetResultAsync<T>(new CancellationTokenSource());
        }

        public async Task<T> GetResultAsync<T>(CancellationTokenSource cancellationTokenSource) {
            await _responseReceivedEvent
                .WaitAsync()
                .WithCancellation(cancellationTokenSource.Token)
                .WithCancellation(_remoteExceptionCancelTokenSource.Token);

            return (T)Result;
        }

        internal override void SetRemoteException(RemoteExecutionException exception) {
            RemoteException = exception;

            _responseReceivedEvent.SetException(exception);
            _remoteExceptionCancelTokenSource.Cancel();
        }

        internal void SetResult(object result) {
            Result = result;
            _responseReceivedEvent.Set();
        }
    }
}

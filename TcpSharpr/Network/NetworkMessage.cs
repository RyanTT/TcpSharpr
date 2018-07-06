using System;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.Network.Events;
using TcpSharpr.Network.Protocol.RemoteExceptions;
using TcpSharpr.Threading;

namespace TcpSharpr.Network {
    public class NetworkMessage : IDisposable {
        public short NetworkId { get; protected set; }
        public object[] CommandParameters { get; protected set; }
        public string CommandName { get; protected set; }
        public NetworkMessageStatus Status { get; protected set; } = NetworkMessageStatus.None;
        public bool ReceiveUpdates { get; protected set; }
        public RemoteExecutionException RemoteException { get; protected set; }
        public double NetworkTransmissionProgress { get; protected set; }

        public event EventHandler<NetworkTransmissionProgressChangedArgs> OnNetworkTransmissionProgressChanged;
        public event EventHandler<NetworkTransmissionFinishedArgs> OnNetworkTransmissionFinished;

        protected CancellationTokenSource _remoteExceptionCancelTokenSource = new CancellationTokenSource();
        private AwaitableManualResetEvent _acknowledgedEvent;

        public NetworkMessage(short networkId, string command, object[] parameters) {
            NetworkId = networkId;
            CommandParameters = parameters;
            CommandName = command;
            _acknowledgedEvent = new AwaitableManualResetEvent();
        }

        public virtual void Cancel() {
            _acknowledgedEvent.Cancel();
        }

        public void Dispose() {
            ReceiveUpdates = false;
        }

        public async Task WaitUntilAcknowledgedAsync(int timeout) {
            await WaitUntilAcknowledgedAsync(TimeSpan.FromMilliseconds(timeout));
        }

        public async Task WaitUntilAcknowledgedAsync(TimeSpan timeout) {
            await WaitUntilAcknowledgedAsync(new CancellationTokenSource(timeout));
        }

        public async Task WaitUntilAcknowledgedAsync() {
            await WaitUntilAcknowledgedAsync(new CancellationTokenSource());
        }

        public async Task WaitUntilAcknowledgedAsync(CancellationTokenSource cancellationTokenSource) {
            await _acknowledgedEvent
                .WaitAsync()
                .WithCancellation(cancellationTokenSource.Token);
        }

        internal void SetStatus(NetworkMessageStatus status) {
            Status = status;

            if (status == NetworkMessageStatus.Acknowledged) {
                _acknowledgedEvent.Set();
            }
        }

        internal virtual void SetRemoteException(RemoteExecutionException exception) {
            RemoteException = exception;
            _remoteExceptionCancelTokenSource.Cancel();
        }

        internal void SetNetworkTransmissionProgress(double progress) {
            NetworkTransmissionProgress = progress;

            OnNetworkTransmissionProgressChanged?.Invoke(this, new NetworkTransmissionProgressChangedArgs(progress));

            if (progress >= 1) {
                OnNetworkTransmissionFinished?.Invoke(this, new NetworkTransmissionFinishedArgs());
            }
        }

        [Flags]
        public enum NetworkMessageStatus {
            None = 1,
            Acknowledged = 2,
            RemoteException = 4
        }
    }
}

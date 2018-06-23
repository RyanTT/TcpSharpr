using System;
using System.Threading;
using TcpSharpr.Network.Protocol.RemoteExceptions;

namespace TcpSharpr.Network {
    public class NetworkMessage : IDisposable {
        public short NetworkId { get; protected set; }
        public object[] CommandParameters { get; protected set; }
        public string CommandName { get; protected set; }
        public NetworkMessageStatus Status { get; protected set; }
        public bool ReceiveUpdates { get; protected set; }
        public RemoteExecutionException RemoteException { get; protected set; }

        protected CancellationTokenSource _remoteExceptionCancelTokenSource = new CancellationTokenSource();

        public NetworkMessage(short networkId, string command, object[] parameters) {
            NetworkId = networkId;
            CommandParameters = parameters;
            CommandName = command;
        }

        public virtual void Cancel() {

        }

        public void Dispose() {
            ReceiveUpdates = false;
        }

        internal void SetStatus(NetworkMessageStatus status) {
            Status = status;
        }

        internal virtual void SetRemoteException(RemoteExecutionException exception) {
            RemoteException = exception;
            _remoteExceptionCancelTokenSource.Cancel();
        }

        [Flags]
        public enum NetworkMessageStatus {
            Acknowledged = 0,
            RemoteException = 1
        }
    }
}

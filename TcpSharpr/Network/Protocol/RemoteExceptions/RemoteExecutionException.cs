using System;
using System.Runtime.Serialization;

namespace TcpSharpr.Network.Protocol.RemoteExceptions {
    public class RemoteExecutionException : Exception {
        protected RemoteExecutionException(SerializationInfo info, StreamingContext context) : base(info, context) {

        }

        public RemoteExecutionException(RemoteExecutionExceptionNetworkModel remoteData) : base("A exception was thrown during the execution on the remote host. View <RemoteException...> fields for more information.") {
            RemoteExceptionTypeName = remoteData.RemoteExceptionTypeName;
            RemoteExceptionMessage = remoteData.RemoteExceptionMessage;
            RemoteExceptionStack = remoteData.RemoteExceptionStack;
        }

        public string RemoteExceptionTypeName { get; set; }
        public string RemoteExceptionMessage { get; set; }
        public string RemoteExceptionStack { get; set; }
    }
}

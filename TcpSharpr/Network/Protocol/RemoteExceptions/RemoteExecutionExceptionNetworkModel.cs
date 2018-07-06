using System;

namespace TcpSharpr.Network.Protocol.RemoteExceptions {
    [Serializable]
    public class RemoteExecutionExceptionNetworkModel {
        public string RemoteExceptionTypeName { get; set; }
        public string RemoteExceptionMessage { get; set; }
        public string RemoteExceptionStack { get; set; }

        public static RemoteExecutionExceptionNetworkModel FromException(Exception ex) {
            return new RemoteExecutionExceptionNetworkModel {
                RemoteExceptionTypeName = ex?.GetType().Name,
                RemoteExceptionMessage = ex.Message,
                RemoteExceptionStack = ex.StackTrace
            };
        }
    }
}

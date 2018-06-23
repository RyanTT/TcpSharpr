using System;
using System.Net;

namespace TcpSharpr.Network.Events {
    public class ConnectedEventArgs : EventArgs {
        public EndPoint Endpoint { get; private set; }

        public ConnectedEventArgs(EndPoint endPoint) {
            Endpoint = endPoint;
        }
    }
}

using System;

namespace TcpSharpr.Network.Events {
    public class ClientDisconnectedEventArgs : EventArgs {
        public NetworkClient NetworkClient { get; private set; }

        public ClientDisconnectedEventArgs(NetworkClient networkClient) {
            NetworkClient = networkClient;
        }
    }
}

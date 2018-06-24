using System;

namespace TcpSharpr.Network.Events {
    public class ClientConnectedEventArgs : EventArgs {
        public NetworkClient NetworkClient { get; private set; }

        public ClientConnectedEventArgs(NetworkClient networkClient) {
            NetworkClient = networkClient;
        }
    }
}

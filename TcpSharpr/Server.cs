using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network;
using TcpSharpr.Network.Events;
using TcpSharpr.Threading;

namespace TcpSharpr {
    public class Server {
        public IPEndPoint ListeningIpEndposhort { get; private set; }
        public CommandManager CommandManager { get; private set; }
        public NetworkClient[] ConnectedClients { get { return _connectedClients.ToArray(); } }
        public EventHandler<ClientConnectedEventArgs> OnNetworkClientConnected;
        public EventHandler<ClientDisconnectedEventArgs> OnNetworkClientDisconnected;

        private CancellationTokenSource _serverStopTokenSource;
        private Socket _listeningSocket;
        private List<NetworkClient> _connectedClients;
        private Task _serverWorkerTask;

        public Server(IPEndPoint ipEndposhort) {
            ListeningIpEndposhort = ipEndposhort;
            CommandManager = new CommandManager();

            _connectedClients = new List<NetworkClient>();

            _serverStopTokenSource = new CancellationTokenSource();
            _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listeningSocket.Bind(ipEndposhort);
            _listeningSocket.Listen(10);
        }

        public void Start() {
            Start(_serverStopTokenSource);
        }

        public void Start(CancellationTokenSource cancellationTokenSource) {
            _serverWorkerTask = Task.Run(async () => {
                _serverStopTokenSource = cancellationTokenSource;

                while (!cancellationTokenSource.IsCancellationRequested) {
                    Socket acceptedSocket = await _listeningSocket.AcceptAsync().WithCancellation(_serverStopTokenSource.Token);
                    NetworkClient networkClient = new NetworkClient(acceptedSocket, _serverStopTokenSource, CommandManager);

                    networkClient.RunsAt(this);

                    networkClient.OnConnected += NetworkClient_OnConnected;
                    networkClient.OnDisconnected += NetworkClient_OnDisconnected;

                    Task receiveTask = networkClient.ReceiveAsync();
                }
            });
        }

        private void NetworkClient_OnConnected(object sender, Network.Events.ConnectedEventArgs e) {
            lock (_connectedClients) {
                _connectedClients.Add(sender as NetworkClient);
            }

            OnNetworkClientConnected?.Invoke(this, new ClientConnectedEventArgs(sender as NetworkClient));
        }

        private void NetworkClient_OnDisconnected(object sender, Network.Events.DisconnectedEventArgs e) {
            lock (_connectedClients) {
                _connectedClients.Remove(sender as NetworkClient);
            }

            OnNetworkClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(sender as NetworkClient));
        }

        public void Stop() {
            _serverStopTokenSource.Cancel();

            try {
                _listeningSocket.Close();
            } catch { }
        }
    }
}

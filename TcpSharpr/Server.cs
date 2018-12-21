using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network;
using TcpSharpr.Network.Events;
using TcpSharpr.Threading;

namespace TcpSharpr {
    public class Server {
        public IPEndPoint ListeningIpEndpoint { get; private set; }
        public CommandManager CommandManager { get; private set; }

        public bool IsRunning => !_serverStopTokenSource.IsCancellationRequested;
        public IReadOnlyList<NetworkClient> ConnectedClients => _connectedClients;
        public EventHandler<ClientConnectedEventArgs> OnNetworkClientConnected;
        public EventHandler<ClientDisconnectedEventArgs> OnNetworkClientDisconnected;
        public EventHandler<StatusChangedEventArgs> OnStatusChanged;

        private CancellationTokenSource _serverStopTokenSource;
        private readonly Socket _listeningSocket;
        private readonly List<NetworkClient> _connectedClients;
        private readonly SymmetricAlgorithm _algorithm;
        private Task _serverWorkerTask;

        public Server(IPEndPoint ipEndpoint, SymmetricAlgorithm algorithm = null) {
            ListeningIpEndpoint = ipEndpoint;
            _algorithm = algorithm;
            CommandManager = new CommandManager();

            _connectedClients = new List<NetworkClient>();
            _serverStopTokenSource = new CancellationTokenSource();
            _listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start() {
            Start(_serverStopTokenSource);
        }

        public void Start(CancellationTokenSource cancellationTokenSource) {
            _serverWorkerTask = Task.Run(async () => {
                _serverStopTokenSource = cancellationTokenSource;
                _listeningSocket.Bind(ListeningIpEndpoint);
                _listeningSocket.Listen(10);
                OnStatusChanged?.Invoke(this, new StatusChangedEventArgs(IsRunning));

                while (!cancellationTokenSource.IsCancellationRequested) {
                    Socket acceptedSocket = await _listeningSocket.AcceptAsync().WithCancellation(_serverStopTokenSource.Token);
                    NetworkClient networkClient = new NetworkClient(acceptedSocket, _serverStopTokenSource, CommandManager, _algorithm);

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
            OnStatusChanged?.Invoke(this, new StatusChangedEventArgs(IsRunning));
            try {
                _listeningSocket.Close();
            } catch { }
        }

        public void DisconnectAll() {
            foreach (var client in ConnectedClients) {
                client.Disconnect();
            }
        }
    }
}

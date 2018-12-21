using System;
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
    public class Client : INetworkSender {
        public CommandManager CommandManager { get; }
        public IPEndPoint RemoteIpEndpoint { get; }
        public bool ReconnectOnDisconnect { get; set; } = true;
        public bool ReconnectOnConnectFailure { get; set; } = false;
        public bool IsConnected { get; private set; }
        public NetworkClient NetworkClient { get; private set; }

        public event EventHandler<ConnectedEventArgs> OnNetworkClientConnected;
        public event EventHandler<DisconnectedEventArgs> OnNetworkClientDisconnected;

        private readonly SymmetricAlgorithm _algorithm;
        private CancellationTokenSource _clientCancellationToken;
        private Task _clientWorkerTask;

        public Client(IPEndPoint ipEndpoint, SymmetricAlgorithm algorithm = null) {
            RemoteIpEndpoint = ipEndpoint;
            _algorithm = algorithm;
            CommandManager = new CommandManager();
        }

        public async Task<bool> ConnectAsync() {
            _clientCancellationToken = new CancellationTokenSource();

            if (ReconnectOnConnectFailure) {
                // Attempt connecting in a loop until asked to stop or on success
                try {
                    while (!await AttemptConnectAsync(false)) await Task.Delay(1000).WithCancellation(_clientCancellationToken.Token);
                    return true;
                } catch (OperationCanceledException) {
                    return false;
                }
            }

            // Normal attempt at connecting, no retries
            return await AttemptConnectAsync(false);
        }

        public void Disconnect() {
            _clientCancellationToken?.Cancel();
        }

        public async Task<NetworkMessage> SendAsync(string command, params object[] args) {
            return await NetworkClient.SendAsync(command, args);
        }

        public async Task<NetworkRequest> SendRequestAsync(string command, params object[] args) {
            return await NetworkClient.SendRequestAsync(command, args);
        }

        internal async Task<bool> AttemptConnectAsync(bool isDedicatedContext) {
            try {
                if (_clientCancellationToken != null && _clientCancellationToken.IsCancellationRequested) {
                    return false;
                }

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(RemoteIpEndpoint);

                NetworkClient = new NetworkClient(socket, _clientCancellationToken, CommandManager, _algorithm);
                NetworkClient.OnDisconnected += NetworkClient_OnDisconnected;

#pragma warning disable CS4014
                Task.Run(() => OnNetworkClientConnected?.Invoke(this, new ConnectedEventArgs(RemoteIpEndpoint)));
#pragma warning restore CS4014

                if (!isDedicatedContext) {
                    _clientWorkerTask = Task.Run(async () => await NetworkClient.ReceiveAsync());
                } else {
                    await NetworkClient.ReceiveAsync();
                }

                return IsConnected = true;
            } catch (SocketException) {
                return false;
            }
        }

        private async void NetworkClient_OnDisconnected(object sender, Network.Events.DisconnectedEventArgs e) {
#pragma warning disable CS4014
            Task.Run(() => OnNetworkClientDisconnected?.Invoke(this, new DisconnectedEventArgs()));
#pragma warning restore CS4014

            IsConnected = false;

            try {
                while (!await AttemptConnectAsync(true)) await Task.Delay(1000).WithCancellation(_clientCancellationToken.Token);
            } catch (OperationCanceledException ex) {
                // Catch, consume, adapt, overcome
            }
        }
    }
}

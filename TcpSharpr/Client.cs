using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.Methodshorteraction;
using TcpSharpr.Network;

namespace TcpSharpr {
    public class Client {
        public CommandManager CommandManager { get; private set; }
        public IPEndPoint RemoteIpEndpoint { get; private set; }
        public bool ReconnectOnDisconnect { get; set; } = true;
        public bool IsConnected { get; private set; } = false;

        public NetworkClient _networkClient { get; private set; }
        private CancellationTokenSource _serverStopTokenSource;
        private Task _clientWorkerTask;

        public Client(IPEndPoint ipEndpoint) {
            RemoteIpEndpoint = ipEndpoint;
            CommandManager = new CommandManager();
        }

        public async Task<bool> ConnectAsync() {
            _serverStopTokenSource = new CancellationTokenSource();

            return await AttemptConnectAsync(false);
        }

        public void Disconnect() {
            _serverStopTokenSource?.Cancel();
        }

        public async Task<NetworkMessage> SendAsync(string command, params object[] args) {
            return await _networkClient.SendAsync(command, args);
        }

        public async Task<NetworkRequest> SendRequestAsync(string command, params object[] args) {
            return await _networkClient.SendRequestAsync(command, args);
        }

        internal async Task<bool> AttemptConnectAsync(bool isDedicatedContext) {
            try {
                if (_serverStopTokenSource != null && _serverStopTokenSource.IsCancellationRequested) {
                    return await Task.FromResult(false);
                }

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(RemoteIpEndpoint);

                _networkClient = new NetworkClient(socket, _serverStopTokenSource, CommandManager);
                _networkClient.OnDisconnected += NetworkClient_OnDisconnected;

                if (!isDedicatedContext) {
                    _clientWorkerTask = Task.Run(async () => await _networkClient.ReceiveAsync());
                } else {
                    await _networkClient.ReceiveAsync();
                }

                return IsConnected = true;
            } catch (SocketException) {
                return false;
            }
        }

        private async void NetworkClient_OnDisconnected(object sender, Network.Events.DisconnectedEventArgs e) {
            IsConnected = false;
            while (!await AttemptConnectAsync(true)) await Task.Delay(1000);
        }
    }
}

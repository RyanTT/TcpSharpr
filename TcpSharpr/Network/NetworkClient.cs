using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network.Protocol;
using TcpSharpr.Threading;

namespace TcpSharpr.Network {
    public class NetworkClient : INetworkSender {
        public const int RECEIVE_BUFFER_SIZE = 1048576; //1024;

        protected Socket _socket;
        protected CancellationTokenSource _parentStopTokenSource;
        protected CancellationTokenSource _internalStopTokenSource;
        protected PacketFormatter _packetFormatter;
        protected MessageManager _messageManager;
        protected CommandManager _commandManager;
        protected IMessageSerializer _messageSerializer;
        protected Server _parent;

        public event EventHandler<Events.ConnectedEventArgs> OnConnected;
        public event EventHandler<Events.DisconnectedEventArgs> OnDisconnected;
        
        public object Tag { get; set; }

        public MessageManager MessageManager => _messageManager;

        public EndPoint Endpoint => _socket.RemoteEndPoint;

        public NetworkClient(Socket socket, CancellationTokenSource stopTokenSource, CommandManager commandManager, SymmetricAlgorithm encryptionAlgorithm) {
            _socket = socket;
            _parentStopTokenSource = stopTokenSource;
            _internalStopTokenSource = new CancellationTokenSource();
            
            if (encryptionAlgorithm == null) {
                _packetFormatter = new PacketFormatter();
            } else {
                _packetFormatter = new SymmetricEncryptionPacketFormatter(encryptionAlgorithm);
            }

            _commandManager = commandManager;
            _messageManager = new MessageManager(_commandManager, this);
            _messageSerializer = new BinaryFormatterSerializer();
        }

        internal void RunsAt(Server server) {
            _parent = server;
        }

        public Server GetParent() {
            return _parent;
        }

        public async Task ReceiveAsync() {
            OnConnected?.Invoke(this, new Events.ConnectedEventArgs(_socket.RemoteEndPoint));

            try {
                List<byte> totalReceiveBuffer = new List<byte>();

                while (!_parentStopTokenSource.IsCancellationRequested) {
                    byte[] receiveBuffer = new byte[RECEIVE_BUFFER_SIZE];
                    int bytesReceived = await _socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), SocketFlags.None).WithCancellation(_parentStopTokenSource.Token);

                    if (bytesReceived == 0) {
                        throw new SocketException();
                    }

                    // Add received bytes to total buffer
                    totalReceiveBuffer.AddRange(receiveBuffer.Take(bytesReceived));

                    byte[][] receivedPackets = _packetFormatter.TryReadPackets(totalReceiveBuffer);

                    foreach (var packet in receivedPackets) {
                        await OnPacketReceived(packet);
                    }
                }
            } catch {
                await OnShutdown();
            }
        }

        public async Task<NetworkMessage> SendAsync(string command, params object[] args) {
            return await _messageManager.SendNetworkMessageAction(command, args);
        }

        public async Task<NetworkRequest> SendRequestAsync(string command, params object[] args) {
            return await _messageManager.SendNetworkMessageRequest(command, args);
        }

        public void Disconnect() {
            _internalStopTokenSource?.Cancel();
            _parentStopTokenSource?.Cancel();
        }

        internal async Task SendInternalAsync(NetworkPacket networkPacket) {
            // Serialize
            byte[] packet = _messageSerializer.SerializeNetworkMessage(networkPacket);

            // Wrap for network
            packet = _packetFormatter.PreparePacketForNetwork(packet);

            // Send
            await _socket.SendAsync(new ArraySegment<byte>(packet), SocketFlags.None);
        }

        internal async Task OnShutdown() {
            if (!_internalStopTokenSource.IsCancellationRequested) {
                _internalStopTokenSource.Cancel();
            }

            try {
                _socket.Disconnect(false);
                //_socket.Close();
            } catch { }

            OnDisconnected?.Invoke(this, new Events.DisconnectedEventArgs());

            await _messageManager.OnShutdown();
        }

        private async Task OnPacketReceived(byte[] packet) {
            try {
                var networkMessage = _messageSerializer.DeserializeNetworkMessage(packet);
                await _messageManager.OnNetworkMessage(networkMessage);
            } catch { }
        }
    }
}

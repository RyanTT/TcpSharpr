using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network.Protocol;
using TcpSharpr.Threading;

namespace TcpSharpr.Network {
    public class NetworkClient {
        public const short RECEIVE_BUFFER_SIZE = 1024;

        protected Socket _socket;
        protected CancellationTokenSource _parentStopTokenSource;
        protected CancellationTokenSource _internalStopTokenSource;
        protected PacketFormatter _packetFormatter;
        protected MessageManager _messageManager;
        protected CommandManager _commandManager;
        protected IMessageSerializer _messageSerializer;

        public event EventHandler<Events.ConnectedEventArgs> OnConnected;
        public event EventHandler<Events.DisconnectedEventArgs> OnDisconnected;
        
        public NetworkClient(Socket socket, CancellationTokenSource stopTokenSource, CommandManager commandManager) {
            _socket = socket;
            _parentStopTokenSource = stopTokenSource;
            _internalStopTokenSource = new CancellationTokenSource();

            _packetFormatter = new PacketFormatter();
            _commandManager = commandManager;
            _messageManager = new MessageManager(_commandManager, this);
            _messageSerializer = new BinaryFormatterSerializer();
        }

        public async Task ReceiveAsync() {
            OnConnected?.Invoke(this, new Events.ConnectedEventArgs(_socket.RemoteEndPoint));

            try {
                List<byte> totalReceiveBuffer = new List<byte>();

                while (!_parentStopTokenSource.IsCancellationRequested) {
                    byte[] receiveBuffer = new byte[RECEIVE_BUFFER_SIZE];
                    int bytesReceived = await _socket.ReceiveAsync(receiveBuffer, SocketFlags.None).WithCancellation(_parentStopTokenSource.Token);

                    if (bytesReceived == 0) {
                        throw new SocketException();
                    }

                    // Copy actually received bytes
                    byte[] receivedData = new byte[bytesReceived];
                    Array.Copy(receiveBuffer, receivedData, bytesReceived);

                    // Add received bytes to total buffer
                    totalReceiveBuffer.AddRange(receivedData);

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
        }

        internal async Task SendInternalAsync(NetworkPacket networkPacket) {
            // Serialize
            byte[] packet = _messageSerializer.SerializeNetworkMessage(networkPacket);

            // Wrap for network
            packet = _packetFormatter.PreparePacketForNetwork(packet);

            // Send
            await _socket.SendAsync(packet, SocketFlags.None);
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

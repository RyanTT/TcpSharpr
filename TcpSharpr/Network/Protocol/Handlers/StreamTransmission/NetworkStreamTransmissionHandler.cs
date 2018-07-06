using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TcpSharpr.Network.Protocol.Handlers.StreamTransmission;

namespace TcpSharpr.Network.Protocol.Handlers {
    public class NetworkStreamTransmissionHandler {
        private static Random _randomIdGenerator = new Random();
        private MessageManager _messageManager;
        private NetworkClient _networkClient;
        private ConcurrentDictionary<int, SegmentedNetworkStream> _trackedStreams;

        public const string PROTOCOL_BEGIN_TRANSMISSION = "NST_BeginTransmission";
        public const string PROTOCOL_TRANSMIT_STREAM_PART = "NST_TransmitPart";
        public const string PROTOCOL_TRANSMIT_STREAM_END = "NST_TransmitEnd";
        public const int PROTOCOL_BUFFER_SIZE = 1048576;

        public NetworkStreamTransmissionHandler(NetworkClient networkClient, MessageManager messageManager) {
            _messageManager = messageManager;
            _networkClient = networkClient;
            _trackedStreams = new ConcurrentDictionary<int, SegmentedNetworkStream>();

            _messageManager.CommandManager.RegisterAsyncCommand(PROTOCOL_BEGIN_TRANSMISSION, new Func<NetworkPacket, Task>(ProtocolBeginTransmission));
            _messageManager.CommandManager.RegisterAsyncCommand(PROTOCOL_TRANSMIT_STREAM_PART, new Func<int, byte[], bool, Task>(ProtocolReceiveTransmissionPart));
            _messageManager.CommandManager.RegisterAsyncCommand(PROTOCOL_TRANSMIT_STREAM_END, new Func<int, Task>(ProtocolCloseStream));
        }

        public async Task OnNetworkMessageSend(MessageManager.PreparedNetworkMessage preparedNetworkMessage) {
            var networkPacket = preparedNetworkMessage.NetworkPacket;

            // Get network message
            var streamsToTransmitDictionary = new Dictionary<int, Stream>();

            List<object> filteredParameters = new List<object>();
            foreach (var parameter in networkPacket.CommandParameters) {
                var parameterType = parameter?.GetType();

                if (parameterType != null && typeof(Stream).IsAssignableFrom(parameterType)) {
                    int streamId = _randomIdGenerator.Next(int.MinValue, int.MaxValue);

                    filteredParameters.Add(new NetworkStreamPlaceholder {
                        Id = streamId,
                        PromisedLength = (parameter as Stream).Length
                    });

                    streamsToTransmitDictionary.Add(streamId, parameter as Stream);
                } else {
                    filteredParameters.Add(parameter);
                }
            }

            NetworkPacket newNetworkPacket = new NetworkPacket {
                CommandName = networkPacket.CommandName,
                CommandParameters = filteredParameters.ToArray(),
                NetworkId = networkPacket.NetworkId,
                PacketType = networkPacket.PacketType,
                RequiresAcknowledgement = true
            };

            // Send initial request that this will be a sliced up stream communication
            await (await _networkClient.SendAsync(PROTOCOL_BEGIN_TRANSMISSION, newNetworkPacket)).WaitUntilAcknowledgedAsync(TimeSpan.FromSeconds(10));

            // Streams to transmit
            var streamsToTransmit = networkPacket.CommandParameters.Where(x => typeof(Stream).IsAssignableFrom(x?.GetType())).ToList();

            // Total bytes to send
            long totalBytesToTransmit = 0;
            long totalBytesTransmitted = 0;

            foreach (var streamToTransmit in streamsToTransmitDictionary)
                totalBytesToTransmit += streamToTransmit.Value.Length;


            // Send streams behind each other
            foreach (var streamToTransmit in streamsToTransmitDictionary) {
                while (streamToTransmit.Value.Length - streamToTransmit.Value.Position > 0) {
                    // Send PROTOCOL_BUFFER_SIZEed chunks of the stream as seperate packets
                    byte[] buffer = new byte[PROTOCOL_BUFFER_SIZE];
                    int bytesRead = await streamToTransmit.Value.ReadAsync(buffer, 0, buffer.Length);

                    var bufferToSend = buffer.Take(bytesRead).ToArray();
                    var isEnd = streamToTransmit.Value.Length - streamToTransmit.Value.Position == 0;
                    var networkMessage = await _networkClient.SendAsync(PROTOCOL_TRANSMIT_STREAM_PART, streamToTransmit.Key, bufferToSend, isEnd);

                    try {
                        await networkMessage.WaitUntilAcknowledgedAsync(TimeSpan.FromSeconds(1));
                    } catch { }

                    totalBytesTransmitted += bufferToSend.Length;

                    preparedNetworkMessage.NetworkMessage.SetNetworkTransmissionProgress((double)totalBytesTransmitted / totalBytesToTransmit);
                }

                // Stream is empty, send completed signal
                await _networkClient.SendAsync(PROTOCOL_TRANSMIT_STREAM_END, streamToTransmit.Key);
            }
        }

        private async Task ProtocolBeginTransmission(NetworkPacket networkPacket) {
            List<object> filteredParameters = new List<object>();

            foreach (var parameter in networkPacket.CommandParameters) {
                var parameterType = parameter?.GetType();

                if (parameterType != null && typeof(NetworkStreamPlaceholder).IsAssignableFrom(parameterType)) {
                    var streamToTransmit = parameter as NetworkStreamPlaceholder;
                    var stream = new SegmentedNetworkStream();
                    var id = streamToTransmit.Id;

                    _trackedStreams.TryAdd(id, stream);
                    filteredParameters.Add(stream);

                    // Set promised length
                    stream.SetPromisedLength(streamToTransmit.PromisedLength);
                } else {
                    filteredParameters.Add(parameter);
                }
            }

            NetworkPacket localInvocationNetworkPacket = new NetworkPacket {
                CommandName = networkPacket.CommandName,
                CommandParameters = filteredParameters.ToArray(),
                NetworkId = networkPacket.NetworkId,
                PacketType = networkPacket.PacketType,
                RequiresAcknowledgement = true
            };

            await _messageManager.OnNetworkMessage(localInvocationNetworkPacket);
        }

        private async Task ProtocolReceiveTransmissionPart(int streamId, byte[] part, bool isContentEnd) {
            if (_trackedStreams.ContainsKey(streamId)) {
                var stream = _trackedStreams[streamId];

                await stream.WriteAsync(part, 0, part.Length);
                await stream.FlushAsync();

                if (isContentEnd) {
                    stream.MarkAsComplete();
                }

                stream.SignalDataAvailable(isContentEnd);
            }
        }
    
        private async Task ProtocolCloseStream(int streamId) {
            if (_trackedStreams.ContainsKey(streamId)) {
                var stream = _trackedStreams[streamId];

                stream.MarkAsComplete();
            }

            // Remove stream
            _trackedStreams.TryRemove(streamId, out var ignored);

            await Task.CompletedTask;
        }

        [Serializable]
        public class NetworkStreamPlaceholder {
            public int Id { get; set; }
            public long PromisedLength { get; set; }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network.Protocol.Handlers;
using TcpSharpr.Network.Protocol.RemoteExceptions;

namespace TcpSharpr.Network.Protocol {
    public class MessageManager {
        private static readonly Random _randomIdGenerator = new Random();
        private NetworkClient _networkClient;
        private NetworkStreamTransmissionHandler _networkStreamTransmissionHandler;
        private ConcurrentBag<NetworkMessage> _trackedSentMessages;

        public CommandManager CommandManager { get; private set; }

        public MessageManager(CommandManager commandManager, NetworkClient networkClient) {
            _networkClient = networkClient;
            CommandManager = commandManager;
            _trackedSentMessages = new ConcurrentBag<NetworkMessage>();
            _networkStreamTransmissionHandler = new NetworkStreamTransmissionHandler(networkClient, this);
        }

        public NetworkMessage GetTrackedNetworkMessage(short id) {
            lock (_trackedSentMessages) {
                return _trackedSentMessages.Where(x => x.NetworkId == id).FirstOrDefault();
            }
        }

        public async Task OnNetworkMessage(NetworkPacket networkMessage) {
            if (networkMessage.RequiresAcknowledgement) {
                await SendNetworkMessageAcknowledgement(networkMessage.NetworkId);
            }

            if (networkMessage.PacketType == NetworkPacket.Type.Action) {
                await FromNetworkHandleAction(networkMessage);
            } else if (networkMessage.PacketType == NetworkPacket.Type.Request) {
                await FromNetworkHandleRequest(networkMessage);
            } else if (networkMessage.PacketType == NetworkPacket.Type.Response) {
                await FromNetworkHandleResponse(networkMessage);
            } else if (networkMessage.PacketType == NetworkPacket.Type.PackageAcknowledged) {
                await FromNetworkHandleAcknowledge(networkMessage);
            } else if (networkMessage.PacketType == NetworkPacket.Type.RemoteException) {
                await FromNetworkHandleRemoteException(networkMessage);
            }
        }

        #region Message Handling
        public async Task FromNetworkHandleAction(NetworkPacket networkPacket) {
            try {
                await CommandManager.InvokeCommand(_networkClient, networkPacket.CommandName, networkPacket.CommandParameters, false);
            } catch (Exception ex) {
                await SendNetworkMessageRemoteException(networkPacket.NetworkId, RemoteExecutionExceptionNetworkModel.FromException(ex));
            }
        }

        public async Task FromNetworkHandleRequest(NetworkPacket networkPacket) {
            try {
                var result = await CommandManager.InvokeCommand(_networkClient, networkPacket.CommandName, networkPacket.CommandParameters, true);
                await SendNetworkMessageResponse(networkPacket.NetworkId, result);
            } catch (Exception ex) {
                await SendNetworkMessageRemoteException(networkPacket.NetworkId, RemoteExecutionExceptionNetworkModel.FromException(ex));
            }
        }

        public async Task FromNetworkHandleResponse(NetworkPacket networkPacket) {
            try {
                lock (_trackedSentMessages) {
                    foreach (var trackedMessage in _trackedSentMessages) {
                        if (trackedMessage.NetworkId == networkPacket.NetworkId && trackedMessage is NetworkRequest request) {
                            request.SetResult(networkPacket.CommandParameters[0]);
                        }
                    }
                }
            } catch { }

            await Task.CompletedTask;
        }

        public async Task FromNetworkHandleAcknowledge(NetworkPacket networkPacket) {
            try {
                lock (_trackedSentMessages) {
                    foreach (var trackedMessage in _trackedSentMessages) {
                        if (trackedMessage.NetworkId == networkPacket.NetworkId) {
                            trackedMessage.SetStatus(NetworkMessage.NetworkMessageStatus.Acknowledged);
                            break;
                        }
                    }
                }
            } catch { }

            await Task.CompletedTask;
        }

        public async Task FromNetworkHandleRemoteException(NetworkPacket networkPacket) {
            try {
                lock (_trackedSentMessages) {
                    foreach (var trackedMessage in _trackedSentMessages) {
                        if (trackedMessage.NetworkId == networkPacket.NetworkId) {
                            var remoteExecutionExceptionNetworkModel = (RemoteExecutionExceptionNetworkModel)networkPacket.CommandParameters[0];

                            trackedMessage.SetRemoteException(new RemoteExecutionException(remoteExecutionExceptionNetworkModel));
                            trackedMessage.SetStatus(NetworkMessage.NetworkMessageStatus.RemoteException);
                        }
                    }
                }
            } catch { }

            await Task.CompletedTask;
        }
        #endregion

        #region Message Sending
        public PreparedNetworkMessage PrepareNetworkMessageWithId(short networkId, NetworkPacket.Type packetType, bool requiresAcknowledgement, string command, params object[] parameters) {
            var networkPacket = new NetworkPacket {
                NetworkId = networkId,
                CommandName = command,
                CommandParameters = parameters,
                RequiresAcknowledgement = requiresAcknowledgement,
                PacketType = packetType
            };
            
            return new PreparedNetworkMessage(_networkClient, networkPacket, _networkStreamTransmissionHandler);
        }

        public PreparedNetworkMessage PrepareNetworkMessage(NetworkPacket.Type packetType, bool requiresAcknowledgement, string command, params object[] parameters) {
            short generatedId = 0;

            while (true) {
                generatedId = (short)_randomIdGenerator.Next(short.MinValue, short.MaxValue);

                lock (_trackedSentMessages) {
                    if (_trackedSentMessages.Where(x => x.NetworkId == generatedId).Count() == 0) {
                        break;
                    }
                }
            }

            return PrepareNetworkMessageWithId(generatedId, packetType, requiresAcknowledgement, command, parameters);
        }

        public async Task<NetworkMessage> SendNetworkMessageRemoteException(short networkId, RemoteExecutionExceptionNetworkModel exceptionModel) {
            var preparedNetworkMessage = await PrepareNetworkMessageWithId(networkId, NetworkPacket.Type.RemoteException, false, "", exceptionModel).Send();
            return preparedNetworkMessage.NetworkMessage;
        }

        public async Task<NetworkMessage> SendNetworkMessageAcknowledgement(short networkId) {
            var preparedNetworkMessage = await PrepareNetworkMessageWithId(networkId, NetworkPacket.Type.PackageAcknowledged, false, "").Send();
            return preparedNetworkMessage.NetworkMessage;
        }

        public async Task<NetworkMessage> SendNetworkMessageResponse(short networkId, object response) {
            var preparedNetworkMessage = await PrepareNetworkMessageWithId(networkId, NetworkPacket.Type.Response, true, "", response).Send();
            return preparedNetworkMessage.NetworkMessage;
        }

        public async Task<NetworkMessage> SendNetworkMessageAction(string command, params object[] parameters) {
            var preparedNetworkMessage = PrepareNetworkMessage(NetworkPacket.Type.Action, true, command, parameters);

            lock (_trackedSentMessages) {
                _trackedSentMessages.Add(preparedNetworkMessage.NetworkMessage);
            }

            await preparedNetworkMessage.Send();

            return preparedNetworkMessage.NetworkMessage;
        }

        public async Task<NetworkRequest> SendNetworkMessageRequest(string command, params object[] parameters) {
            var preparedNetworkMessage = PrepareNetworkMessage(NetworkPacket.Type.Request, true, command, parameters);

            lock (_trackedSentMessages) {
                _trackedSentMessages.Add(preparedNetworkMessage.NetworkMessage);
            }

            await preparedNetworkMessage.Send();

            return preparedNetworkMessage.NetworkMessage as NetworkRequest;
        }
        #endregion

        public async Task OnShutdown() {
            lock (_trackedSentMessages) {
                foreach (var trackedMessage in _trackedSentMessages) {
                    trackedMessage.Cancel();
                }
            }

            await Task.CompletedTask;
        }

        public class PreparedNetworkMessage {
            public NetworkClient NetworkClient { get; private set; }
            public NetworkPacket NetworkPacket { get; private set; }
            public NetworkStreamTransmissionHandler NetworkSth { get; private set; }
            public NetworkMessage NetworkMessage { get; private set; }

            public PreparedNetworkMessage(NetworkClient networkClient, NetworkPacket networkPacket, NetworkStreamTransmissionHandler networkStreamTransmissionHandler) {
                NetworkClient = networkClient;
                NetworkPacket = networkPacket;
                NetworkSth = networkStreamTransmissionHandler;
                NetworkMessage = networkPacket.PacketType == NetworkPacket.Type.Request ?
                    new NetworkRequest(NetworkPacket.NetworkId, NetworkPacket.CommandName, NetworkPacket.CommandParameters) :
                    new NetworkMessage(NetworkPacket.NetworkId, NetworkPacket.CommandName, NetworkPacket.CommandParameters);
            }

            public async Task<PreparedNetworkMessage> Send() {
                // Is there any Stream that needs to be transmitted in chunks?
                if (NetworkPacket.CommandParameters.Count(x => typeof(Stream).IsAssignableFrom(x?.GetType())) > 0) {
                    var sendStreamTask = NetworkSth.OnNetworkMessageSend(this);
                    return this;
                }

                // Send it as one packet
                await NetworkClient.SendInternalAsync(NetworkPacket);
                return this;
            }
        }
    }
}

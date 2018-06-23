using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TcpSharpr.MethodInteraction;
using TcpSharpr.Network.Protocol.RemoteExceptions;

namespace TcpSharpr.Network.Protocol {
    public class MessageManager {
        private static readonly Random _randomIdGenerator = new Random();
        private CommandManager _commandManager;
        private NetworkClient _networkClient;
        private List<NetworkMessage> _trackedSentMessages;

        public MessageManager(CommandManager commandManager, NetworkClient networkClient) {
            _networkClient = networkClient;
            _commandManager = commandManager;
            _trackedSentMessages = new List<NetworkMessage>();
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
                await _commandManager.InvokeCommand(networkPacket.CommandName, networkPacket.CommandParameters);
            } catch (Exception ex) {
                await SendNetworkMessageRemoteException(networkPacket.NetworkId, RemoteExecutionExceptionNetworkModel.FromException(ex));
            }
        }

        public async Task FromNetworkHandleRequest(NetworkPacket networkPacket) {
            try {
                var result = await _commandManager.InvokeCommand(networkPacket.CommandName, networkPacket.CommandParameters);
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
        public async Task<NetworkPacket> SendNetworkMessageWithId(short networkId, NetworkPacket.Type packetType, bool requiresAcknowledgement, string command, params object[] parameters) {
            var networkPacket = new NetworkPacket {
                NetworkId = networkId,
                CommandName = command,
                CommandParameters = parameters,
                RequiresAcknowledgement = requiresAcknowledgement,
                PacketType = packetType
            };

            await _networkClient.SendInternalAsync(networkPacket);
            return networkPacket;
        }

        public async Task<NetworkPacket> SendNetworkMessage(NetworkPacket.Type packetType, bool requiresAcknowledgement, string command, params object[] parameters) {
            return await SendNetworkMessageWithId((short)_randomIdGenerator.Next(), packetType, requiresAcknowledgement, command, parameters);
        }

        public async Task<NetworkMessage> SendNetworkMessageRemoteException(short networkId, RemoteExecutionExceptionNetworkModel exceptionModel) {
            var networkPacket = await SendNetworkMessageWithId(networkId, NetworkPacket.Type.RemoteException, false, "", exceptionModel);
            return new NetworkMessage(networkPacket.NetworkId, networkPacket.CommandName, networkPacket.CommandParameters);
        }

        public async Task<NetworkMessage> SendNetworkMessageAcknowledgement(short networkId) {
            var networkPacket = await SendNetworkMessageWithId(networkId, NetworkPacket.Type.PackageAcknowledged, false, "");
            return new NetworkMessage(networkPacket.NetworkId, networkPacket.CommandName, networkPacket.CommandParameters);
        }

        public async Task<NetworkMessage> SendNetworkMessageResponse(short networkId, object response) {
            var networkPacket = await SendNetworkMessageWithId(networkId, NetworkPacket.Type.Response, true, "", response);
            return new NetworkMessage(networkPacket.NetworkId, networkPacket.CommandName, new object[] { response });
        }

        public async Task<NetworkMessage> SendNetworkMessageAction(string command, params object[] parameters) {
            var networkPacket = await SendNetworkMessage(NetworkPacket.Type.Action, true, command, parameters);
            var networkMessage = new NetworkMessage(networkPacket.NetworkId, networkPacket.CommandName, networkPacket.CommandParameters);

            lock (_trackedSentMessages) {
                _trackedSentMessages.Add(networkMessage);
            }

            return networkMessage;
        }

        public async Task<NetworkRequest> SendNetworkMessageRequest(string command, params object[] parameters) {
            var networkPacket = await SendNetworkMessage(NetworkPacket.Type.Request, true, command, parameters);
            var networkMessage = new NetworkRequest(networkPacket.NetworkId, networkPacket.CommandName, networkPacket.CommandParameters);

            lock (_trackedSentMessages) {
                _trackedSentMessages.Add(networkMessage);
            }

            return networkMessage;
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
    }
}

using System;

namespace TcpSharpr.Network {
    [Serializable]
    public class NetworkPacket {
        public short NetworkId { get; set; }
        public Type PacketType { get; set; }
        public bool RequiresAcknowledgement { get; set; }
        public object[] CommandParameters { get; set; }
        public string CommandName { get; set; }

        public enum Type {
            Action,
            Request,
            Response,
            PackageAcknowledged,
            RemoteException
        }
    }
}

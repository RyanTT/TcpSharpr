namespace TcpSharpr.Network.Protocol {
    public interface IMessageSerializer {
        NetworkPacket DeserializeNetworkMessage(byte[] packet);
        byte[] SerializeNetworkMessage(NetworkPacket networkMessage);
    }
}

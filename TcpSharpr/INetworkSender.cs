using System.Threading.Tasks;
using TcpSharpr.Network;

namespace TcpSharpr {
    public interface INetworkSender {
        Task<NetworkMessage> SendAsync(string command, params object[] args);
        Task<NetworkRequest> SendRequestAsync(string command, params object[] args);
    }
}

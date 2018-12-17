using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace TcpSharpr.Network.Protocol {
    public class BinaryFormatterSerializer : IMessageSerializer {
        public NetworkPacket DeserializeNetworkMessage(byte[] packet) {
            return (NetworkPacket)BytesToObject(packet);
        }

        public byte[] SerializeNetworkMessage(NetworkPacket networkMessage) {
            return ObjectToBytes(networkMessage);
        }

        private byte[] ObjectToBytes(object o) {
            if (o == null) return new byte[0];

            var binFormatter = new BinaryFormatter();
            var mStream = new MemoryStream();

            binFormatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            binFormatter.Serialize(mStream, o);

            return mStream.ToArray();
        }

        private object BytesToObject(byte[] bytes) {
            var mStream = new MemoryStream();
            var binFormatter = new BinaryFormatter {
                AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple
            };

            mStream.Write(bytes, 0, bytes.Length);
            mStream.Position = 0;

            return binFormatter.Deserialize(mStream);
        }
    }
}

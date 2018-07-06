using System;
using System.Collections.Generic;
using System.Linq;

namespace TcpSharpr.Network.Protocol {
    public class PacketFormatter {
        public byte[][] TryReadPackets(List<byte> buffer) {
            List<byte[]> readPackets = new List<byte[]>();

            while (buffer.Count > 2) {
                try {
                    int packetBodyLength = BitConverter.ToInt32(buffer.Take(4).ToArray(), 0);

                    if (buffer.Count - 2 >= packetBodyLength) {
                        byte[] packetBody = buffer.Skip(4).Take(packetBodyLength).ToArray();

                        readPackets.Add(packetBody);
                        buffer.RemoveRange(0, 4 + packetBodyLength);
                    } else {
                        break;
                    }
                } catch {
                    break;
                }
            }

            return readPackets.ToArray();
        }

        public byte[] PreparePacketForNetwork(byte[] buffer) {
            int packetLength = buffer.Length;

            List<byte> packet = new List<byte>();
            packet.AddRange(BitConverter.GetBytes(packetLength));
            packet.AddRange(buffer);

            return packet.ToArray();
        }
    }
}

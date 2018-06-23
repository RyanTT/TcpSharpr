using System;
using System.Collections.Generic;
using System.Linq;

namespace TcpSharpr.Network.Protocol {
    public class PacketFormatter {
        public byte[][] TryReadPackets(List<byte> buffer) {
            List<byte[]> readPackets = new List<byte[]>();

            while (buffer.Count > 2) {
                try {
                    short packetBodyLength = BitConverter.ToInt16(buffer.Take(2).ToArray(), 0);

                    if (buffer.Count - 2 >= packetBodyLength) {
                        byte[] packetBody = buffer.Skip(2).Take(packetBodyLength).ToArray();

                        readPackets.Add(packetBody);
                        buffer.RemoveRange(0, 2 + packetBodyLength);
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
            short packetLength = (short)buffer.Length;

            List<byte> packet = new List<byte>();
            packet.AddRange(BitConverter.GetBytes(packetLength));
            packet.AddRange(buffer);

            return packet.ToArray();
        }
    }
}

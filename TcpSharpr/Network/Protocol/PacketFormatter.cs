using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TcpSharpr.Network.Protocol {
    public class PacketFormatter {
        private readonly ICryptoTransform _encryptTransform, _decryptTransform;

        public PacketFormatter(SymmetricAlgorithm algorithm) {
            if (algorithm != null) {
                _encryptTransform = algorithm.CreateEncryptor();
                _decryptTransform = algorithm.CreateDecryptor();
            }
        }
		
        public byte[][] TryReadPackets(List<byte> buffer) {
            List<byte[]> readPackets = new List<byte[]>();

            while (buffer.Count > 2) {
                try {
                    int packetBodyLength = BitConverter.ToInt32(buffer.Take(4).ToArray(), 0);

                    if (buffer.Count - 2 >= packetBodyLength) {
                        byte[] packetBody = buffer.Skip(4).Take(packetBodyLength).ToArray();
						if (_decryptTransform != null)
                            packetBody = DoCrypto(packetBody, _decryptTransform);
							
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
            if (_encryptTransform != null)
                buffer = DoCrypto(buffer, _encryptTransform);
            int packetLength = buffer.Length;

            List<byte> packet = new List<byte>();
            packet.AddRange(BitConverter.GetBytes(packetLength));
            packet.AddRange(buffer);

            return packet.ToArray();
        }

        private byte[] DoCrypto(byte[] input, ICryptoTransform transform) {
            if (input == null || input.Length <= 0)
                throw new ArgumentNullException("input length must be > 0");

            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write)) {
                cs.Write(input, 0, input.Length);
                cs.Close();
                return ms.ToArray();
            }
        }
    }
}
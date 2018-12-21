using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace TcpSharpr.Network.Protocol {
    /// <summary>
    /// Type of <see cref="PacketFormatter"/> that applies a symmetric encryption algorithm to all incoming and outgoing packages.
    /// <para>Courtesy of https://github.com/MelvinGr/</para>
    /// </summary>
    public class SymmetricEncryptionPacketFormatter : PacketFormatter {
        private readonly ICryptoTransform _encryptTransform, _decryptTransform;

        public SymmetricEncryptionPacketFormatter(SymmetricAlgorithm algorithm) {
            _encryptTransform = algorithm.CreateEncryptor();
            _decryptTransform = algorithm.CreateDecryptor();
        }

        public override byte[][] TryReadPackets(List<byte> buffer) {
            List<byte[]> readPackets = new List<byte[]>();

            while (buffer.Count > 2) {
                try {
                    int packetBodyLength = BitConverter.ToInt32(buffer.Take(4).ToArray(), 0);

                    if (buffer.Count - 2 >= packetBodyLength) {
                        byte[] packetBody = buffer.Skip(4).Take(packetBodyLength).ToArray();
                        packetBody = ApplyTransformation(packetBody, _decryptTransform);

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

        public override byte[] PreparePacketForNetwork(byte[] buffer) {
            buffer = ApplyTransformation(buffer, _encryptTransform);
   
            int packetLength = buffer.Length;

            List<byte> packet = new List<byte>();
            packet.AddRange(BitConverter.GetBytes(packetLength));
            packet.AddRange(buffer);

            return packet.ToArray();
        }

        private byte[] ApplyTransformation(byte[] input, ICryptoTransform transform) {
            if (input == null || input.Length <= 0) {
                throw new ArgumentNullException("input length must be greater than 0.");
            }

            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write)) {
                cs.Write(input, 0, input.Length);
                cs.Close();
                return ms.ToArray();
            }
        }
    }
}

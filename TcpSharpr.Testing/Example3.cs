using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TcpSharpr.Network.Protocol.Handlers.StreamTransmission;

namespace TcpSharpr.Testing {
    public class Example3 {
        public static async Task Run() {
            Server server = new Server(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            server.CommandManager.RegisterAsyncCommand("StreamTransmission", new Func<SegmentedNetworkStream, Task<Stream>>(StreamTransmission));
            server.Start();

            Client client = new Client(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            client.CommandManager.RegisterCommand("TestMessage", new Action<string>(Console.WriteLine));
            await client.ConnectAsync();

            string filePath = "";

            while (!File.Exists(filePath)) {
                Console.Write("\nPlease enter a test payload file path: ");
                filePath = Console.ReadLine();
            }

            Console.WriteLine("ENTER to start transmission");
            Console.ReadLine();

            FileStream fileStream = new FileStream(filePath, FileMode.Open);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Send the filestream
            var request = await client.SendRequestAsync("StreamTransmission", fileStream);

            request.OnNetworkTransmissionProgressChanged += Request_OnNetworkTransmissionProgressChanged;
            request.OnNetworkTransmissionFinished += Request_OnNetworkTransmissionFinished;

            // Get the returned stream
            var responseStream = await request.GetResultAsync<SegmentedNetworkStream>();

            // Read from it until it is signaled that the data is complete
            int totalBytesRead = 0;
            while (!responseStream.DataComplete || responseStream.DataAvailable > 0) {
                byte[] buffer = new byte[1048576];

                // Wait until (partial) data has been transmitted and is ready to be used
                long dataAvailable = await responseStream.WaitUntilDataAvailable();
                int bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length);

                totalBytesRead += bytesRead;

                // Use the by the remote side promised length of the stream to determine 
                Console.WriteLine($"Download progress: {Math.Round((totalBytesRead / (double)responseStream.PromisedLength) * 100, 2)}%");
            }

            Console.WriteLine("Download progress: Finished");

            stopwatch.Stop();
            Console.WriteLine($"Completed transmission in {stopwatch.Elapsed.Seconds} seconds");

            server.Stop();
            client.Disconnect();

            Console.WriteLine("Complete. ENTER to exit.");
            Console.ReadLine();
        }

        private static void Request_OnNetworkTransmissionFinished(object sender, Network.Events.NetworkTransmissionFinishedArgs e) {
            Console.WriteLine($"Upload progress: Finished");
        }

        private static void Request_OnNetworkTransmissionProgressChanged(object sender, Network.Events.NetworkTransmissionProgressChangedArgs e) {
            Console.WriteLine($"Upload progress: {Math.Round(e.Progress * 100, 2)}%");
        }

        private static async Task<Stream> StreamTransmission(SegmentedNetworkStream segmentedNetworkStream) {
            MemoryStream memoryStream = new MemoryStream();

            int totalBytesRead = 0;
            while (!segmentedNetworkStream.DataComplete || segmentedNetworkStream.DataAvailable > 0) {
                byte[] buffer = new byte[1048576];
                
                // Wait until (partial) data has been transmitted and is ready to be used
                long dataAvailable = await segmentedNetworkStream.WaitUntilDataAvailable();
                int bytesRead = await segmentedNetworkStream.ReadAsync(buffer, 0, buffer.Length);

                totalBytesRead += bytesRead;

                await memoryStream.WriteAsync(buffer, 0, buffer.Length);
                await memoryStream.FlushAsync();
            }

            memoryStream.Position = 0;

            // Send the same back
            return memoryStream;
        }
    }
}

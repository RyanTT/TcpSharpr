using System;
using System.Net;
using System.Threading.Tasks;
using TcpSharpr.Network;

namespace TcpSharpr.Testing {
    public class Example2 {
        public static async Task Run() {
            Server server = new Server(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            server.CommandManager.RegisterCommand("BroadcastToAll", new Action<NetworkClient, string>(ServerBroadcastToAll));
            server.Start();

            Client client = new Client(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            client.CommandManager.RegisterCommand("WriteToConsole", new Action<string>(Console.WriteLine));
            await client.ConnectAsync();

            Console.WriteLine("/exit to exit");

            while (true) {
                string input = Console.ReadLine();

                if (!string.IsNullOrEmpty(input)) {
                    if (input.ToLower().Equals("/exit"))
                        break;
                    else {
                        await client.SendAsync("BroadcastToAll", input);
                    }
                }
            }

            server.Stop();
            client.Disconnect();
        }

        static void ServerBroadcastToAll(NetworkClient context, string message) {
            Server server = context.GetParent();

            var allClients = server.ConnectedClients;

            foreach (var client in allClients) {
                Task a = client.SendAsync("WriteToConsole", $"{context.Endpoint.ToString()}: {message}");
            }
        }
    }
}

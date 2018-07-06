using System;
using System.Net;
using System.Threading.Tasks;

namespace TcpSharpr.Testing {
    public class Example1 {
        public static async Task Run() {
            Server server = new Server(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            server.CommandManager.RegisterCommand("AddNumbers", new Func<int, int, int>(RemoteAddition));
            server.CommandManager.RegisterAsyncCommand("DownloadHtml", new Func<string, Task<string>>(DownloadHtml));
            server.Start();

            Client client = new Client(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
            await client.ConnectAsync();

            int a = 5;
            int b = 7;

            Console.WriteLine($"> Server, what is {a} + {b}?");
            var additionRequest = await client.SendRequestAsync("AddNumbers", a, b);
            Console.WriteLine(">> Addition Result: " + await additionRequest.GetResultAsync<int>());

            string toDownloadUrl = "http://niceme.me/";

            Console.WriteLine($"> Server, download the html of {toDownloadUrl} for me please");
            var htmlRequest = await client.SendRequestAsync("DownloadHtml", toDownloadUrl);

            int c = 10;
            int d = 19;

            Console.WriteLine($"> Server, while you're downloading that html, can you please also tell me what's {c} + {d}");
            var additionRequest2 = await client.SendRequestAsync("AddNumbers", c, d);
            Console.WriteLine(">> Addition Result: " + await additionRequest2.GetResultAsync<int>());

            Console.WriteLine(">> Html Result:" + Environment.NewLine + await htmlRequest.GetResultAsync<string>());

            server.Stop();
            client.Disconnect();

            Console.WriteLine("Complete. ENTER to exit.");
            Console.ReadLine();
        }

        private static int RemoteAddition(int a, int b) {
            return a + b;
        }

        private static async Task<string> DownloadHtml(string url) {
            return await new WebClient().DownloadStringTaskAsync(url);
        }
    }
}

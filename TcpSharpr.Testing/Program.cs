using System;
using System.Net;
using System.Threading.Tasks;

namespace TcpSharpr.Testing {
    class Program {
        static void Main(string[] args) {
            MainAsync().Wait();
        }

        static async Task MainAsync() {
            await Example2.Run();
        }
    }
}

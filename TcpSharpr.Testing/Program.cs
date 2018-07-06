using System;
using System.Net;
using System.Threading.Tasks;

namespace TcpSharpr.Testing {
    class Program {
        static void Main(string[] args) {
            MainAsync().Wait();
        }

        static async Task MainAsync() {
            while (true) {
                Console.WriteLine(
                    $"Menu{Environment.NewLine}" +
                    $"1 - Example 1{Environment.NewLine}" +
                    $"2 - Example 2{Environment.NewLine}" +
                    $"3 - Example 3");

                string option = Console.ReadLine();

                switch (option) {
                    case "1": await Example1.Run(); break;
                    case "2": await Example2.Run(); break;
                    case "3": await Example3.Run(); break;

                    default:
                        return;
                }
            }
        }
    }
}

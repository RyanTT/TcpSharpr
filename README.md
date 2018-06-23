# TcpSharpr
An easy to use C# networking library for .NET Standard/Core 2.0

## What is TcpSharpr?
TcpSharpr is a small async-based networking library meant for easy internet/inter-process communication with other .NET applications. It allows you to call methods in other processes almost like you would call a method locally (see the example below).

## How do I use this library?
With TcpSharpr, you register commands on your server/client for the other party to invoke. Commands can be invoked either synchronously or asynchronously. Use the respective methods for that to prevent issues.

### How do I host a server?
```csharp
Server server = new Server(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
server.Start();
```

### How do I create a client and connect to a server?
```csharp
Client client = new Client(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1805));
await client.ConnectAsync();
```

### How do I register a command for the other party to invoke
#### Synchronously
```csharp
public int RemoteAddition(int a, int b) {
    return a + b;
}

server.CommandManager.RegisterCommand("AddNumbers", new Func<int, int, int>(RemoteAddition));
```

#### Asynchronously
```csharp
public static async Task<string> DownloadHtml(string url) {
    return await new WebClient().DownloadStringTaskAsync(url);
}

server.CommandManager.RegisterAsyncCommand("DownloadHtml", new Func<string, Task<string>>(DownloadHtml));
```

### How do I send a request?
```csharp
var additionRequest = await client.SendRequestAsync("AddNumbers", 5, 10);
int result = await additionRequest.GetResultAsync<int>(); // result = 15
```
or if you don't care about the result...
```csharp
await client.SendRequestAsync("AddNumbers", 5, 10);
```

### What happens when theres an exception on the remote side?
When an exception occurs, some general data about it is sent back to the caller. On the caller end a `RemoteExecutionException` is thrown (it might be wrapped in several `AggregateException`s though). `RemoteExceptionTypeName`, `RemoteExceptionMessage` and `RemoteExceptionStack` contain the important information. 

## Full example
```csharp
using System;
using System.Net;
using System.Threading.Tasks;

namespace TcpSharpr.Testing {
    class Program {
        static void Main(string[] args) {
            MainAsync().Wait();
        }

        static async Task MainAsync() {
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

            Console.ReadLine();
        }

        public static int RemoteAddition(int a, int b) {
            return a + b;
        }

        public static async Task<string> DownloadHtml(string url) {
            return await new WebClient().DownloadStringTaskAsync(url);
        }
    }
}
```

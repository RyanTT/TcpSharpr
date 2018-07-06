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

### If everything is packet based, how do I transmit Streams?
TcpSharpr offers the ability to transmit Streams as part of remote command invocation. Take the following example:
In this case I'd like to transmit a file from my PC to the server.

For the server to be able to receive the stream, it needs to have a command able of receiving that stream. Here's for example how to register this command:

```csharp
server.CommandManager.RegisterAsyncCommand("StreamTransmission", new Func<SegmentedNetworkStream, Task>(StreamTransmission));
```
**Note** Any stream transmitted over the network requires the other side to be ready to receive it as a `SegmentedNetworkStream`. View below on how to read from it correctly.

Now that we have the command registered, let's send a file from our PC to that command on the server.

```csharp
FileStream fileStream = new FileStream(@"C:\Users\timet\Desktop\dotnet-sdk-2.1.201-win-x64.exe", FileMode.Open);
var request = await client.SendRequestAsync("StreamTransmission", fileStream);
```

This will start reading from the `FileStream` until the end and sending the chunks to the other party's `SegmentedNetworkStream`.

We may also use the returned `NetworkRequest` or `NetworkMessage` object to receive information over how far the transmission of all in this command invocation's Streams has progressed.
You can find this information in `NetworkMessage.NetworkTransmissionProgress` or if you'd like it event based in `NetworkMessage.OnNetworkTransmissionProgressChanged` and `NetworkMessage.OnNetworkTransmissionFinished`.

**So much for the sending part. But how do we read it on the server-side?**
Reading can be done like so:

```csharp
private static async Task StreamTransmission(SegmentedNetworkStream segmentedNetworkStream) {
	while (!segmentedNetworkStream.DataComplete || segmentedNetworkStream.DataAvailable > 0) {
		byte[] buffer = new byte[1048576];
		
		// Wait until (partial) data has been transmitted and is ready to be used
		long dataAvailable = await segmentedNetworkStream.WaitUntilDataAvailable();
		int bytesRead = await segmentedNetworkStream.ReadAsync(buffer, 0, buffer.Length);
	}
}
```

`SegmentedNetworkStream.WaitUntilDataAvailable()` allows you to easily wait for the data to be received without blocking the execution thread. After this, you may do with the data what you want. `SegmentedNetworkStream.DataComplete` will become true once all data of that Stream is transmitted.

If you want to see how far you are through the Stream, use the `SegmentedNetworkStream.PromisedLength` property. It's value will be set before the very first transmission of that Stream.

**Note** If you transmit more than one Stream as a parameter, the Streams will be transmitted in order.
**Note** Returning a Stream from a command method is also supported. In this case you get the Stream the same way like this `var responseStream = await request.GetResultAsync<SegmentedNetworkStream>();`


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

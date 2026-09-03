using System.Net;
using System.Net.Sockets;

const int port = 5000;
const string filePath = "stream-test2.txt";

Console.WriteLine("Fake OMAX Server");
Console.WriteLine("----------------");
Console.WriteLine($"Starting server on port {port}...");

TcpListener listener = new TcpListener(IPAddress.Loopback, port);

listener.Start();

Console.WriteLine("Server is listening.");
Console.WriteLine("Waiting for collector connection...");
Console.WriteLine();

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    Console.WriteLine($"Collector connected: {client.Client.RemoteEndPoint}");
    _ = SendStreamAsync(client);
}


static async Task SendStreamAsync(TcpClient client)
{
    try
    {
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new StreamWriter(stream);

        writer.AutoFlush = true;

        while (true)
        {
            Console.WriteLine("Sending stream-test2.txt...");
            using StreamReader reader = new StreamReader("stream-test2.txt");

            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                await writer.WriteLineAsync(line);
                Console.WriteLine($"Sent: {line}");
                await Task.Delay(10);
            }

            Console.WriteLine("Finished sending file.");
            Console.WriteLine("Waiting 10 seconds...");
            await Task.Delay(10000);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client connection ended: {ex.Message}");
    }
}
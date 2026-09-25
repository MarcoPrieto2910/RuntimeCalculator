using System.Net;
using System.Net.Sockets;

const int port = 5000;
const string filePath = "stream-test2.txt";

TimeSpan expectedMorningRuntime = new(3, 31, 11);
TimeSpan expectedAfternoonRuntime = new(0, 30, 48);

TimeSpan totalMorningRuntime = TimeSpan.Zero;
TimeSpan totalAfternoonRuntime = TimeSpan.Zero;

Console.WriteLine("Fake OMAX Server");
Console.WriteLine("----------------");
Console.WriteLine($"Starting server on port {port}...");

TcpListener listener = new TcpListener(IPAddress.Loopback, port);
listener.Start();

Console.WriteLine("Server is listening.");
Console.WriteLine("Waiting for collector connection...");
Console.WriteLine();

bool shouldQuit = false;

while (!shouldQuit)
{
    TcpClient client = await listener.AcceptTcpClientAsync();

    Console.WriteLine($"Collector connected: {client.Client.RemoteEndPoint}");
    Console.WriteLine();

    (totalMorningRuntime, totalAfternoonRuntime, shouldQuit) =
        await SendStreamAsync(
            client,
            filePath,
            expectedMorningRuntime,
            expectedAfternoonRuntime,
            totalMorningRuntime,
            totalAfternoonRuntime);

    if (!shouldQuit)
    {
        Console.WriteLine("Collector disconnected.");
        Console.WriteLine();
        Console.WriteLine("Waiting for collector connection...");
        Console.WriteLine();
    }
}

listener.Stop();

Console.WriteLine();
Console.WriteLine("Fake OMAX Server stopped.");

static async Task<(TimeSpan Morning, TimeSpan Afternoon, bool ShouldQuit)> SendStreamAsync(
    TcpClient client,
    string filePath,
    TimeSpan expectedMorningRuntime,
    TimeSpan expectedAfternoonRuntime,
    TimeSpan totalMorningRuntime,
    TimeSpan totalAfternoonRuntime)
{
    try
    {
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new StreamWriter(stream);

        writer.AutoFlush = true;

        while (true)
        {
            Console.WriteLine($"Sending {filePath}...");
            Console.WriteLine();

            using StreamReader reader = new StreamReader(filePath);

            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                await writer.WriteLineAsync(line);
                Console.WriteLine($"Sent: {line}");
                await Task.Delay(10);
            }

            Console.WriteLine();
            Console.WriteLine("Finished sending file.");

            totalMorningRuntime += expectedMorningRuntime;
            totalAfternoonRuntime += expectedAfternoonRuntime;

            Console.WriteLine();
            Console.WriteLine("Expected runtime:");
            Console.WriteLine(
                $"  Morning:   {FormatDuration(totalMorningRuntime)}");
            Console.WriteLine(
                $"  Afternoon: {FormatDuration(totalAfternoonRuntime)}");

            Console.WriteLine();
            Console.WriteLine("Press ENTER to send again, or Q to stop.");

            string? input = Console.ReadLine();

            if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                Console.WriteLine("Stopping Fake OMAX...");

                client.Close();

                return (
                    totalMorningRuntime,
                    totalAfternoonRuntime,
                    true);
            }

            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client connection ended: {ex.Message}");
        client.Dispose();

        return (
            totalMorningRuntime,
            totalAfternoonRuntime,
            false);
    }
}

static string FormatDuration(TimeSpan duration)
{
    return
        $"{(int)duration.TotalHours:00}:" +
        $"{duration.Minutes:00}:" +
        $"{duration.Seconds:00}";
}
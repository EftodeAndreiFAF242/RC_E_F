// Lab 2: many clients order one croissant together.
using System.Diagnostics;
using System.Net.Sockets;

namespace Lab2;

public static class Demo
{
    public static void Run(string host, int port, int clients)
    {
        var clock = new Stopwatch();
        var results = new string[clients];

        // Everybody orders in the same moment.
        using var barrier = new Barrier(clients, _ => clock.Restart());

        var threads = new Thread[clients];
        for (int i = 0; i < clients; i++)
        {
            int index = i;
            threads[i] = new Thread(() => results[index] = OneClient(host, port, barrier, clock));
            threads[i].Start();
        }
        foreach (Thread thread in threads)
            thread.Join();

        Console.WriteLine($"\n{clients} clients ordered 1 croissant each:");
        foreach (var group in results.GroupBy(r => r).OrderBy(g => g.Key))
            Console.WriteLine($"  {group.Count(),3} x {group.Key}");
    }

    private static string OneClient(string host, int port, Barrier barrier, Stopwatch clock)
    {
        Connection? connection = null;
        bool joined = false;
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            connection = new Connection(socket, sendHeartbeat: false);
            connection.Send("HELLO client");

            joined = true;
            barrier.SignalAndWait();
            connection.Send("ORDER croissant 1");

            while (connection.ReadLine(out string line) != ReadResult.Closed)
            {
                if (line.StartsWith("REFUSED"))
                    return "refused, no croissant left";
                if (line.StartsWith("READY"))
                    return $"ready after {Math.Round(clock.Elapsed.TotalSeconds)} s";
            }
            return "connection lost";
        }
        catch (SocketException ex)
        {
            if (!joined)
                barrier.RemoveParticipant();
            return $"network error: {ex.SocketErrorCode}";
        }
        finally
        {
            connection?.Close("demo finished");
        }
    }
}

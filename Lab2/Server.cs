// Lab 2: the listening socket, one thread per connection.
using System.Net;
using System.Net.Sockets;

namespace Lab2;

public static class Server
{
    public static void Run(int port, string stockOption)
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.Listen();

        var shop = new Shop(stockOption);
        Log.Write("server", $"listening on port {port}, {shop.WorkerCount} workers");
        Log.Write("server", shop.Menu());

        while (true)
        {
            Socket? socket = null;
            try
            {
                socket = listener.Accept();
                Socket accepted = socket;
                new Thread(() => Serve(accepted, shop)) { IsBackground = true }.Start();
            }
            catch (Exception ex)
            {
                Log.Write("server", $"error on accept: {ex.Message}");
                socket?.Close();
                Thread.Sleep(100);
            }
        }
    }

    private static void Serve(Socket socket, Shop shop)
    {
        try
        {
            new ClientHandler(socket, shop).Run();
        }
        catch (Exception ex)
        {
            Log.Write("server", $"error on a new connection: {ex.Message}");
            socket.Close();
        }
    }
}

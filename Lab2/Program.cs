// Lab 2 - A Coffee Shop Server: starts as server, client, provider or demo.
using System.Net.Sockets;
using Lab2;

try
{
    string role = args.Length > 0 ? args[0].ToLowerInvariant() : "";

    if (role == "server" && (args.Length == 2 || args.Length == 4))
        Server.Run(ReadPort(args[1]), ReadStockOption(args));
    else if (role is "client" or "provider" && args.Length == 3)
        ConsoleApp.Run(role, args[1], ReadPort(args[2]));
    else if (role == "demo" && (args.Length == 3 || args.Length == 4))
        Demo.Run(args[1], ReadPort(args[2]), args.Length == 4 ? ReadClients(args[3]) : Environment.ProcessorCount + 8);
    else
        PrintHelp();
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.ExitCode = 1;
}
catch (SocketException ex)
{
    Console.Error.WriteLine($"Network error ({ex.SocketErrorCode}): {ex.Message}");
    Environment.ExitCode = 1;
}

static int ReadPort(string text)
{
    if (int.TryParse(text, out int port) && port >= 1 && port <= 65535)
        return port;
    throw new ArgumentException($"'{text}' is not a port (1-65535).");
}

static int ReadClients(string text)
{
    if (int.TryParse(text, out int clients) && clients >= 1 && clients <= 1000)
        return clients;
    throw new ArgumentException($"'{text}' is not a number of clients (1-1000).");
}

static string ReadStockOption(string[] args)
{
    if (args.Length == 2)
        return "saved";
    if (args[2] != "--stock")
        throw new ArgumentException($"Unknown option '{args[2]}'.");
    if (args[3] == "random" || (int.TryParse(args[3], out int amount) && amount >= 0 && amount <= Shop.MaxStock))
        return args[3];
    throw new ArgumentException($"--stock takes 'random' or a number from 0 to {Shop.MaxStock}.");
}

static void PrintHelp()
{
    Console.WriteLine(@"Lab 2 - A Coffee Shop Server

Usage:
  Lab2 server <port> [--stock random|N]   start the shop
  Lab2 client <address> <port>            interactive client
  Lab2 provider <address> <port>          interactive provider
  Lab2 demo <address> <port> [clients]    many clients order one croissant at once

Without --stock the server keeps the saved stock, or picks 10-50 of each product.");
}

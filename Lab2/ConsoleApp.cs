// Lab 2: the interactive client and provider.
using System.Net.Sockets;
using System.Text;

namespace Lab2;

public sealed class ConsoleApp
{
    private const int ServerSilenceMs = 10_000;
    private const string Prompt = "> ";

    private readonly Connection _connection;
    private readonly object _consoleLock = new();
    private readonly StringBuilder _typed = new();
    private volatile bool _quitting;

    private ConsoleApp(Connection connection) => _connection = connection;

    public static void Run(string role, string host, int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);
        socket.ReceiveTimeout = ServerSilenceMs;

        var app = new ConsoleApp(new Connection(socket, sendHeartbeat: true));
        Console.WriteLine($"Connected to {host}:{port} as {role}. Type help for commands.");
        app.Start(role);
    }

    private void Start(string role)
    {
        // One thread receives, this one reads keys.
        new Thread(ReceiveLoop) { IsBackground = true }.Start();
        _connection.Send($"HELLO {role}");

        while (true)
        {
            string? text = ReadInput();
            if (text == null)
                break;

            text = text.Trim();
            string lower = text.ToLowerInvariant();

            if (lower is "quit" or "exit")
                break;
            if (lower == "help")
                Show(HelpText);
            else if (lower.StartsWith("test "))
                RunTest(lower);
            else if (text != "")
                _connection.Send(text);
        }

        _quitting = true;
        _connection.CloseAfterSending();
    }

    private void ReceiveLoop()
    {
        while (true)
        {
            ReadResult result = _connection.ReadLine(out string line);

            if (result == ReadResult.Closed)
                break;
            if (result != ReadResult.Line)
                Show("(the server sent an invalid message)");
            else if (line != "PONG")
                Show(line);
        }

        if (_quitting)
            return;

        Show($"The server is gone ({_connection.CloseReason}). Closing.", showPrompt: false);
        Environment.Exit(0);
    }

    // Prints a message without losing typed text.
    private void Show(string message, bool showPrompt = true)
    {
        lock (_consoleLock)
        {
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(message);
                return;
            }

            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            Console.WriteLine(message);
            if (showPrompt)
                Console.Write(Prompt + _typed);
        }
    }

    private string? ReadInput()
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            lock (_consoleLock)
            {
                if (key.Key == ConsoleKey.Enter)
                {
                    string text = _typed.ToString();
                    _typed.Clear();
                    Console.WriteLine();
                    Console.Write(Prompt);
                    return text;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (_typed.Length > 0)
                    {
                        _typed.Length--;
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar) && _typed.Length < Console.WindowWidth - Prompt.Length - 1)
                {
                    _typed.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }
    }

    // Small tests to show at the defense.
    private void RunTest(string command)
    {
        switch (command)
        {
            case "test split":
                _connection.SendBytes(Encoding.ASCII.GetBytes("ME"));
                Thread.Sleep(700);
                _connection.SendBytes(Encoding.ASCII.GetBytes("NU\nMENU\n"));
                break;

            case "test long":
                _connection.Send(new string('x', Connection.MaxLineBytes + 50));
                break;

            case "test junk":
                byte[] junk = new byte[64];
                Random.Shared.NextBytes(junk);
                junk[^1] = (byte)'\n';
                _connection.SendBytes(junk);
                break;

            case "test silent":
                _connection.StopHeartbeat();
                Show("Heartbeat stopped: the server now sees a lost network.");
                break;

            default:
                Show("Tests: test split | test long | test junk | test silent");
                break;
        }
    }

    private const string HelpText =
        "menu | order coffee 1 tea 2 | deliver coffee 20 (provider) | quit\n" +
        "tests: test split | test long | test junk | test silent";
}

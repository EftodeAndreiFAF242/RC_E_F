// Lab 2: reads one connection and runs its commands.
using System.Net.Sockets;

namespace Lab2;

public sealed class ClientHandler
{
    private enum Role { Unknown, Client, Provider }

    private readonly Connection _connection;
    private readonly Shop _shop;
    private Role _role = Role.Unknown;

    public ClientHandler(Socket socket, Shop shop)
    {
        _shop = shop;
        _connection = new Connection(socket, sendHeartbeat: false);
        _connection.EnableKeepAlive();
    }

    public void Run()
    {
        Log.Write(_connection.Endpoint, "connected");
        try
        {
            _connection.Send("WELCOME to the coffee shop. Type HELP for commands.");

            while (true)
            {
                ReadResult result = _connection.ReadLine(out string line);

                if (result == ReadResult.Closed)
                    break;
                if (result == ReadResult.TooLong)
                    Error($"message too long (max {Connection.MaxLineBytes} bytes)");
                else if (result == ReadResult.BadBytes)
                    Error("the message contains invalid bytes");
                else
                    Handle(line);
            }
        }
        catch (Exception ex)
        {
            Log.Write(_connection.Endpoint, $"error: {ex.Message}");
        }
        finally
        {
            _connection.Close("connection handler ended");
            if (_role == Role.Provider)
                _shop.RemoveProvider(_connection);
            _shop.CancelOrders(_connection);
            Log.Write(_connection.Endpoint, $"disconnected ({_connection.CloseReason})");
        }
    }

    private void Handle(string line)
    {
        string[] words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string command = words.Length > 0 ? words[0].ToUpperInvariant() : "";

        if (command == "PING")
        {
            _connection.ExpectHeartbeat();
            _connection.Send("PONG");
            return;
        }

        if (command == "HELLO")
        {
            Hello(words);
            return;
        }

        // Without HELLO, a connection is a client.
        if (_role == Role.Unknown)
            _role = Role.Client;

        switch (command)
        {
            case "MENU":
                if (words.Length == 1)
                    _connection.Send(_shop.Menu());
                else
                    Error("usage: MENU");
                break;

            case "ORDER":
                if (_role != Role.Client)
                    Error("only a client can order");
                else if (_shop.TryParseItems(words, out var ordered, out string orderError))
                    _shop.PlaceOrder(_connection, ordered);
                else
                    Error(orderError);
                break;

            case "DELIVER":
                if (_role != Role.Provider)
                    Error("only the provider can deliver");
                else if (_shop.TryParseItems(words, out var delivered, out string deliveryError))
                    _shop.Deliver(_connection, delivered);
                else
                    Error(deliveryError);
                break;

            case "HELP":
                _connection.Send("HELP MENU | ORDER <product> <quantity> ... | DELIVER <product> <quantity> ... (provider only)");
                break;

            case "":
                Error("empty message");
                break;

            default:
                Error($"unknown command '{Shop.Shorten(words[0])}'");
                break;
        }
    }

    // The role is chosen once, by the first message.
    private void Hello(string[] words)
    {
        string wanted = words.Length == 2 ? words[1].ToLowerInvariant() : "";

        if (wanted != "client" && wanted != "provider")
        {
            Error("usage: HELLO client | HELLO provider");
            return;
        }
        if (_role != Role.Unknown)
        {
            Error($"you are already connected as {_role.ToString().ToLowerInvariant()}");
            return;
        }

        if (wanted == "provider")
        {
            if (!_shop.TrySetProvider(_connection))
            {
                Error("a provider is already connected");
                return;
            }
            _role = Role.Provider;
        }
        else
        {
            _role = Role.Client;
            _connection.Send("OK role=client");
        }
        Log.Write(_connection.Endpoint, $"identified as {wanted}");
    }

    private void Error(string text)
    {
        _connection.Send("ERROR " + text);
        Log.Write(_connection.Endpoint, "error: " + text);
    }
}

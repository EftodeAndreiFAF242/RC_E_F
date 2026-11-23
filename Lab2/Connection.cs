// Lab 2: one TCP connection, cut into lines, with one writer thread.
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace Lab2;

public enum ReadResult { Line, TooLong, BadBytes, Closed }

public sealed class Connection
{
    public const int MaxLineBytes = 256;

    private const int OutboxSize = 256;
    private const int HeartbeatMs = 1000;
    private const int SilenceLimitMs = 5000;
    private const int SendLimitMs = 5000;

    private readonly Socket _socket;
    private readonly Thread _writer;
    private readonly BlockingCollection<byte[]> _outbox = new(OutboxSize);

    private readonly byte[] _received = new byte[1024];
    private readonly List<byte> _line = new();
    private int _receivedCount;
    private int _position;
    private bool _skippingLongLine;

    private volatile bool _sendHeartbeat;
    private bool _expectsHeartbeat;
    private int _closed;

    public string Endpoint { get; }
    public string CloseReason { get; private set; } = "";

    public Connection(Socket socket, bool sendHeartbeat)
    {
        _socket = socket;
        _sendHeartbeat = sendHeartbeat;
        _socket.NoDelay = true;
        _socket.SendTimeout = SendLimitMs;
        Endpoint = socket.RemoteEndPoint?.ToString() ?? "unknown";

        _writer = new Thread(WriteLoop) { IsBackground = true };
        _writer.Start();
    }

    // Finds silent clients that send no PING.
    public void EnableKeepAlive()
    {
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
        _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 2);
        _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
    }

    // After the first PING, silence means gone.
    public void ExpectHeartbeat()
    {
        if (_expectsHeartbeat)
            return;
        _expectsHeartbeat = true;
        _socket.ReceiveTimeout = SilenceLimitMs;
    }

    public void StopHeartbeat() => _sendHeartbeat = false;

    // Returns one whole line, however the bytes arrived.
    public ReadResult ReadLine(out string line)
    {
        line = "";

        while (true)
        {
            if (_position == _receivedCount && !ReceiveMore())
                return ReadResult.Closed;

            byte current = _received[_position++];

            if (current == '\n')
            {
                if (_skippingLongLine)
                {
                    _skippingLongLine = false;
                    continue;
                }
                return FinishLine(out line);
            }

            if (_skippingLongLine)
                continue;

            _line.Add(current);
            if (_line.Count > MaxLineBytes)
            {
                _line.Clear();
                _skippingLongLine = true;
                return ReadResult.TooLong;
            }
        }
    }

    private bool ReceiveMore()
    {
        try
        {
            _position = 0;
            _receivedCount = _socket.Receive(_received);
            if (_receivedCount > 0)
                return true;
            Close("the other side closed the connection");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
        {
            _receivedCount = 0;
            Close("no heartbeat, network lost");
        }
        catch (SocketException ex)
        {
            _receivedCount = 0;
            Close($"connection broken: {ex.SocketErrorCode}");
        }
        catch (ObjectDisposedException)
        {
            _receivedCount = 0;
            Close("connection closed");
        }
        return false;
    }

    private ReadResult FinishLine(out string line)
    {
        line = "";
        if (_line.Count > 0 && _line[^1] == '\r')
            _line.RemoveAt(_line.Count - 1);

        bool printable = _line.All(b => b >= 32 && b <= 126);
        if (printable)
            line = Encoding.ASCII.GetString(_line.ToArray());

        _line.Clear();
        return printable ? ReadResult.Line : ReadResult.BadBytes;
    }

    public void Send(string line) => SendBytes(Encoding.UTF8.GetBytes(line + "\n"));

    // Only puts bytes in the outbox, never blocks.
    public void SendBytes(byte[] data)
    {
        try
        {
            if (!_outbox.TryAdd(data))
                Close("too many unread messages");
        }
        catch (InvalidOperationException)
        {
            // The connection is already closed.
        }
    }

    // The only thread that writes to this socket.
    private void WriteLoop()
    {
        byte[] ping = Encoding.ASCII.GetBytes("PING\n");
        try
        {
            while (!_outbox.IsCompleted)
            {
                if (_outbox.TryTake(out byte[]? data, HeartbeatMs))
                    SendAll(data);
                else if (_sendHeartbeat && !_outbox.IsAddingCompleted)
                    SendAll(ping);
            }
        }
        catch (SocketException ex)
        {
            Close($"send failed: {ex.SocketErrorCode}");
        }
        catch (ObjectDisposedException)
        {
            Close("connection closed");
        }
    }

    // One Send may take only part.
    private void SendAll(byte[] data)
    {
        int sent = 0;
        while (sent < data.Length)
            sent += _socket.Send(data, sent, data.Length - sent, SocketFlags.None);
    }

    public void CloseAfterSending()
    {
        _outbox.CompleteAdding();
        _writer.Join(1000);
        Close("closed by this side");
    }

    public void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
            return;

        CloseReason = reason;
        _outbox.CompleteAdding();
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            // The connection is already broken.
        }
        _socket.Close();
    }
}

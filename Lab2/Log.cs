// Lab 2: one log line with time and endpoint.
namespace Lab2;

public static class Log
{
    public static void Write(string who, string text) =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{who}] {text}");
}

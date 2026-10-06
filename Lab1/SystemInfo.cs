using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lab1;

/// <summary>Prints the hardware / software facts the report asks for (the parts we can read from code).</summary>
public static class SystemInfo
{
    public static void Print()
    {
        // GC.GetGCMemoryInfo().TotalAvailableMemoryBytes = physical RAM the runtime can see.
        double ramGb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);

        Console.WriteLine($"CPU                : {CpuName()}");
        Console.WriteLine($"Logical processors : {Environment.ProcessorCount}");
        Console.WriteLine($"RAM                : {ramGb:F1} GB");
        Console.WriteLine($"OS                 : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Runtime            : {RuntimeInformation.FrameworkDescription} " +
                          $"({RuntimeInformation.ProcessArchitecture})");
        Console.WriteLine("Physical cores, disk type and the USB drive: see Task Manager -> Performance.");
    }

    private static string CpuName()
    {
        // The Registry is Windows-only, so we check the OS first.
        if (!OperatingSystem.IsWindows()) return "unknown";

        using RegistryKey? key =
            Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

        // ?.  = "if the thing on the left is null, the whole expression is null (don't crash)"
        // ?? = "if the left side is null, use the right side instead"
        return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "unknown";
    }
}

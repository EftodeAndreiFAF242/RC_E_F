using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Lab1;

/// <summary>Prints the hardware and runtime facts for the report that can be read from code.</summary>
public static class SystemInfo
{
    public static void Print()
    {
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
        if (!OperatingSystem.IsWindows()) return "unknown";

        using RegistryKey? key =
            Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

        return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "unknown";
    }
}

package lab1;

import java.lang.foreign.Arena;
import java.lang.foreign.FunctionDescriptor;
import java.lang.foreign.Linker;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.SymbolLookup;
import java.lang.foreign.ValueLayout;
import java.lang.invoke.MethodHandle;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;

/**
 * Peak working set of this process; Java has no API for it, so it calls the same Windows function .NET uses
 * (K32GetProcessMemoryInfo) through the Foreign Function API, which keeps the C# and Java numbers comparable.
 */
final class PeakMemory {
    private PeakMemory() { }

    static long peakBytes() {
        try {
            if (System.getProperty("os.name").toLowerCase(Locale.ROOT).contains("win"))
                return windowsPeakWorkingSet();
            return linuxPeakRss();
        } catch (Throwable e) {
            return -1;
        }
    }

    private static long windowsPeakWorkingSet() throws Throwable {
        Linker linker = Linker.nativeLinker();

        try (Arena arena = Arena.ofConfined()) {
            SymbolLookup kernel32 = SymbolLookup.libraryLookup("kernel32.dll", arena);

            MethodHandle getCurrentProcess = linker.downcallHandle(
                    kernel32.find("GetCurrentProcess").orElseThrow(),
                    FunctionDescriptor.of(ValueLayout.ADDRESS));

            MethodHandle getProcessMemoryInfo = linker.downcallHandle(
                    kernel32.find("K32GetProcessMemoryInfo").orElseThrow(),
                    FunctionDescriptor.of(ValueLayout.JAVA_INT, ValueLayout.ADDRESS, ValueLayout.ADDRESS, ValueLayout.JAVA_INT));

            // PROCESS_MEMORY_COUNTERS is 72 bytes on 64-bit Windows; PeakWorkingSetSize is the 8-byte field at offset 8.
            int size = 72;
            MemorySegment counters = arena.allocate(size, 8);
            counters.set(ValueLayout.JAVA_INT, 0, size);

            MemorySegment process = (MemorySegment) getCurrentProcess.invokeExact();
            int ok = (int) getProcessMemoryInfo.invokeExact(process, counters, size);
            return ok != 0 ? counters.get(ValueLayout.JAVA_LONG, 8) : -1;
        }
    }

    private static long linuxPeakRss() throws Exception {
        for (String line : Files.readAllLines(Path.of("/proc/self/status")))
            if (line.startsWith("VmHWM:"))
                return Long.parseLong(line.replaceAll("\\D", "")) * 1024;
        return -1;
    }
}

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
 * Reads the PEAK working set of this process: the most physical RAM it has used since it started.
 *
 * C# gets this with one line: Process.GetCurrentProcess().PeakWorkingSet64.
 * Java's standard library has no such method, so we ask Windows ourselves, using the same Windows
 * function .NET uses internally: K32GetProcessMemoryInfo (in kernel32.dll). Java 22+ can call
 * native functions directly with the "Foreign Function and Memory API" (java.lang.foreign).
 * Because the numbers come from the same Windows counter, the C# and Java memory results are comparable.
 */
final class PeakMemory {
    private PeakMemory() { }

    /** Peak working set in bytes, or -1 if it can't be read. */
    static long peakBytes() {
        try {
            if (System.getProperty("os.name").toLowerCase(Locale.ROOT).contains("win"))
                return windowsPeakWorkingSet();
            return linuxPeakRss();
        } catch (Throwable e) {   // Throwable: MethodHandle.invokeExact is declared to throw Throwable
            return -1;
        }
    }

    private static long windowsPeakWorkingSet() throws Throwable {
        Linker linker = Linker.nativeLinker();   // knows how to call C functions on this platform

        // An Arena owns native memory; try-with-resources frees it at the end of the block.
        try (Arena arena = Arena.ofConfined()) {
            SymbolLookup kernel32 = SymbolLookup.libraryLookup("kernel32.dll", arena);

            // HANDLE GetCurrentProcess(void);
            MethodHandle getCurrentProcess = linker.downcallHandle(
                    kernel32.find("GetCurrentProcess").orElseThrow(),
                    FunctionDescriptor.of(ValueLayout.ADDRESS));

            // BOOL K32GetProcessMemoryInfo(HANDLE process, PROCESS_MEMORY_COUNTERS* counters, DWORD size);
            MethodHandle getProcessMemoryInfo = linker.downcallHandle(
                    kernel32.find("K32GetProcessMemoryInfo").orElseThrow(),
                    FunctionDescriptor.of(ValueLayout.JAVA_INT, ValueLayout.ADDRESS, ValueLayout.ADDRESS, ValueLayout.JAVA_INT));

            // PROCESS_MEMORY_COUNTERS on 64-bit Windows is 72 bytes:
            //   offset 0: DWORD cb (the size of the structure)   offset 4: DWORD PageFaultCount
            //   offset 8: SIZE_T PeakWorkingSetSize  <- what we want   ... then 7 more SIZE_T fields
            int size = 72;
            MemorySegment counters = arena.allocate(size, 8);
            counters.set(ValueLayout.JAVA_INT, 0, size);

            MemorySegment process = (MemorySegment) getCurrentProcess.invokeExact();
            int ok = (int) getProcessMemoryInfo.invokeExact(process, counters, size);
            return ok != 0 ? counters.get(ValueLayout.JAVA_LONG, 8) : -1;
        }
    }

    /** On Linux the same number is the "VmHWM" line ("high water mark") of /proc/self/status, in kB. */
    private static long linuxPeakRss() throws Exception {
        for (String line : Files.readAllLines(Path.of("/proc/self/status")))
            if (line.startsWith("VmHWM:"))
                return Long.parseLong(line.replaceAll("\\D", "")) * 1024;   // \D = "not a digit"
        return -1;
    }
}

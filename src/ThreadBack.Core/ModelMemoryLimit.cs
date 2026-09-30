using System.Runtime.InteropServices;

namespace ThreadBack.Core;

// Windows job limits the private memory committed by one model worker.
public sealed class ModelMemoryLimit : IDisposable
{
    private IntPtr handle;
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitProcessMemory = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, uint infoClass, ref ExtendedLimitInformation information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    public ModelMemoryLimit(System.Diagnostics.Process process, int gigabytes)
    {
        if (gigabytes is not (6 or 8 or 10)) throw new ArgumentOutOfRangeException(nameof(gigabytes));
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not create the model memory limit.");
        try
        {
            var information = new ExtendedLimitInformation();
            information.BasicLimitInformation.LimitFlags = JobObjectLimitProcessMemory;
            information.ProcessMemoryLimit = new UIntPtr(checked((ulong)gigabytes * 1024 * 1024 * 1024));
            if (!SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ref information, (uint)Marshal.SizeOf<ExtendedLimitInformation>()))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not set the model memory limit.");
            if (!AssignProcessToJobObject(handle, process.Handle))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not apply the model memory limit.");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
}

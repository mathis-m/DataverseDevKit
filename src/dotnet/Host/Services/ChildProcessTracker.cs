using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DataverseDevKit.Host.Services;

/// <summary>
/// Tracks child processes and ensures they are terminated when the parent process exits.
/// On Windows, uses a Job Object to automatically kill child processes.
/// </summary>
public static class ChildProcessTracker
{
    private static nint s_jobHandle;
    private static bool s_initialized;
    private static readonly object s_lock = new();

    /// <summary>
    /// Initializes the child process tracker. Must be called once at application startup.
    /// </summary>
    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (s_lock)
        {
            if (s_initialized)
            {
                return;
            }

            s_jobHandle = CreateJobObject(IntPtr.Zero, null);
            if (s_jobHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create job object");
            }

            // Configure the job to kill all processes when the job handle is closed
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            nint infoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);
                if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure job object");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            s_initialized = true;
        }
    }

    /// <summary>
    /// Adds a process to the job object so it will be terminated when the parent exits.
    /// </summary>
    /// <param name="process">The process to track.</param>
    public static void AddProcess(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (s_lock)
        {
            if (!s_initialized)
            {
                Initialize();
            }

            if (s_jobHandle != IntPtr.Zero && process != null && !process.HasExited)
            {
                if (!AssignProcessToJobObject(s_jobHandle, process.Handle))
                {
                    // Don't throw - this might fail if the process has already exited
                    // or is already assigned to another job
                    var error = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"Warning: Failed to assign process {process.Id} to job object. Error: {error}");
                }
            }
        }
    }

    #region Native Methods

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(nint hJob, JobObjectInfoType infoType, nint lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    #endregion
}

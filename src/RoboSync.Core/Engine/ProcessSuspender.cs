using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RoboSync.Core.Engine;

/// <summary>
/// Pauses and resumes a running process by suspending/resuming all of its threads.
/// Robocopy has no native pause, so this is how the UI implements the Pause button.
/// Uses the stable ntdll entry points that the Windows debugger and Process Explorer rely on.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProcessSuspender
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern uint NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern uint NtResumeProcess(IntPtr processHandle);

    /// <summary>Suspends every thread of the given process. No-op if it has already exited.</summary>
    public static bool Suspend(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.HasExited)
        {
            return false;
        }

        return NtSuspendProcess(process.Handle) == 0;
    }

    /// <summary>Resumes every thread of a previously suspended process.</summary>
    public static bool Resume(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (process.HasExited)
        {
            return false;
        }

        return NtResumeProcess(process.Handle) == 0;
    }
}

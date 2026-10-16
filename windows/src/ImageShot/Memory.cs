using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace ImageShot
{
    // Et skærmbillede af flere store skærme fylder 100+ MB. Efter hver optagelse frigives det igen,
    // så ImageShot ikke ligger og holder på hukommelse i systembakken.
    static class Memory
    {
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        public static void Trim()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Process p = Process.GetCurrentProcess())
                    SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        public static long PrivateMB()
        {
            using (Process p = Process.GetCurrentProcess()) { p.Refresh(); return p.PrivateMemorySize64 / (1024 * 1024); }
        }
    }
}

using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.Windows.Native;

internal static class Kernel32
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandleW(IntPtr lpModuleName);
}
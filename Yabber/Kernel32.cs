using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Yabber {
    public static class Kernel32 {
        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);
        
        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        static void LoadLibrary(string path) {
            IntPtr handle = LoadLibraryW(path);
            if (handle == IntPtr.Zero) {
                uint error = GetLastError();
                throw new DllNotFoundException($"{Path.GetFileName(path)} not found at path {path}\n" +
                                               $"Last Error = {error}");
            }
        }
    }
}
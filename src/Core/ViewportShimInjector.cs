using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GpgPatcher
{
    internal sealed class ViewportShimInjectResult
    {
        public int ProcessId { get; set; }

        public string WindowTitle { get; set; }

        public IntPtr ParentWindowHandle { get; set; }

        public IntPtr ChildWindowHandle { get; set; }

        public DisplaySizeSnapshot VisibleChildSize { get; set; }

        public IntPtr RemoteModuleHandle { get; set; }

        public string ShimPath { get; set; }
    }

    internal static class ViewportShimInjector
    {
        private const uint GwChild = 5;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpShowWindow = 0x0040;
        private const uint SwpFrameChanged = 0x0020;
        private const uint ProcessCreateThread = 0x0002;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmWrite = 0x0020;
        private const uint ProcessVmRead = 0x0010;
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint PageReadWrite = 0x04;
        private const uint Infinite = 0xFFFFFFFF;

        public static ViewportShimInjectResult Inject(string baseDirectory)
        {
            var shimPath = Path.Combine(baseDirectory, "GpgViewportShim.dll");
            if (!File.Exists(shimPath))
            {
                throw new FriendlyException(
                    "GpgViewportShim.dll was not found. Build it with scripts\\build-native-viewport-shim.ps1 first.");
            }

            var target = FindTargetWindow();
            if (target == null)
            {
                throw new FriendlyException("Could not find a visible Whiteout Survival crosvm window. Launch the game first.");
            }

            UnloadExistingShim(target.ProcessId, Path.GetFileName(shimPath));
            var remoteModuleHandle = LoadLibraryInProcess(target.ProcessId, shimPath);
            NudgeWindow(target.ParentWindowHandle);
            Thread.Sleep(1000);

            return new ViewportShimInjectResult
            {
                ProcessId = target.ProcessId,
                WindowTitle = target.WindowTitle,
                ParentWindowHandle = target.ParentWindowHandle,
                ChildWindowHandle = target.ChildWindowHandle,
                VisibleChildSize = GetClientSize(target.ChildWindowHandle),
                RemoteModuleHandle = remoteModuleHandle,
                ShimPath = shimPath,
            };
        }

        private static IntPtr LoadLibraryInProcess(int processId, string dllPath)
        {
            var processHandle = OpenProcess(
                ProcessCreateThread | ProcessQueryInformation | ProcessVmOperation | ProcessVmWrite | ProcessVmRead,
                false,
                processId);
            if (processHandle == IntPtr.Zero)
            {
                throw new FriendlyException("Could not open the crosvm process for DLL injection.");
            }

            try
            {
                var bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                var remotePath = VirtualAllocEx(processHandle, IntPtr.Zero, (UIntPtr)bytes.Length, MemCommit | MemReserve, PageReadWrite);
                if (remotePath == IntPtr.Zero)
                {
                    throw new FriendlyException("Could not allocate memory in the crosvm process.");
                }

                try
                {
                    UIntPtr bytesWritten;
                    if (!WriteProcessMemory(processHandle, remotePath, bytes, (UIntPtr)bytes.Length, out bytesWritten))
                    {
                        throw new FriendlyException("Could not write the shim path into the crosvm process.");
                    }

                    var kernel32 = GetModuleHandle("kernel32.dll");
                    var loadLibrary = GetProcAddress(kernel32, "LoadLibraryW");
                    if (loadLibrary == IntPtr.Zero)
                    {
                        throw new FriendlyException("Could not find LoadLibraryW.");
                    }

                    var threadHandle = CreateRemoteThread(
                        processHandle,
                        IntPtr.Zero,
                        UIntPtr.Zero,
                        loadLibrary,
                        remotePath,
                        0,
                        IntPtr.Zero);
                    if (threadHandle == IntPtr.Zero)
                    {
                        throw new FriendlyException("Could not start the remote LoadLibraryW thread.");
                    }

                    try
                    {
                        WaitForSingleObject(threadHandle, Infinite);
                        uint exitCode;
                        if (!GetExitCodeThread(threadHandle, out exitCode) || exitCode == 0)
                        {
                            throw new FriendlyException("The native viewport shim did not load into crosvm.");
                        }

                        return new IntPtr(unchecked((int)exitCode));
                    }
                    finally
                    {
                        CloseHandle(threadHandle);
                    }
                }
                finally
                {
                    VirtualFreeEx(processHandle, remotePath, UIntPtr.Zero, MemRelease);
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        private static void UnloadExistingShim(int processId, string shimFileName)
        {
            IntPtr moduleHandle = IntPtr.Zero;
            using (var process = Process.GetProcessById(processId))
            {
                try
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        if (string.Equals(module.ModuleName, shimFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            moduleHandle = module.BaseAddress;
                            break;
                        }
                    }
                }
                catch
                {
                    return;
                }
            }

            if (moduleHandle == IntPtr.Zero)
            {
                return;
            }

            var processHandle = OpenProcess(ProcessCreateThread | ProcessQueryInformation | ProcessVmOperation, false, processId);
            if (processHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var kernel32 = GetModuleHandle("kernel32.dll");
                var freeLibrary = GetProcAddress(kernel32, "FreeLibrary");
                if (freeLibrary == IntPtr.Zero)
                {
                    return;
                }

                var threadHandle = CreateRemoteThread(
                    processHandle,
                    IntPtr.Zero,
                    UIntPtr.Zero,
                    freeLibrary,
                    moduleHandle,
                    0,
                    IntPtr.Zero);
                if (threadHandle == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    WaitForSingleObject(threadHandle, 5000);
                }
                finally
                {
                    CloseHandle(threadHandle);
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        private static void NudgeWindow(IntPtr windowHandle)
        {
            NativeRect rect;
            if (!GetWindowRect(windowHandle, out rect))
            {
                return;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, width + 1, height, SwpNoMove | SwpNoZOrder | SwpShowWindow | SwpFrameChanged);
            Thread.Sleep(150);
            SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, width, height, SwpNoMove | SwpNoZOrder | SwpShowWindow | SwpFrameChanged);
        }

        private static TargetWindow FindTargetWindow()
        {
            TargetWindow fallback = null;
            foreach (var process in Process.GetProcessesByName("crosvm"))
            {
                using (process)
                {
                    var parent = process.MainWindowHandle;
                    if (parent == IntPtr.Zero || !IsWindowVisible(parent))
                    {
                        continue;
                    }

                    var child = GetWindow(parent, GwChild);
                    if (child == IntPtr.Zero || !IsWindowVisible(child))
                    {
                        continue;
                    }

                    var title = GetWindowTitle(parent);
                    var target = new TargetWindow
                    {
                        ProcessId = process.Id,
                        WindowTitle = title,
                        ParentWindowHandle = parent,
                        ChildWindowHandle = child,
                    };

                    if (!string.IsNullOrEmpty(title)
                        && title.IndexOf("Whiteout Survival", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return target;
                    }

                    if (fallback == null && !string.IsNullOrEmpty(title))
                    {
                        fallback = target;
                    }
                }
            }

            return fallback;
        }

        private static DisplaySizeSnapshot GetClientSize(IntPtr windowHandle)
        {
            NativeRect rect;
            if (!GetClientRect(windowHandle, out rect))
            {
                return null;
            }

            return new DisplaySizeSnapshot(rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private static string GetWindowTitle(IntPtr windowHandle)
        {
            var builder = new StringBuilder(256);
            GetWindowText(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }

        private sealed class TargetWindow
        {
            public int ProcessId { get; set; }

            public string WindowTitle { get; set; }

            public IntPtr ParentWindowHandle { get; set; }

            public IntPtr ChildWindowHandle { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint freeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, UIntPtr size, out UIntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(
            IntPtr process,
            IntPtr threadAttributes,
            UIntPtr stackSize,
            IntPtr startAddress,
            IntPtr parameter,
            uint creationFlags,
            IntPtr threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr thread, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string procedureName);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}

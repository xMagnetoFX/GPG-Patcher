using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GpgPatcher
{
    internal sealed class ViewportForceResult
    {
        public bool Success { get; set; }

        public int ProcessId { get; set; }

        public string WindowTitle { get; set; }

        public IntPtr ParentWindowHandle { get; set; }

        public IntPtr ChildWindowHandle { get; set; }

        public DisplaySizeSnapshot FinalChildSize { get; set; }

        public int Attempts { get; set; }
    }

    internal static class ViewportForcer
    {
        private const uint GwChild = 5;
        private const uint SwpShowWindow = 0x0040;

        public static ViewportForceResult ForceTargetViewport(
            TimeSpan duration,
            TimeSpan interval,
            bool allowOffscreen,
            ResolutionProfile targetResolution)
        {
            var target = FindTargetWindow();
            if (target == null)
            {
                throw new FriendlyException("Could not find a visible Whiteout Survival crosvm window. Launch the game first.");
            }

            var attempts = Math.Max(1, (int)Math.Ceiling(duration.TotalMilliseconds / interval.TotalMilliseconds));
            DisplaySizeSnapshot finalChildSize = null;

            for (var i = 0; i < attempts; i++)
            {
                ForceOnce(target.ParentWindowHandle, target.ChildWindowHandle, allowOffscreen, targetResolution);
                Thread.Sleep(interval);
                finalChildSize = GetClientSize(target.ChildWindowHandle);
            }

            return new ViewportForceResult
            {
                Success = finalChildSize != null
                    && finalChildSize.Width == targetResolution.Width
                    && finalChildSize.Height == targetResolution.Height,
                ProcessId = target.ProcessId,
                WindowTitle = target.WindowTitle,
                ParentWindowHandle = target.ParentWindowHandle,
                ChildWindowHandle = target.ChildWindowHandle,
                FinalChildSize = finalChildSize,
                Attempts = attempts,
            };
        }

        private static void ForceOnce(
            IntPtr parentWindowHandle,
            IntPtr childWindowHandle,
            bool allowOffscreen,
            ResolutionProfile targetResolution)
        {
            NativeRect parentRect;
            NativeRect childRect;
            if (!GetWindowRect(parentWindowHandle, out parentRect) || !GetWindowRect(childWindowHandle, out childRect))
            {
                throw new FriendlyException("Could not read the crosvm window bounds.");
            }

            var topChrome = Math.Max(0, childRect.Top - parentRect.Top);
            var targetParentHeight = targetResolution.Height + topChrome;
            var targetX = 0;
            var targetY = 0;

            var monitorBounds = GetMonitorBounds(parentWindowHandle);
            if (monitorBounds != null
                && targetResolution.Width <= monitorBounds.Width
                && targetResolution.Height <= monitorBounds.Height)
            {
                targetX = monitorBounds.Left;
                targetY = monitorBounds.Top - topChrome;
            }
            else if (!allowOffscreen)
            {
                throw new FriendlyException(
                    "A " + targetResolution.Value
                    + " viewport is larger than the current monitor bounds and would be cropped. The offscreen resize was not applied.");
            }

            if (!SetWindowPos(
                parentWindowHandle,
                IntPtr.Zero,
                targetX,
                targetY,
                targetResolution.Width,
                targetParentHeight,
                SwpShowWindow))
            {
                throw new FriendlyException("Windows rejected the viewport resize request.");
            }
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

        private static MonitorBounds GetMonitorBounds(IntPtr windowHandle)
        {
            var monitor = MonitorFromWindow(windowHandle, 2);
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var info = new MonitorInfo();
            info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            if (!GetMonitorInfo(monitor, ref info))
            {
                return null;
            }

            return new MonitorBounds
            {
                Left = info.Monitor.Left,
                Top = info.Monitor.Top,
                Width = info.Monitor.Right - info.Monitor.Left,
                Height = info.Monitor.Bottom - info.Monitor.Top,
            };
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

        private sealed class MonitorBounds
        {
            public int Left { get; set; }

            public int Top { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    }
}

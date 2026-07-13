using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GpgPatcher
{
    internal sealed class WindowBoundsSnapshot
    {
        public WindowBoundsSnapshot(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; }

        public int Top { get; }

        public int Right { get; }

        public int Bottom { get; }

        public int Width
        {
            get { return Right - Left; }
        }

        public int Height
        {
            get { return Bottom - Top; }
        }

        public override string ToString()
        {
            return Left + "," + Top + " " + Width + "x" + Height;
        }
    }

    internal sealed class ViewportDiagnosticResult
    {
        public bool Found { get; set; }

        public int ProcessId { get; set; }

        public string WindowTitle { get; set; }

        public IntPtr ParentWindowHandle { get; set; }

        public IntPtr ChildWindowHandle { get; set; }

        public WindowBoundsSnapshot ParentWindowRect { get; set; }

        public WindowBoundsSnapshot ChildWindowRect { get; set; }

        public DisplaySizeSnapshot ChildClientSize { get; set; }

        public WindowBoundsSnapshot DwmFrameBounds { get; set; }

        public int? WindowDpi { get; set; }

        public WindowBoundsSnapshot MonitorBounds { get; set; }

        public string Presentation { get; set; }
    }

    internal static class ViewportDiagnostics
    {
        private const uint GwChild = 5;
        private const int DwmwaExtendedFrameBounds = 9;

        public static ViewportDiagnosticResult Capture(
            HostViewportLogEntry hostViewport,
            ResolutionProfile targetResolution)
        {
            var target = FindTargetWindow();
            if (target == null)
            {
                return new ViewportDiagnosticResult
                {
                    Found = false,
                    Presentation = "no live crosvm window",
                };
            }

            var childClientSize = GetClientSize(target.ChildWindowHandle);
            var monitorBounds = GetMonitorBounds(target.ParentWindowHandle);
            var result = new ViewportDiagnosticResult
            {
                Found = true,
                ProcessId = target.ProcessId,
                WindowTitle = target.WindowTitle,
                ParentWindowHandle = target.ParentWindowHandle,
                ChildWindowHandle = target.ChildWindowHandle,
                ParentWindowRect = GetWindowBounds(target.ParentWindowHandle),
                ChildWindowRect = GetWindowBounds(target.ChildWindowHandle),
                ChildClientSize = childClientSize,
                DwmFrameBounds = GetDwmFrameBounds(target.ParentWindowHandle),
                WindowDpi = GetWindowDpi(target.ParentWindowHandle),
                MonitorBounds = monitorBounds,
            };

            result.Presentation = Classify(childClientSize, hostViewport, monitorBounds, targetResolution);
            return result;
        }

        private static string Classify(
            DisplaySizeSnapshot childClientSize,
            HostViewportLogEntry hostViewport,
            WindowBoundsSnapshot monitorBounds,
            ResolutionProfile targetResolution)
        {
            var target = new DisplaySizeSnapshot(targetResolution.Width, targetResolution.Height);

            if (IsSameSize(childClientSize, target))
            {
                if (monitorBounds != null
                    && (monitorBounds.Width < target.Width || monitorBounds.Height < target.Height))
                {
                    return "cropped/offscreen " + targetResolution.Name + " window";
                }

                return "native-sized " + targetResolution.Name + " viewport";
            }

            if (hostViewport != null && IsSameSize(hostViewport.ViewportSize, target))
            {
                return "virtual " + targetResolution.Name + " backing surface, downsampled into the visible window";
            }

            if (hostViewport != null && hostViewport.ViewportSize != null)
            {
                return "visible-window limited; crosvm host viewport is " + hostViewport.ViewportSize;
            }

            return "live window found; host viewport log not available";
        }

        private static bool IsSameSize(DisplaySizeSnapshot left, DisplaySizeSnapshot right)
        {
            return left != null
                && right != null
                && left.Width == right.Width
                && left.Height == right.Height;
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

        private static WindowBoundsSnapshot GetWindowBounds(IntPtr windowHandle)
        {
            NativeRect rect;
            return GetWindowRect(windowHandle, out rect) ? ToBounds(rect) : null;
        }

        private static WindowBoundsSnapshot GetDwmFrameBounds(IntPtr windowHandle)
        {
            NativeRect rect;
            var size = Marshal.SizeOf(typeof(NativeRect));
            return DwmGetWindowAttribute(windowHandle, DwmwaExtendedFrameBounds, out rect, size) == 0
                ? ToBounds(rect)
                : null;
        }

        private static int? GetWindowDpi(IntPtr windowHandle)
        {
            try
            {
                var dpi = GetDpiForWindow(windowHandle);
                return dpi == 0 ? (int?)null : (int)dpi;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        private static WindowBoundsSnapshot GetMonitorBounds(IntPtr windowHandle)
        {
            var monitor = MonitorFromWindow(windowHandle, 2);
            if (monitor == IntPtr.Zero)
            {
                return null;
            }

            var info = new MonitorInfo();
            info.Size = Marshal.SizeOf(typeof(MonitorInfo));
            return GetMonitorInfo(monitor, ref info) ? ToBounds(info.Monitor) : null;
        }

        private static WindowBoundsSnapshot ToBounds(NativeRect rect)
        {
            return new WindowBoundsSnapshot(rect.Left, rect.Top, rect.Right, rect.Bottom);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect Work;
            public uint Flags;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);

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
        private static extern uint GetDpiForWindow(IntPtr hwnd);
    }
}

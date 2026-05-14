using System;
using System.Collections.Generic;

namespace GpgPatcher
{
    internal sealed class DisplaySizeSnapshot
    {
        public DisplaySizeSnapshot(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public long Area
        {
            get { return (long)Width * Height; }
        }

        public override string ToString()
        {
            return Width + "x" + Height;
        }
    }

    internal sealed class ServiceLaunchLogEntry
    {
        public ServiceLaunchLogEntry(
            DateTimeOffset? timestamp,
            int? displayDensity,
            DisplaySizeSnapshot displaySize,
            IReadOnlyList<DisplaySizeSnapshot> availableDisplaySizes,
            int? displayId,
            string rawLine)
        {
            Timestamp = timestamp;
            DisplayDensity = displayDensity;
            DisplaySize = displaySize;
            AvailableDisplaySizes = availableDisplaySizes;
            DisplayId = displayId;
            RawLine = rawLine;
        }

        public DateTimeOffset? Timestamp { get; }

        public int? DisplayDensity { get; }

        public DisplaySizeSnapshot DisplaySize { get; }

        public IReadOnlyList<DisplaySizeSnapshot> AvailableDisplaySizes { get; }

        public int? DisplayId { get; }

        public string RawLine { get; }
    }

    internal sealed class ResolutionCapLogEntry
    {
        public ResolutionCapLogEntry(DateTimeOffset? timestamp, string cap, string rawLine)
        {
            Timestamp = timestamp;
            Cap = cap;
            RawLine = rawLine;
        }

        public DateTimeOffset? Timestamp { get; }

        public string Cap { get; }

        public string RawLine { get; }
    }

    internal sealed class AndroidSerialDisplayEntry
    {
        public AndroidSerialDisplayEntry(DisplaySizeSnapshot displaySize, string rawLine)
        {
            DisplaySize = displaySize;
            RawLine = rawLine;
        }

        public DisplaySizeSnapshot DisplaySize { get; }

        public string RawLine { get; }
    }

    internal sealed class AndroidLogicalDisplayEntry
    {
        public AndroidLogicalDisplayEntry(
            int displayId,
            DisplaySizeSnapshot realDisplaySize,
            DisplaySizeSnapshot appDisplaySize,
            int? density,
            string rawLine)
        {
            DisplayId = displayId;
            RealDisplaySize = realDisplaySize;
            AppDisplaySize = appDisplaySize;
            Density = density;
            RawLine = rawLine;
        }

        public int DisplayId { get; }

        public DisplaySizeSnapshot RealDisplaySize { get; }

        public DisplaySizeSnapshot AppDisplaySize { get; }

        public int? Density { get; }

        public string RawLine { get; }
    }

    internal sealed class HostViewportLogEntry
    {
        public HostViewportLogEntry(int scanoutId, DisplaySizeSnapshot viewportSize, string rawLine)
        {
            ScanoutId = scanoutId;
            ViewportSize = viewportSize;
            RawLine = rawLine;
        }

        public int ScanoutId { get; }

        public DisplaySizeSnapshot ViewportSize { get; }

        public string RawLine { get; }
    }

    internal sealed class PatchStatus
    {
        public string InstalledVersion { get; set; }

        public bool IsCompatible { get; set; }

        public string CompatibilityState { get; set; }

        public string CompatibilityMessage { get; set; }

        public bool AvailableSettingsPatched { get; set; }

        public bool LaunchSettingsPatched { get; set; }

        public bool MonitorDisplayPatched { get; set; }

        public bool RuntimeDisplaySettingsPatched { get; set; }

        public bool SharpeningFilterPatched { get; set; }

        public bool AccountLimitBypassPatched { get; set; }

        public bool AddAccountDeepLinkPatched { get; set; }

        public bool HookAssemblyReferencePresent { get; set; }

        public bool HookDllPresent { get; set; }

        public bool HookDllCompatible { get; set; }

        public bool BackupPresent { get; set; }

        public bool PhenotypeOverridePresent { get; set; }

        public string PhenotypeOverrideValue { get; set; }

        public bool IsPatched
        {
            get
            {
                return AvailableSettingsPatched
                    && LaunchSettingsPatched
                    && MonitorDisplayPatched
                    && RuntimeDisplaySettingsPatched
                    && SharpeningFilterPatched
                    && AccountLimitBypassPatched
                    && AddAccountDeepLinkPatched
                    && HookAssemblyReferencePresent
                    && HookDllPresent
                    && HookDllCompatible;
            }
        }
    }
}

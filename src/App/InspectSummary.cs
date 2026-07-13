using System;

namespace GpgPatcher.Gui
{
    internal sealed class InspectSummary
    {
        public string Version { get; set; }

        public string Compatible { get; set; }

        public string CompatibilityState { get; set; }

        public string CompatibilityMessage { get; set; }

        public string ServiceLibPatched { get; set; }

        public string HookDllPresent { get; set; }

        public string HookDllCompatible { get; set; }

        public string BackupPresent { get; set; }

        public string Density { get; set; }

        public string DisplaySize { get; set; }

        public string GuestDisplay { get; set; }

        public string ResolutionCap { get; set; }

        public string TargetResolution { get; set; }

        public string AvailableSettingsHook { get; set; }

        public string LaunchSettingsHook { get; set; }

        public string MonitorDisplayHook { get; set; }

        public string RuntimeDisplayHook { get; set; }

        public string VirtualGuestDisplayHook { get; set; }

        public string ShowWindowRequestHook { get; set; }

        public string SharpeningFilterHook { get; set; }

        public string AccountLimitBypassHook { get; set; }

        public string AddAccountDeepLinkHook { get; set; }

        public string ExactInstanceLaunchHook { get; set; }

        public bool HasExactInstanceLaunch
        {
            get { return IsTruthy(ExactInstanceLaunchHook); }
        }

        public string PhenotypeOverridePresent { get; set; }

        public bool IsCompatible
        {
            get { return IsTruthy(Compatible); }
        }

        public bool IsPatched
        {
            get
            {
                return IsTruthy(ServiceLibPatched)
                    && IsTruthy(HookDllPresent)
                    && IsTruthy(AvailableSettingsHook)
                    && IsTruthy(LaunchSettingsHook)
                    && IsTruthy(MonitorDisplayHook)
                    && IsTruthy(RuntimeDisplayHook)
                    && IsTruthy(VirtualGuestDisplayHook)
                    && IsTruthy(ShowWindowRequestHook)
                    && IsTruthy(SharpeningFilterHook)
                    && IsTruthy(AccountLimitBypassHook)
                    && IsTruthy(AddAccountDeepLinkHook)
                    && IsTruthy(ExactInstanceLaunchHook)
                    && IsTruthy(HookDllCompatible);
            }
        }

        public bool HasBackup
        {
            get { return IsTruthy(BackupPresent); }
        }

        public bool HasPhenotypeOverride
        {
            get { return IsTruthy(PhenotypeOverridePresent); }
        }

        public static InspectSummary Parse(string output)
        {
            var summary = new InspectSummary();
            if (string.IsNullOrWhiteSpace(output))
            {
                return summary;
            }

            var lines = output.Replace("\r\n", "\n").Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                ReadValue(line, "version:", value => summary.Version = value);
                ReadValue(line, "compatible:", value => summary.Compatible = value);
                ReadValue(line, "compatibility state:", value => summary.CompatibilityState = value);
                ReadValue(line, "compatibility message:", value => summary.CompatibilityMessage = value);
                ReadValue(line, "service lib patched:", value => summary.ServiceLibPatched = value);
                ReadValue(line, "available-settings hook:", value => summary.AvailableSettingsHook = value);
                ReadValue(line, "launch-settings hook:", value => summary.LaunchSettingsHook = value);
                ReadValue(line, "monitor-display hook:", value => summary.MonitorDisplayHook = value);
                ReadValue(line, "runtime-display hook:", value => summary.RuntimeDisplayHook = value);
                ReadValue(line, "virtual guest-display hook:", value => summary.VirtualGuestDisplayHook = value);
                ReadValue(line, "show-window request hook:", value => summary.ShowWindowRequestHook = value);
                ReadValue(line, "sharpening-filter hook:", value => summary.SharpeningFilterHook = value);
                ReadValue(line, "account-limit bypass hook:", value => summary.AccountLimitBypassHook = value);
                ReadValue(line, "add-account deep-link hook:", value => summary.AddAccountDeepLinkHook = value);
                ReadValue(line, "exact-instance launch hook:", value => summary.ExactInstanceLaunchHook = value);
                ReadValue(line, "hook dll present:", value => summary.HookDllPresent = value);
                ReadValue(line, "hook dll compatible:", value => summary.HookDllCompatible = value);
                ReadValue(line, "backup present:", value => summary.BackupPresent = value);
                ReadValue(line, "phenotype override present:", value => summary.PhenotypeOverridePresent = value);
                ReadValue(line, "density:", value => summary.Density = value);
                ReadValue(line, "display size:", value => summary.DisplaySize = value);
                ReadValue(line, "android serial display:", value => summary.GuestDisplay = value);
                ReadValue(line, "resolution cap:", value => summary.ResolutionCap = value);
                ReadValue(line, "target resolution:", value => summary.TargetResolution = value);
            }

            return summary;
        }

        private static void ReadValue(string line, string prefix, Action<string> setter)
        {
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            setter(line.Substring(prefix.Length).Trim());
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "True", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "On", StringComparison.OrdinalIgnoreCase);
        }
    }
}

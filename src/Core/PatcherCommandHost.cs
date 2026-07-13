using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Xml.Linq;

namespace GpgPatcher
{
    public static class PatcherCommandHost
    {
        public static int Run(string[] args)
        {
            try
            {
                return RunCore(args ?? Array.Empty<string>());
            }
            catch (FriendlyException ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                WriteHeadlessErrorLog(ex);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                WriteHeadlessErrorLog(ex);
                return 1;
            }
        }

        private static int RunCore(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintUsage();
                return 0;
            }

            var layout = PlayGamesInstallLayout.CreateDefault();
            var command = args[0].Trim().ToLowerInvariant();
            var allowOffscreen = args.Skip(1)
                .Any(argument => string.Equals(argument, "--allow-offscreen", StringComparison.OrdinalIgnoreCase));
            var allowExperimentalShim = args.Skip(1)
                .Any(argument => string.Equals(argument, "--experimental", StringComparison.OrdinalIgnoreCase));

            switch (command)
            {
                case "inspect":
                    Inspect(layout);
                    return 0;
                case "patch":
                    Patch(layout, PatchCommandOptions.Parse(args.Skip(1).ToArray()));
                    return 0;
                case "verify":
                    return Verify(layout);
                case "launch":
                case "launch-account":
                case "play":
                    return LaunchWhiteout(args);
                case "accounts":
                case "list-accounts":
                    return ListAccounts();
                case "pick":
                case "pick-account":
                case "select-account":
                    return PickAccount(layout, args);
                case "pick-index":
                case "pick-account-index":
                case "play-index":
                    return PickAccountByIndex(layout, args);
                case "launch-instance":
                    return LaunchInstance(layout, args);
                case "add-account":
                    return AddAccount(layout);
                case "viewport-diagnose":
                case "diagnose-viewport":
                    return DiagnoseViewport(layout);
                case "force-viewport":
                case "viewport":
                    return ForceViewport(layout, allowOffscreen);
                case "viewport-shim":
                case "inject-viewport-shim":
                    return InjectViewportShim(allowExperimentalShim);
                case "restore":
                    Restore(layout);
                    return 0;
                default:
                    throw new FriendlyException("Unknown command '" + args[0] + "'.");
            }
        }

        private static void Inspect(PlayGamesInstallLayout layout)
        {
            var patchStatus = PatchStatusInspector.Inspect(layout);
            var launch = LogParser.TryGetLatestLaunch(layout.ServiceLogPath, GpgConstants.TargetPackageName);
            var cap = LogParser.TryGetLatestResolutionCap(layout.ServiceLogPath);
            var androidSerial = LogParser.TryGetLatestAndroidSerialDisplay(layout.AndroidSerialLogPath);
            var logicalDisplay = LogParser.TryGetLatestLogicalDisplay(
                layout.AndroidSerialLogPath,
                launch == null ? null : launch.DisplayId);
            var hostViewport = LogParser.TryGetLatestHostViewport(GetGpuSyslogPath(layout));

            Console.WriteLine("Google Play Games");
            Console.WriteLine("  version: " + patchStatus.InstalledVersion);
            Console.WriteLine("  compatible: " + patchStatus.IsCompatible);
            Console.WriteLine("  compatibility state: " + patchStatus.CompatibilityState);
            Console.WriteLine("  compatibility message: " + patchStatus.CompatibilityMessage);
            Console.WriteLine("  service dir: " + layout.ServiceDirectory);
            Console.WriteLine();

            Console.WriteLine("Patch status");
            Console.WriteLine("  service lib patched: " + patchStatus.IsPatched);
            Console.WriteLine("  available-settings hook: " + patchStatus.AvailableSettingsPatched);
            Console.WriteLine("  launch-settings hook: " + patchStatus.LaunchSettingsPatched);
            Console.WriteLine("  monitor-display hook: " + patchStatus.MonitorDisplayPatched);
            Console.WriteLine("  runtime-display hook: " + patchStatus.RuntimeDisplaySettingsPatched);
            Console.WriteLine("  virtual guest-display hook: " + patchStatus.VirtualGuestDisplayPatched);
            Console.WriteLine("  show-window request hook: " + patchStatus.ShowWindowRequestPatched);
            Console.WriteLine("  sharpening-filter hook: " + patchStatus.SharpeningFilterPatched);
            Console.WriteLine("  account-limit bypass hook: " + patchStatus.AccountLimitBypassPatched);
            Console.WriteLine("  add-account deep-link hook: " + patchStatus.AddAccountDeepLinkPatched);
            Console.WriteLine("  exact-instance launch hook: " + patchStatus.ExactInstanceLaunchPatched);
            Console.WriteLine("  hook dll present: " + patchStatus.HookDllPresent);
            Console.WriteLine("  hook dll compatible: " + patchStatus.HookDllCompatible);
            Console.WriteLine("  backup present: " + patchStatus.BackupPresent);
            Console.WriteLine("  target resolution: " + patchStatus.TargetResolution.Value);
            Console.WriteLine("  phenotype override present: " + patchStatus.PhenotypeOverridePresent);
            if (patchStatus.PhenotypeOverridePresent && !string.IsNullOrWhiteSpace(patchStatus.PhenotypeOverrideValue))
            {
                Console.WriteLine("  phenotype override value: " + patchStatus.PhenotypeOverrideValue.Trim());
            }

            Console.WriteLine();
            Console.WriteLine("Latest " + GpgConstants.TargetPackageName + " launch");
            if (launch == null)
            {
                Console.WriteLine("  launch settings: not found in " + layout.ServiceLogPath);
            }
            else
            {
                Console.WriteLine("  timestamp: " + FormatTimestamp(launch.Timestamp));
                Console.WriteLine("  density: " + (launch.DisplayDensity.HasValue ? launch.DisplayDensity.Value.ToString() : "(unknown)"));
                Console.WriteLine("  display size: " + (launch.DisplaySize == null ? "(unknown)" : launch.DisplaySize.ToString()));
                Console.WriteLine("  available sizes: " + LogParser.FormatDisplaySizes(launch.AvailableDisplaySizes));
                Console.WriteLine("  display id: " + (launch.DisplayId.HasValue ? launch.DisplayId.Value.ToString() : "(unknown)"));
            }

            Console.WriteLine();
            Console.WriteLine("Latest related caps and guest display");
            Console.WriteLine("  resolution cap: " + (cap == null ? "(not found)" : cap.Cap));
            Console.WriteLine("  android serial display: " + (androidSerial == null ? "(not found)" : androidSerial.DisplaySize.ToString()));
            Console.WriteLine("  physical display mode: " + FormatLogicalDisplay(logicalDisplay));
            Console.WriteLine("  host viewport: " + FormatHostViewport(hostViewport));
            Console.WriteLine("  virtual viewport requested: " + FormatVirtualViewportRequest(patchStatus));
        }

        private static void Patch(PlayGamesInstallLayout layout, PatchCommandOptions options)
        {
            EnsureAdministrator();
            layout.EnsureInstallationExists();
            layout.EnsureHookBuildExists();

            var patchStatus = PatchStatusInspector.Inspect(layout);
            EnsureCompatibleForPatch(patchStatus);
            if (!patchStatus.BackupPresent && patchStatus.IsPatched)
            {
                throw new FriendlyException(
                    "The service already looks patched, but no pristine backup exists in '" + layout.BackupDirectory + "'. Restore would be unsafe, so patch is aborting.");
            }

            PlayGamesServiceManager.Stop(layout);
            var startedAgain = false;
            try
            {
                BackupOriginalFiles(layout);

                var tempPatchedServiceLib = Path.Combine(Path.GetTempPath(), "gpg-patcher-ServiceLib.dll");
                if (File.Exists(tempPatchedServiceLib))
                {
                    File.Delete(tempPatchedServiceLib);
                }

                var patchResult = ServiceLibPatcher.Patch(layout.ServiceLibPath, tempPatchedServiceLib);
                File.Copy(tempPatchedServiceLib, layout.ServiceLibPath, true);
                File.Delete(tempPatchedServiceLib);

                File.Copy(layout.HookSourcePath, layout.HookTargetPath, true);
                DeleteIfExists(layout.LegacyHookTargetPath);
                ResolutionProfileStorage.WriteApplied(layout, options.Resolution);

                if (options.EnablePhenotypeFallback)
                {
                    PhenotypeFallbackConfiguration.Apply(layout.ServiceConfigPath);
                }
                else if (!options.Resolution.IsUhd)
                {
                    PhenotypeFallbackConfiguration.Clear(layout.ServiceConfigPath);
                }

                PlayGamesServiceManager.Start(layout);
                startedAgain = true;

                Console.WriteLine("Patch complete");
                Console.WriteLine("  available-settings method changed: " + patchResult.AvailableSettingsPatched);
                Console.WriteLine("  launch-settings method changed: " + patchResult.LaunchSettingsPatched);
                Console.WriteLine("  monitor-display method changed: " + patchResult.MonitorDisplayPatched);
                Console.WriteLine("  runtime-display method changed: " + patchResult.RuntimeDisplaySettingsPatched);
                Console.WriteLine("  virtual guest-display method changed: " + patchResult.VirtualGuestDisplayPatched);
                Console.WriteLine("  show-window request method changed: " + patchResult.ShowWindowRequestPatched);
                Console.WriteLine("  sharpening-filter method changed: " + patchResult.SharpeningFilterPatched);
                Console.WriteLine("  account-limit bypass changed: " + patchResult.AccountLimitBypassPatched);
                Console.WriteLine("  add-account deep-link changed: " + patchResult.AddAccountDeepLinkPatched);
                Console.WriteLine("  exact-instance launch changed: " + patchResult.ExactInstanceLaunchPatched);
                Console.WriteLine("  hook dll copied: " + layout.HookTargetPath);
                Console.WriteLine("  target resolution: " + options.Resolution.Value);
                Console.WriteLine("  phenotype fallback applied: " + options.EnablePhenotypeFallback);
                Console.WriteLine();
                Console.WriteLine("Next step");
                Console.WriteLine("  launch Whiteout Survival, then run Verify from the app.");
            }
            finally
            {
                if (!startedAgain)
                {
                    try
                    {
                        PlayGamesServiceManager.Start(layout);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static int Verify(PlayGamesInstallLayout layout)
        {
            var launch = LogParser.TryGetLatestLaunch(layout.ServiceLogPath, GpgConstants.TargetPackageName);
            var cap = LogParser.TryGetLatestResolutionCap(layout.ServiceLogPath);
            var androidSerial = LogParser.TryGetLatestAndroidSerialDisplay(layout.AndroidSerialLogPath);
            var logicalDisplay = LogParser.TryGetLatestLogicalDisplay(
                layout.AndroidSerialLogPath,
                launch == null ? null : launch.DisplayId);
            var hostViewport = LogParser.TryGetLatestHostViewport(GetGpuSyslogPath(layout));
            var patchStatus = PatchStatusInspector.Inspect(layout);

            var targetResolution = patchStatus.TargetResolution;
            var expectedDisplaySize = new DisplaySizeSnapshot(targetResolution.Width, targetResolution.Height);

            var launchMatches = launch != null
                && launch.DisplaySize != null
                && launch.DisplaySize.Width == expectedDisplaySize.Width
                && launch.DisplaySize.Height == expectedDisplaySize.Height
                && launch.AvailableDisplaySizes.Any(size => size.Width == expectedDisplaySize.Width && size.Height == expectedDisplaySize.Height);

            var serialMatches = androidSerial != null
                && androidSerial.DisplaySize.Width == expectedDisplaySize.Width
                && androidSerial.DisplaySize.Height == expectedDisplaySize.Height;

            Console.WriteLine("Verify");
            Console.WriteLine("  expected launch size: " + expectedDisplaySize);
            Console.WriteLine("  expected scaled density: runtime-derived");
            Console.WriteLine("  latest launch size: " + (launch == null || launch.DisplaySize == null ? "(not found)" : launch.DisplaySize.ToString()));
            Console.WriteLine("  latest launch density: " + (launch == null || !launch.DisplayDensity.HasValue ? "(not found)" : launch.DisplayDensity.Value.ToString()));
            Console.WriteLine("  latest available sizes: " + (launch == null ? "(not found)" : LogParser.FormatDisplaySizes(launch.AvailableDisplaySizes)));
            Console.WriteLine("  latest android serial display: " + (androidSerial == null ? "(not found)" : androidSerial.DisplaySize.ToString()));
            Console.WriteLine("  latest physical display mode: " + FormatLogicalDisplay(logicalDisplay));
            Console.WriteLine("  latest host viewport: " + FormatHostViewport(hostViewport));
            Console.WriteLine("  virtual viewport requested: " + FormatVirtualViewportRequest(patchStatus));
            Console.WriteLine("  latest resolution cap: " + (cap == null ? "(not found)" : cap.Cap));
            Console.WriteLine("  managed viewport hooks: monitor "
                + patchStatus.MonitorDisplayPatched
                + ", runtime "
                + patchStatus.RuntimeDisplaySettingsPatched
                + ", virtual "
                + patchStatus.VirtualGuestDisplayPatched
                + ", show-window "
                + patchStatus.ShowWindowRequestPatched
                + ", sharpening "
                + patchStatus.SharpeningFilterPatched
                + ", accounts "
                + patchStatus.AccountLimitBypassPatched
                + ", add-account "
                + patchStatus.AddAccountDeepLinkPatched
                + ", exact-launch "
                + patchStatus.ExactInstanceLaunchPatched);
            Console.WriteLine();

            if (launchMatches && serialMatches)
            {
                Console.WriteLine("PASS: Whiteout Survival is launching with the patched " + targetResolution.Name + " portrait display.");
                if (!patchStatus.MonitorDisplayPatched || !patchStatus.RuntimeDisplaySettingsPatched)
                {
                    Console.WriteLine("WARN: The newer managed viewport hooks are not installed yet; rerun Patch to install the stronger host-display override.");
                }
                if (!patchStatus.VirtualGuestDisplayPatched || !patchStatus.ShowWindowRequestPatched)
                {
                    Console.WriteLine("WARN: The virtual viewport hooks are not installed yet; rerun Patch to request the target backing surface through GPG's managed display path.");
                }
                if (!patchStatus.SharpeningFilterPatched)
                {
                    Console.WriteLine("WARN: The sharpening-filter hook is not installed yet; rerun Patch to force Google's own deblur path on.");
                }
                if (!patchStatus.AccountLimitBypassPatched)
                {
                    Console.WriteLine("WARN: The account-count bypass hook is not installed yet; rerun Patch before using more than 5 accounts.");
                }
                if (!patchStatus.AddAccountDeepLinkPatched)
                {
                    Console.WriteLine("WARN: The add-account deep-link hook is not installed yet; rerun Patch before using the Add Account button.");
                }

                if (!patchStatus.ExactInstanceLaunchPatched)
                {
                    Console.WriteLine("WARN: Exact account launching is not installed yet; rerun Patch before launching an instance.");
                }
                if (!patchStatus.HookDllCompatible)
                {
                    Console.WriteLine("WARN: The installed hook DLL is stale or incompatible; rerun Patch to copy the current hook DLL.");
                }

                PrintPresentationWarnings(expectedDisplaySize, logicalDisplay, hostViewport, patchStatus);
                return 0;
            }

            Console.WriteLine("FAIL: the latest logs do not show the patched " + targetResolution.Name + " portrait launch yet.");

            if (targetResolution.IsUhd && !patchStatus.PhenotypeOverridePresent)
            {
                Console.WriteLine("Hint: if the target size is missing from the latest logs, rerun Patch with phenotype fallback enabled and try again.");
            }
            else
            {
                Console.WriteLine("Hint: launch Whiteout Survival once after patching, then rerun verify.");
            }

            return 2;
        }

        private static int AddAccount(PlayGamesInstallLayout layout)
        {
            var patchStatus = PatchStatusInspector.Inspect(layout);
            if (!patchStatus.AddAccountDeepLinkPatched)
            {
                throw new FriendlyException(
                    "The add-account deep-link hook is not installed. Run Patch first, then try Add Account again.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = GpgConstants.AddAccountProtocolUrl,
                UseShellExecute = true,
            });

            Console.WriteLine("Add account");
            Console.WriteLine("  launched: " + GpgConstants.AddAccountProtocolUrl);
            Console.WriteLine("  expected result: Google Play Games should open the normal new-account sign-in flow.");
            return 0;
        }

        private static int LaunchWhiteout(string[] args)
        {
            var profileSlot = ParseProfileSlot(args);
            var launchUrl = AccountPickerController.OpenPicker(profileSlot);

            Console.WriteLine("Open Whiteout Survival account picker");
            Console.WriteLine("  account slot: " + profileSlot);
            Console.WriteLine("  launched: " + launchUrl);
            Console.WriteLine("  expected result: Google Play Games should show the Whiteout Survival account picker.");
            return 0;
        }

        private static int ListAccounts()
        {
            var accounts = new PlayGamesAccountRepository().ReadAccounts();

            Console.WriteLine("Google Play Games instances");
            if (accounts.Count == 0)
            {
                Console.WriteLine("  none found");
                return 2;
            }

            for (var index = 0; index < accounts.Count; index++)
            {
                var account = accounts[index];
                PlayGamesInstanceLauncher.BuildLaunchUrl(account);
                Console.WriteLine(
                    "  " + (index + 1) + ". " + account.GamerTag
                    + " | " + account.MaskedEmail
                    + " | " + (account.IsForeground ? "active" : "idle")
                    + " | instance " + account.InstanceId);
            }

            return 0;
        }

        private static int PickAccount(PlayGamesInstallLayout layout, string[] args)
        {
            if (args.Length < 2)
            {
                throw new FriendlyException("Account name is required. Example: pick-account WOFxFarmx009");
            }

            var accountName = string.Join(" ", args.Skip(1).ToArray()).Trim();
            var accounts = new PlayGamesAccountRepository().ReadAccounts();
            var account = PlayGamesInstanceLauncher.ResolveByGamerTag(accounts, accountName);
            EnsureExactLaunchInstalled(layout);
            return LaunchResolvedAccount(account);
        }

        private static int PickAccountByIndex(PlayGamesInstallLayout layout, string[] args)
        {
            if (args.Length < 2)
            {
                throw new FriendlyException("Account row number is required. Example: pick-index 3");
            }

            int accountIndex;
            if (!int.TryParse(args[1], out accountIndex) || accountIndex < 1)
            {
                throw new FriendlyException("Account row number must be 1 or higher.");
            }

            var accounts = new PlayGamesAccountRepository().ReadAccounts();
            var account = PlayGamesInstanceLauncher.ResolveByIndex(accounts, accountIndex);
            EnsureExactLaunchInstalled(layout);
            return LaunchResolvedAccount(account);
        }

        private static int LaunchInstance(PlayGamesInstallLayout layout, string[] args)
        {
            if (args.Length != 2)
            {
                throw new FriendlyException("Instance ID is required. Example: launch-instance 01234567-89ab-cdef-0123-456789abcdef");
            }

            var accounts = new PlayGamesAccountRepository().ReadAccounts();
            var account = PlayGamesInstanceLauncher.ResolveByInstanceId(accounts, args[1]);
            EnsureExactLaunchInstalled(layout);
            return LaunchResolvedAccount(account);
        }

        private static int LaunchResolvedAccount(PlayGamesAccount account)
        {
            var launchUrl = PlayGamesInstanceLauncher.Launch(account);
            Console.WriteLine("Launch Whiteout Survival instance");
            Console.WriteLine("  gamer tag: " + account.GamerTag);
            Console.WriteLine("  email: " + account.MaskedEmail);
            Console.WriteLine("  instance: " + account.InstanceId);
            Console.WriteLine("  launched: " + launchUrl);
            Console.WriteLine("  expected result: Google Play Games should launch Whiteout Survival for this exact instance.");
            return 0;
        }

        private static void EnsureExactLaunchInstalled(PlayGamesInstallLayout layout)
        {
            var status = PatchStatusInspector.Inspect(layout);
            if (!status.ExactInstanceLaunchPatched)
            {
                throw new FriendlyException(
                    "Exact account launching is not installed. Run Patch with this GPG Patcher build, then try again.");
            }
        }

        private static int ParseProfileSlot(string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                return 1;
            }

            var value = args[1].Trim();
            if ((value.StartsWith("a", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("p", StringComparison.OrdinalIgnoreCase))
                && value.Length > 1)
            {
                value = value.Substring(1);
            }

            int profileSlot;
            if (!int.TryParse(value, out profileSlot) || profileSlot < 1 || profileSlot > 50)
            {
                throw new FriendlyException("Account slot must be a number between 1 and 50.");
            }

            return profileSlot;
        }

        private static int DiagnoseViewport(PlayGamesInstallLayout layout)
        {
            var patchStatus = PatchStatusInspector.Inspect(layout);
            var hostViewport = LogParser.TryGetLatestHostViewport(GetGpuSyslogPath(layout));
            var diagnostic = ViewportDiagnostics.Capture(hostViewport, patchStatus.TargetResolution);

            Console.WriteLine("Viewport diagnose");
            Console.WriteLine("  target backing surface: " + patchStatus.TargetResolution.Value);
            Console.WriteLine("  virtual viewport requested: " + FormatVirtualViewportRequest(patchStatus));
            Console.WriteLine("  latest log host viewport: " + FormatHostViewport(hostViewport));

            if (!diagnostic.Found)
            {
                Console.WriteLine("  live crosvm window: not found");
                Console.WriteLine();
                Console.WriteLine("FAIL: launch Whiteout Survival before running viewport-diagnose.");
                return 2;
            }

            Console.WriteLine("  crosvm pid: " + diagnostic.ProcessId);
            Console.WriteLine("  window title: " + (string.IsNullOrWhiteSpace(diagnostic.WindowTitle) ? "(unknown)" : diagnostic.WindowTitle));
            Console.WriteLine("  parent hwnd: 0x" + diagnostic.ParentWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  child hwnd: 0x" + diagnostic.ChildWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  parent window rect: " + FormatBounds(diagnostic.ParentWindowRect));
            Console.WriteLine("  child window rect: " + FormatBounds(diagnostic.ChildWindowRect));
            Console.WriteLine("  child client size: " + FormatDisplaySize(diagnostic.ChildClientSize));
            Console.WriteLine("  DWM frame bounds: " + FormatBounds(diagnostic.DwmFrameBounds));
            Console.WriteLine("  window DPI: " + (diagnostic.WindowDpi.HasValue ? diagnostic.WindowDpi.Value.ToString() : "(unknown)"));
            Console.WriteLine("  monitor bounds: " + FormatBounds(diagnostic.MonitorBounds));
            Console.WriteLine("  presentation: " + diagnostic.Presentation);
            Console.WriteLine();
            Console.WriteLine("PASS: viewport diagnostics captured.");
            return 0;
        }

        private static int ForceViewport(PlayGamesInstallLayout layout, bool allowOffscreen)
        {
            var targetResolution = ResolutionProfileStorage.ReadApplied(layout);
            var result = ViewportForcer.ForceTargetViewport(
                TimeSpan.FromSeconds(15),
                TimeSpan.FromMilliseconds(750),
                allowOffscreen,
                targetResolution);

            Console.WriteLine("Force viewport");
            Console.WriteLine("  expected viewport: " + targetResolution.Value);
            Console.WriteLine("  allow offscreen crop: " + allowOffscreen);
            Console.WriteLine("  crosvm pid: " + result.ProcessId);
            Console.WriteLine("  window title: " + (string.IsNullOrWhiteSpace(result.WindowTitle) ? "(unknown)" : result.WindowTitle));
            Console.WriteLine("  parent hwnd: 0x" + result.ParentWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  child hwnd: 0x" + result.ChildWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  attempts: " + result.Attempts);
            Console.WriteLine("  final child viewport: " + (result.FinalChildSize == null ? "(unknown)" : result.FinalChildSize.ToString()));
            Console.WriteLine();

            if (result.Success)
            {
                Console.WriteLine("PASS: crosvm child viewport is now " + targetResolution.Value + ".");
                return 0;
            }

            Console.WriteLine("FAIL: crosvm did not keep the forced viewport size.");
            return 2;
        }

        private static int InjectViewportShim(bool allowExperimentalShim)
        {
            if (!allowExperimentalShim)
            {
                throw new FriendlyException(
                    "The native viewport shim is disabled because it makes crosvm report 2160x3840 but presents a cropped 1:1 view. Use viewport-shim --experimental only for debugging.");
            }

            var result = ViewportShimInjector.Inject(AppDomain.CurrentDomain.BaseDirectory);

            Console.WriteLine("Viewport shim");
            Console.WriteLine("  shim path: " + result.ShimPath);
            Console.WriteLine("  crosvm pid: " + result.ProcessId);
            Console.WriteLine("  window title: " + (string.IsNullOrWhiteSpace(result.WindowTitle) ? "(unknown)" : result.WindowTitle));
            Console.WriteLine("  parent hwnd: 0x" + result.ParentWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  child hwnd: 0x" + result.ChildWindowHandle.ToInt64().ToString("X"));
            Console.WriteLine("  remote load result: 0x" + result.RemoteModuleHandle.ToInt64().ToString("X"));
            Console.WriteLine("  visible child size: " + (result.VisibleChildSize == null ? "(unknown)" : result.VisibleChildSize.ToString()));
            Console.WriteLine();
            Console.WriteLine("PASS: native viewport shim was injected. Run verify after a few seconds to see whether crosvm accepted the fake 4K viewport.");
            return 0;
        }

        private static void Restore(PlayGamesInstallLayout layout)
        {
            EnsureAdministrator();
            layout.EnsureInstallationExists();
            EnsureBackupExists(layout);

            PlayGamesServiceManager.Stop(layout);
            var startedAgain = false;
            try
            {
                File.Copy(layout.ExistingBackupServiceLibPath, layout.ServiceLibPath, true);
                File.Copy(layout.ExistingBackupServiceConfigPath, layout.ServiceConfigPath, true);
                DeleteIfExists(layout.HookTargetPath);
                DeleteIfExists(layout.LegacyHookTargetPath);
                ResolutionProfileStorage.DeleteApplied(layout);

                PlayGamesServiceManager.Start(layout);
                startedAgain = true;

                Console.WriteLine("Restore complete");
                Console.WriteLine("  restored: " + layout.ServiceLibPath);
                Console.WriteLine("  restored: " + layout.ServiceConfigPath);
            }
            finally
            {
                if (!startedAgain)
                {
                    try
                    {
                        PlayGamesServiceManager.Start(layout);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void BackupOriginalFiles(PlayGamesInstallLayout layout)
        {
            Directory.CreateDirectory(layout.BackupDirectory);

            if (!File.Exists(layout.BackupServiceLibPath))
            {
                var sourcePath = layout.HasLegacyBackup
                    ? layout.LegacyBackupServiceLibPath
                    : layout.ServiceLibPath;
                File.Copy(sourcePath, layout.BackupServiceLibPath, false);
            }

            if (!File.Exists(layout.BackupServiceConfigPath))
            {
                var sourcePath = layout.HasLegacyBackup
                    ? layout.LegacyBackupServiceConfigPath
                    : layout.ServiceConfigPath;
                File.Copy(sourcePath, layout.BackupServiceConfigPath, false);
            }
        }

        private static void EnsureCompatibleForPatch(PatchStatus patchStatus)
        {
            if (patchStatus.IsCompatible)
            {
                return;
            }

            throw new FriendlyException(
                "This Google Play Games build is not structurally compatible with the current patcher: "
                + patchStatus.CompatibilityMessage);
        }

        private static void EnsureBackupExists(PlayGamesInstallLayout layout)
        {
            if (!layout.HasCurrentBackup && !layout.HasLegacyBackup)
            {
                throw new FriendlyException(
                    "Backup files were not found in '" + layout.BackupDirectory + "' or '" + layout.LegacyBackupDirectory + "'.");
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void EnsureAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new FriendlyException("Patch and restore require an elevated administrator shell.");
                }
            }
        }

        private static void WriteHeadlessErrorLog(Exception ex)
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    GpgConstants.AppDataDirectoryName);
                Directory.CreateDirectory(directory);

                var logPath = Path.Combine(directory, "headless-error.log");
                File.WriteAllText(
                    logPath,
                    DateTimeOffset.Now.ToString("u")
                    + Environment.NewLine
                    + ex
                    + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static bool IsHelp(string argument)
        {
            return string.Equals(argument, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "/?", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatTimestamp(DateTimeOffset? timestamp)
        {
            return timestamp.HasValue ? timestamp.Value.ToString("u") : "(unknown)";
        }

        private static string GetGpuSyslogPath(PlayGamesInstallLayout layout)
        {
            return Path.Combine(layout.LocalLogsDirectory, "emulator_logs", "gpu_syslog.log");
        }

        private static string FormatVirtualViewportRequest(PatchStatus patchStatus)
        {
            return patchStatus != null
                && patchStatus.VirtualGuestDisplayPatched
                && patchStatus.ShowWindowRequestPatched
                    ? patchStatus.TargetResolution.Value
                    : "(not installed)";
        }

        private static string FormatDisplaySize(DisplaySizeSnapshot size)
        {
            return size == null ? "(unknown)" : size.ToString();
        }

        private static string FormatBounds(WindowBoundsSnapshot bounds)
        {
            return bounds == null ? "(unknown)" : bounds.ToString();
        }

        private static string FormatLogicalDisplay(AndroidLogicalDisplayEntry display)
        {
            if (display == null)
            {
                return "(not found)";
            }

            return "displayId " + display.DisplayId
                + ", real " + display.RealDisplaySize
                + ", app " + display.AppDisplaySize
                + ", density " + (display.Density.HasValue ? display.Density.Value.ToString() : "(unknown)");
        }

        private static string FormatHostViewport(HostViewportLogEntry hostViewport)
        {
            if (hostViewport == null)
            {
                return "(not found)";
            }

            return "scanout " + hostViewport.ScanoutId + ", " + hostViewport.ViewportSize;
        }

        private static void PrintPresentationWarnings(
            DisplaySizeSnapshot expectedDisplaySize,
            AndroidLogicalDisplayEntry logicalDisplay,
            HostViewportLogEntry hostViewport,
            PatchStatus patchStatus)
        {
            var virtualViewportRequested = patchStatus != null
                && patchStatus.VirtualGuestDisplayPatched
                && patchStatus.ShowWindowRequestPatched;

            if (logicalDisplay != null
                && logicalDisplay.AppDisplaySize != null
                && (logicalDisplay.AppDisplaySize.Width < expectedDisplaySize.Width
                    || logicalDisplay.AppDisplaySize.Height < expectedDisplaySize.Height))
            {
                Console.WriteLine(
                    "WARN: Android's physical/app display bounds are smaller than the launch size; Google Play Games may still be compositor-scaling the final image.");
            }

            if (hostViewport != null
                && hostViewport.ViewportSize != null
                && (hostViewport.ViewportSize.Width < expectedDisplaySize.Width
                    || hostViewport.ViewportSize.Height < expectedDisplaySize.Height))
            {
                Console.WriteLine(virtualViewportRequested
                    ? "INFO: The visible host viewport is smaller than the requested virtual viewport; this is expected for a normal-sized window, but final on-screen pixels are still limited by the visible window."
                    : "WARN: The visible host viewport is smaller than the Android display size; the final image is being downscaled to the current Play Games window.");
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("GPG Patcher maintenance command host");
            Console.WriteLine();
            Console.WriteLine("Commands");
            Console.WriteLine("  inspect");
            Console.WriteLine("    Report Google Play Games version, patch status, and the latest logged Whiteout Survival launch settings.");
            Console.WriteLine("  patch [--resolution <" + ResolutionProfiles.SupportedValues + ">] [--phenotype-fallback]");
            Console.WriteLine("    Back up files, apply the selected host-side resolution patch, optionally force the UHD phenotype override, and restart the Play Games service.");
            Console.WriteLine("  verify");
            Console.WriteLine("    Read the latest logs and confirm whether Whiteout Survival launched at the installed target resolution.");
            Console.WriteLine("  launch [account-slot]");
            Console.WriteLine("    Open the Whiteout Survival account picker through Google Play Games.");
            Console.WriteLine("  accounts");
            Console.WriteLine("    List the current Google Play Games instances with masked email addresses.");
            Console.WriteLine("  launch-instance <instance-id>");
            Console.WriteLine("    Launch Whiteout Survival for one exact current instance.");
            Console.WriteLine("  pick-account <name>");
            Console.WriteLine("    Resolve one unique current gamer tag and launch its exact instance.");
            Console.WriteLine("  pick-index <row>");
            Console.WriteLine("    Resolve the current dynamic account order and launch that exact instance.");
            Console.WriteLine("  add-account");
            Console.WriteLine("    Open the patched Google Play Games add-account sign-in flow.");
            Console.WriteLine("  viewport-diagnose");
            Console.WriteLine("    Inspect the live crosvm window, DWM bounds, DPI, monitor, and latest host viewport logs.");
            Console.WriteLine("  force-viewport");
            Console.WriteLine("    Resize the visible Whiteout Survival crosvm surface to the installed target resolution.");
            Console.WriteLine("    Add --allow-offscreen to force it even when the monitor is too small and the window will be cropped.");
            Console.WriteLine("  viewport-shim --experimental");
            Console.WriteLine("    Debug only: inject the native viewport shim. This can crop the game view and is disabled without --experimental.");
            Console.WriteLine("  restore");
            Console.WriteLine("    Restore original files from backup and restart the Play Games service.");
        }
    }
}

extern alias hooks;

using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Google.Hpe.Service.Util;
using Google.Hpe.Service.V1;
using GpgPatcher;
using DisplaySettingsHooks = hooks::GpgPatcher.Hooks.DisplaySettingsHooks;

namespace GpgPatcher.Tests
{
    internal static class Program
    {
        private static int assertions;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && string.Equals(args[0], "--hook-profile", StringComparison.Ordinal))
                {
                    RunHookProfile(ResolutionProfiles.Parse(args[1]));
                    Console.WriteLine("hook profile passed: " + args[1]);
                    return 0;
                }

                if (args.Length == 2 && string.Equals(args[0], "--hook-default", StringComparison.Ordinal))
                {
                    RunHookDefault(args[1]);
                    Console.WriteLine("hook default passed: " + args[1]);
                    return 0;
                }

                RunCatalogAndParserTests();
                RunStorageAndConfigurationTests();
                RunAccountTests();
                RunInstalledAccountReadProcess();
                foreach (var profile in ResolutionProfiles.All)
                {
                    RunHookProfileProcess(profile);
                }
                RunHookDefaultProcess("missing");
                RunHookDefaultProcess("invalid");

                RunTempCopyPatchTest();
                Console.WriteLine("PASS: " + assertions + " assertions completed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static void RunCatalogAndParserTests()
        {
            Assert(ResolutionProfiles.All.Count == 3, "catalog contains three profiles");
            Assert(ResolutionProfiles.Default.Value == "2160x3840", "UHD is the default");

            foreach (var profile in ResolutionProfiles.All)
            {
                ResolutionProfile parsed;
                Assert(ResolutionProfiles.TryParse(profile.Value, out parsed), "supported profile parses: " + profile.Value);
                Assert(ReferenceEquals(profile, parsed), "catalog returns canonical profile: " + profile.Value);
                Assert(profile.Width * 16 == profile.Height * 9, "profile is portrait 9:16: " + profile.Value);

                var options = PatchCommandOptions.Parse(new[] { "--resolution", profile.Value });
                Assert(ReferenceEquals(options.Resolution, profile), "patch parser selects " + profile.Value);
                Assert(!options.EnablePhenotypeFallback, "phenotype defaults off for " + profile.Value);
                Assert(
                    PatchCommandOptions.BuildArguments(profile, false) == "patch --resolution " + profile.Value,
                    "GUI arguments include " + profile.Value);
            }

            var defaults = PatchCommandOptions.Parse(Array.Empty<string>());
            Assert(ReferenceEquals(defaults.Resolution, ResolutionProfiles.Uhd), "omitted resolution defaults to UHD");

            var phenotype = PatchCommandOptions.Parse(new[]
            {
                "--resolution",
                ResolutionProfiles.Uhd.Value,
                "--phenotype-fallback",
            });
            Assert(phenotype.EnablePhenotypeFallback, "phenotype is accepted for UHD");
            Assert(
                PatchCommandOptions.BuildArguments(ResolutionProfiles.Uhd, true)
                    == "patch --resolution 2160x3840 --phenotype-fallback",
                "GUI emits UHD phenotype arguments");

            ExpectRejected(new[] { "--resolution" }, "missing resolution value");
            ExpectRejected(new[] { "--resolution", "1920x1080" }, "unsupported landscape value");
            ExpectRejected(new[] { "--resolution", "1080X1920" }, "malformed uppercase separator");
            ExpectRejected(new[] { "--resolution=1080x1920" }, "malformed equals syntax");
            ExpectRejected(
                new[] { "--resolution", "1080x1920", "--resolution", "1440x2560" },
                "duplicated resolution option");
            ExpectRejected(
                new[] { "--resolution", "1080x1920", "--phenotype-fallback" },
                "FHD phenotype combination");
            ExpectRejected(
                new[] { "--resolution", "1440x2560", "--phenotype-fallback" },
                "QHD phenotype combination");
            ExpectRejected(new[] { "--unknown" }, "unknown patch option");
        }

        private static void RunStorageAndConfigurationTests()
        {
            var root = CreateTempDirectory();
            try
            {
                var preferencePath = Path.Combine(root, ResolutionProfiles.SelectedFileName);
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadSelected(preferencePath), ResolutionProfiles.Uhd), "missing preference defaults to UHD");
                File.WriteAllText(preferencePath, "invalid\n");
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadSelected(preferencePath), ResolutionProfiles.Uhd), "invalid preference defaults to UHD");
                ResolutionProfileStorage.WriteSelected(preferencePath, ResolutionProfiles.Qhd);
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadSelected(preferencePath), ResolutionProfiles.Qhd), "pending preference persists");

                var serviceDirectory = Path.Combine(root, "service");
                Directory.CreateDirectory(serviceDirectory);
                var layout = new PlayGamesInstallLayout(root, serviceDirectory, root, root, root, root);
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadApplied(layout), ResolutionProfiles.Uhd), "missing installed config defaults to UHD");
                ResolutionProfileStorage.WriteApplied(layout, ResolutionProfiles.Fhd);
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadApplied(layout), ResolutionProfiles.Fhd), "FHD applied config persists");
                ResolutionProfileStorage.WriteApplied(layout, ResolutionProfiles.Qhd);
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadApplied(layout), ResolutionProfiles.Qhd), "applied profile switches to QHD");
                File.WriteAllText(layout.InstalledResolutionPath, "not-supported\n");
                Assert(ReferenceEquals(ResolutionProfileStorage.ReadApplied(layout), ResolutionProfiles.Uhd), "invalid installed config safely defaults to UHD");
                ResolutionProfileStorage.DeleteApplied(layout);
                Assert(!File.Exists(layout.InstalledResolutionPath), "Restore cleanup removes installed resolution config");

                var configPath = Path.Combine(serviceDirectory, "Service.exe.config");
                File.WriteAllText(
                    configPath,
                    "<configuration><setting name=\"PhenotypeFlagOverrideJson\"><value /></setting></configuration>");
                PhenotypeFallbackConfiguration.Apply(configPath);
                Assert(ReadPhenotypeValue(configPath).Contains("Enable4KUhdResolution"), "UHD phenotype override is applied");
                PhenotypeFallbackConfiguration.Clear(configPath);
                Assert(string.IsNullOrEmpty(ReadPhenotypeValue(configPath)), "non-UHD profile clears phenotype override");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void RunAccountTests()
        {
            var root = CreateTempDirectory();
            try
            {
                var avatarDirectory = Path.Combine(root, "avatars");
                Directory.CreateDirectory(avatarDirectory);
                var firstId = "11111111-1111-1111-1111-111111111111";
                var secondId = "22222222-2222-2222-2222-222222222222";
                var avatarPath = Path.Combine(avatarDirectory, firstId + ".png");
                File.WriteAllBytes(avatarPath, new byte[] { 1, 2, 3 });
                var mapped = PlayGamesAccountMapper.Map(
                    new[]
                    {
                        new PersistedPlayGamesAccount
                        {
                            InstanceId = firstId,
                            AndroidAppLibraryId = "library/one",
                            GamerTag = "Alpha Player",
                            Email = "alpha.person@example.com",
                        },
                        new PersistedPlayGamesAccount
                        {
                            InstanceId = secondId,
                            AndroidAppLibraryId = "library two",
                            GamerTag = " ",
                            Email = "invalid-email",
                        },
                    },
                    firstId,
                    avatarDirectory);

                Assert(mapped.Count == 2, "account mapper preserves dynamic order");
                Assert(mapped[0].GamerTag == "Alpha Player", "account mapper keeps gamer tag");
                Assert(mapped[0].MaskedEmail == "al***n@example.com", "account mapper masks email local part");
                Assert(mapped[0].IsForeground && !mapped[1].IsForeground, "foreground instance is detected");
                Assert(mapped[0].AvatarPath == avatarPath, "cached avatar is resolved by instance ID");
                Assert(mapped[0].Initials == "AP", "multi-word gamer tag initials are stable");
                Assert(mapped[1].GamerTag == "Google Play Games account 2", "blank gamer tag gets friendly fallback");
                Assert(mapped[1].MaskedEmail == "Email unavailable", "malformed email is never exposed");
                Assert(PlayGamesAccountMapper.Map(null, null, null).Count == 0, "empty persisted state maps to empty account list");

                var minimumGrid = AccountGridLayout.Calculate(800, 1f);
                Assert(minimumGrid.Columns == 2 && minimumGrid.CardWidth >= 280, "minimum window uses two unclipped account columns");
                var wideGrid = AccountGridLayout.Calculate(1000, 1f);
                Assert(wideGrid.Columns == 3 && wideGrid.CardWidth >= 280, "wide window uses three account columns");
                var scaledMinimumGrid = AccountGridLayout.Calculate(1200, 1.5f);
                Assert(scaledMinimumGrid.Columns == 2 && scaledMinimumGrid.CardWidth >= 420, "150 percent DPI minimum keeps two unclipped columns");
                var scaledWideGrid = AccountGridLayout.Calculate(1500, 1.5f);
                Assert(scaledWideGrid.Columns == 3 && scaledWideGrid.CardWidth >= 420, "150 percent DPI wide layout keeps three columns");

                var exact = PlayGamesInstanceLauncher.ResolveByInstanceId(mapped, firstId);
                Assert(ReferenceEquals(exact, mapped[0]), "instance ID resolves exact current account");
                Assert(ReferenceEquals(PlayGamesInstanceLauncher.ResolveByIndex(mapped, 2), mapped[1]), "dynamic index preserves current order");
                Assert(
                    PlayGamesInstanceLauncher.BuildLaunchUrl(mapped[1])
                        == "googleplaygames://launch/?id=com.gof.global&lid=1&pid=0&aid=library%20two",
                    "exact launch URL escapes Android library ID");

                ExpectFriendly(() => PlayGamesInstanceLauncher.ResolveByInstanceId(mapped, "not-a-guid"), "malformed instance ID");
                ExpectFriendly(
                    () => PlayGamesInstanceLauncher.ResolveByInstanceId(mapped, "33333333-3333-3333-3333-333333333333"),
                    "stale instance ID");
                ExpectFriendly(() => PlayGamesInstanceLauncher.ResolveByIndex(mapped, 0), "invalid zero account index");
                ExpectFriendly(() => PlayGamesInstanceLauncher.ResolveByIndex(mapped, 3), "out-of-range account index");

                var duplicates = new List<PlayGamesAccount>
                {
                    mapped[0],
                    new PlayGamesAccount
                    {
                        InstanceId = "44444444-4444-4444-4444-444444444444",
                        AndroidAppLibraryId = "other",
                        GamerTag = "Alpha Player",
                        MaskedEmail = "a***@example.com",
                    },
                };
                ExpectFriendly(
                    () => PlayGamesInstanceLauncher.ResolveByGamerTag(duplicates, "alpha player"),
                    "ambiguous duplicate gamer tags");
                ExpectFriendly(
                    () => PlayGamesInstanceLauncher.ResolveByGamerTag(mapped, "missing"),
                    "unknown gamer tag");

                var missingLibrary = new PlayGamesAccount
                {
                    InstanceId = "55555555-5555-5555-5555-555555555555",
                    GamerTag = "Preparing",
                    MaskedEmail = "p***@example.com",
                };
                ExpectFriendly(() => PlayGamesInstanceLauncher.BuildLaunchUrl(missingLibrary), "missing library ID");

                var missingLocal = Path.Combine(root, "missing-local");
                var missingService = Path.Combine(root, "missing-service");
                Directory.CreateDirectory(missingLocal);
                Directory.CreateDirectory(missingService);
                ExpectFriendly(
                    () => new PlayGamesAccountRepository(missingLocal, missingService).ReadAccounts(),
                    "missing Play Games account store");
                File.WriteAllBytes(Path.Combine(missingLocal, "store.db"), new byte[] { 1 });
                ExpectFriendly(
                    () => new PlayGamesAccountRepository(missingLocal, missingService).ReadAccounts(),
                    "missing instances encryption key");
                File.WriteAllBytes(Path.Combine(missingLocal, "instances_encryption_key"), new byte[] { 1 });
                ExpectFriendly(
                    () => new PlayGamesAccountRepository(missingLocal, missingService).ReadAccounts(),
                    "missing Play Games service assemblies");
                ExpectFriendly(
                    () => WindowsSqlite.ReadSingleBlob(Path.Combine(missingLocal, "store.db"), "SELECT value FROM missing"),
                    "corrupt account database");

            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void RunInstalledAccountReadProcess()
        {
            var testDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var configuration = new DirectoryInfo(testDirectory).Name;
            var executable = Path.GetFullPath(Path.Combine(
                testDirectory,
                "..",
                "..",
                "app",
                configuration,
                "GPG Patcher.exe"));
            Assert(File.Exists(executable), "GUI command host exists for installed account integration test");
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--headless accounts",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert(process.ExitCode == 0, "installed dynamic accounts read succeeds: " + stderr);
                var accountLines = stdout.Replace("\r\n", "\n")
                    .Split('\n')
                    .Where(line => line.Contains(" | instance "))
                    .ToList();
                Assert(accountLines.Count > 0, "installed dynamic account list is non-empty");
                Assert(accountLines.All(line => line.Contains("***") || line.Contains("Email unavailable")), "installed account command masks every email");
            }
        }

        private static void RunHookProfileProcess(ResolutionProfile profile)
        {
            var executable = Assembly.GetExecutingAssembly().Location;
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--hook-profile " + profile.Value,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert(process.ExitCode == 0, "hook child passed for " + profile.Value + ": " + stdout + stderr);
            }
        }

        private static void RunHookDefaultProcess(string mode)
        {
            var executable = Assembly.GetExecutingAssembly().Location;
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--hook-default " + mode,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }))
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert(process.ExitCode == 0, "hook " + mode + " config defaults safely: " + stdout + stderr);
            }
        }

        private static void RunHookDefault(string mode)
        {
            var resolutionPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                ResolutionProfiles.InstalledFileName);
            if (string.Equals(mode, "missing", StringComparison.Ordinal))
            {
                if (File.Exists(resolutionPath))
                {
                    File.Delete(resolutionPath);
                }
            }
            else if (string.Equals(mode, "invalid", StringComparison.Ordinal))
            {
                File.WriteAllText(resolutionPath, "unsupported\n");
            }
            else
            {
                throw new InvalidOperationException("Unknown hook default mode: " + mode);
            }

            try
            {
                Assert(DisplaySettingsHooks.TargetWidth == ResolutionProfiles.Uhd.Width, mode + " config defaults hook width to UHD");
                Assert(DisplaySettingsHooks.TargetHeight == ResolutionProfiles.Uhd.Height, mode + " config defaults hook height to UHD");
            }
            finally
            {
                if (File.Exists(resolutionPath))
                {
                    File.Delete(resolutionPath);
                }
            }
        }

        private static void RunHookProfile(ResolutionProfile profile)
        {
            var resolutionPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                ResolutionProfiles.InstalledFileName);
            File.WriteAllText(resolutionPath, profile.Value + Environment.NewLine);
            try
            {
                Assert(
                    DisplaySettingsHooks.TargetWidth == profile.Width,
                    "hook reads target width (expected " + profile.Width
                    + ", actual " + DisplaySettingsHooks.TargetWidth
                    + ", assembly " + typeof(DisplaySettingsHooks).Assembly.Location
                    + ", config " + resolutionPath + ")");
                Assert(DisplaySettingsHooks.TargetHeight == profile.Height, "hook reads target height");

                var launchRequest = new LaunchGameRequest
                {
                    PackageName = DisplaySettingsHooks.TargetPackageName,
                    ScreenOrientation = App.Types.ScreenOrientation.Portrait,
                };
                var settings = CreateBaseSettings();
                DisplaySettingsHooks.PatchAndroidDisplaySettings(settings, launchRequest);
                AssertSize(settings.DisplaySize, profile, "launch size");
                Assert(ContainsSize(settings.AvailableDisplaySizes, profile), "launch available sizes contains target");
                Assert(settings.DisplayDensity == ExpectedDensity(profile), "launch density scales by target height");

                var runtimeSettings = CreateBaseSettings();
                DisplaySettingsHooks.PatchRuntimeAndroidDisplaySettings(runtimeSettings, launchRequest);
                AssertSize(runtimeSettings.DisplaySize, profile, "runtime size");
                Assert(runtimeSettings.DisplayDensity == ExpectedDensity(profile), "runtime density scales by target height");

                var available = new AndroidDisplay.AvailableSettings();
                available = DisplaySettingsHooks.PatchAvailableSettings(available, launchRequest);
                Assert(
                    ContainsSize(available.DisplaySizeList, profile),
                    "available-settings contains target (actual " + FormatSizes(available.DisplaySizeList)
                    + ", package " + launchRequest.PackageName
                    + ", orientation " + launchRequest.ScreenOrientation + ")");

                var monitor = DisplaySettingsHooks.PatchMonitorDisplaySize(new DisplaySize(), launchRequest);
                AssertSize(monitor, profile, "monitor size");

                var guest = new AddGuestDisplayRequest();
                DisplaySettingsHooks.PatchAddGuestDisplayRequest(
                    guest,
                    new LaunchGamePcsRequest { PackageName = DisplaySettingsHooks.TargetPackageName });
                AssertSize(guest.DisplaySettings.DisplaySize, profile, "guest display size");

                var showWindow = new ShowWindowRequest { PackageName = DisplaySettingsHooks.TargetPackageName };
                DisplaySettingsHooks.PatchShowWindowRequest(showWindow);
                AssertSize(showWindow.GuestDisplaySize, profile, "show-window guest size");
                Assert(
                    showWindow.DisplayRatio == KiwiVmState.Types.DisplayRatio.Portrait916,
                    "show-window requests portrait 9:16");
            }
            finally
            {
                File.Delete(resolutionPath);
            }
        }

        private static void RunTempCopyPatchTest()
        {
            var sourceServiceLib = FindPristineServiceLib();
            var sourceConfig = Path.Combine(Path.GetDirectoryName(sourceServiceLib), "Service.exe.config");
            Assert(File.Exists(sourceConfig), "temporary simulation source config exists");

            var root = CreateTempDirectory();
            try
            {
                var serviceDirectory = Path.Combine(root, "service");
                Directory.CreateDirectory(serviceDirectory);
                var tempServiceLib = Path.Combine(serviceDirectory, "ServiceLib.dll");
                var tempConfig = Path.Combine(serviceDirectory, "Service.exe.config");
                File.Copy(sourceServiceLib, tempServiceLib);
                File.Copy(sourceConfig, tempConfig);

                var firstOutput = Path.Combine(root, "patched-first.dll");
                var secondOutput = Path.Combine(root, "patched-second.dll");
                var first = ServiceLibPatcher.Patch(tempServiceLib, firstOutput);
                Assert(!first.AlreadyPatched, "first temp-copy pass changes pristine ServiceLib");
                Assert(first.ExactInstanceLaunchPatched, "first temp-copy pass installs exact-instance launch hook");
                File.Copy(firstOutput, tempServiceLib, true);
                var second = ServiceLibPatcher.Patch(tempServiceLib, secondOutput);
                Assert(second.AlreadyPatched, "second temp-copy pass is idempotent");
                Assert(!second.ExactInstanceLaunchPatched, "second temp-copy pass detects exact-instance launch hook");

                File.Copy(sourceServiceLib, tempServiceLib, true);
                var restoredOutput = Path.Combine(root, "patched-after-restore.dll");
                var afterRestore = ServiceLibPatcher.Patch(tempServiceLib, restoredOutput);
                Assert(afterRestore.ExactInstanceLaunchPatched, "restored pristine service library removes exact-instance launch hook");

                var layout = new PlayGamesInstallLayout(root, serviceDirectory, root, root, root, root);
                foreach (var profile in ResolutionProfiles.All)
                {
                    ResolutionProfileStorage.WriteApplied(layout, profile);
                    Assert(
                        ReferenceEquals(ResolutionProfileStorage.ReadApplied(layout), profile),
                        "temp-copy profile switches to " + profile.Value);
                }

                PhenotypeFallbackConfiguration.Apply(tempConfig);
                PhenotypeFallbackConfiguration.Clear(tempConfig);
                Assert(string.IsNullOrEmpty(ReadPhenotypeValue(tempConfig)), "temp-copy lower profile clears UHD phenotype");
                ResolutionProfileStorage.DeleteApplied(layout);
                Assert(!File.Exists(layout.InstalledResolutionPath), "temp-copy Restore cleanup removes profile file");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static AndroidDisplaySettings CreateBaseSettings()
        {
            var settings = new AndroidDisplaySettings
            {
                DisplaySize = new DisplaySize { Width = 1080, Height = 1920 },
                DisplayDensity = 320,
            };
            settings.AvailableDisplaySizes.Add(new DisplaySize { Width = 1080, Height = 1920 });
            return settings;
        }

        private static int ExpectedDensity(ResolutionProfile profile)
        {
            return (int)Math.Round(320d * profile.Height / 1920d, MidpointRounding.AwayFromZero);
        }

        private static bool ContainsSize(System.Collections.Generic.IList<DisplaySize> sizes, ResolutionProfile profile)
        {
            return sizes != null && sizes.Any(size => size.Width == profile.Width && size.Height == profile.Height);
        }

        private static string FormatSizes(System.Collections.Generic.IList<DisplaySize> sizes)
        {
            return sizes == null
                ? "null"
                : string.Join(",", sizes.Select(size => size.Width + "x" + size.Height));
        }

        private static void AssertSize(DisplaySize size, ResolutionProfile profile, string description)
        {
            Assert(size != null && size.Width == profile.Width && size.Height == profile.Height, description);
            Assert(size.Width * 16 == size.Height * 9, description + " is 9:16");
        }

        private static string FindPristineServiceLib()
        {
            var backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                GpgConstants.AppDataDirectoryName,
                "backup");
            if (Directory.Exists(backupRoot))
            {
                var backup = Directory.GetFiles(backupRoot, "ServiceLib.dll", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault(path => File.Exists(Path.Combine(Path.GetDirectoryName(path), "Service.exe.config")));
                if (!string.IsNullOrWhiteSpace(backup))
                {
                    return backup;
                }
            }

            throw new InvalidOperationException("A pristine GpgPatcher backup was not found for temp-copy patch verification.");
        }

        private static string ReadPhenotypeValue(string configPath)
        {
            var document = XDocument.Load(configPath);
            return document.Descendants("setting")
                .First(element => (string)element.Attribute("name") == GpgConstants.PhenotypeSettingName)
                .Element("value")
                .Value;
        }

        private static void ExpectRejected(string[] arguments, string description)
        {
            try
            {
                PatchCommandOptions.Parse(arguments);
            }
            catch (FriendlyException)
            {
                assertions++;
                return;
            }

            throw new InvalidOperationException("Expected rejection: " + description);
        }

        private static void ExpectFriendly(Action action, string description)
        {
            try
            {
                action();
            }
            catch (FriendlyException)
            {
                assertions++;
                return;
            }

            throw new InvalidOperationException("Expected friendly rejection: " + description);
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "gpg-patcher-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Assert(bool condition, string description)
        {
            assertions++;
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + description);
            }
        }
    }
}

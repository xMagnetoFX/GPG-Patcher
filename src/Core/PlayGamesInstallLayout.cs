using System;
using System.IO;

namespace GpgPatcher
{
    internal sealed class PlayGamesInstallLayout
    {
        public PlayGamesInstallLayout(
            string installRoot,
            string serviceDirectory,
            string localLogsDirectory,
            string backupDirectory,
            string legacyBackupDirectory,
            string hookSourcePath)
        {
            InstallRoot = installRoot;
            ServiceDirectory = serviceDirectory;
            LocalLogsDirectory = localLogsDirectory;
            BackupDirectory = backupDirectory;
            LegacyBackupDirectory = legacyBackupDirectory;
            HookSourcePath = hookSourcePath;

            ServiceExePath = Path.Combine(ServiceDirectory, "Service.exe");
            ServiceLibPath = Path.Combine(ServiceDirectory, "ServiceLib.dll");
            ServiceConfigPath = Path.Combine(ServiceDirectory, "Service.exe.config");
            HookTargetPath = Path.Combine(ServiceDirectory, GpgConstants.HookAssemblyFileName);
            InstalledResolutionPath = Path.Combine(ServiceDirectory, ResolutionProfiles.InstalledFileName);
            LegacyHookTargetPath = Path.Combine(ServiceDirectory, GpgConstants.LegacyHookAssemblyFileName);
            ServiceLogPath = Path.Combine(LocalLogsDirectory, GpgConstants.ServiceLogFileName);
            AndroidSerialLogPath = Path.Combine(LocalLogsDirectory, GpgConstants.AndroidSerialLogFileName);
            BackupServiceLibPath = Path.Combine(BackupDirectory, "ServiceLib.dll");
            BackupServiceConfigPath = Path.Combine(BackupDirectory, "Service.exe.config");
            LegacyBackupServiceLibPath = Path.Combine(LegacyBackupDirectory, "ServiceLib.dll");
            LegacyBackupServiceConfigPath = Path.Combine(LegacyBackupDirectory, "Service.exe.config");
        }

        public string InstallRoot { get; }

        public string ServiceDirectory { get; }

        public string LocalLogsDirectory { get; }

        public string BackupDirectory { get; }

        public string LegacyBackupDirectory { get; }

        public string HookSourcePath { get; }

        public string ServiceExePath { get; }

        public string ServiceLibPath { get; }

        public string ServiceConfigPath { get; }

        public string HookTargetPath { get; }

        public string InstalledResolutionPath { get; }

        public string LegacyHookTargetPath { get; }

        public string ServiceLogPath { get; }

        public string AndroidSerialLogPath { get; }

        public string BackupServiceLibPath { get; }

        public string BackupServiceConfigPath { get; }

        public string LegacyBackupServiceLibPath { get; }

        public string LegacyBackupServiceConfigPath { get; }

        public bool HasCurrentBackup
        {
            get { return File.Exists(BackupServiceLibPath) && File.Exists(BackupServiceConfigPath); }
        }

        public bool HasLegacyBackup
        {
            get { return File.Exists(LegacyBackupServiceLibPath) && File.Exists(LegacyBackupServiceConfigPath); }
        }

        public string ExistingBackupDirectory
        {
            get { return HasCurrentBackup ? BackupDirectory : (HasLegacyBackup ? LegacyBackupDirectory : BackupDirectory); }
        }

        public string ExistingBackupServiceLibPath
        {
            get { return HasCurrentBackup ? BackupServiceLibPath : (HasLegacyBackup ? LegacyBackupServiceLibPath : BackupServiceLibPath); }
        }

        public string ExistingBackupServiceConfigPath
        {
            get { return HasCurrentBackup ? BackupServiceConfigPath : (HasLegacyBackup ? LegacyBackupServiceConfigPath : BackupServiceConfigPath); }
        }

        public static PlayGamesInstallLayout CreateDefault()
        {
            var installRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Google",
                "Play Games");

            var serviceDirectory = Path.Combine(installRoot, "current", "service");
            var localLogsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google",
                "Play Games",
                "Logs");

            var serviceExePath = Path.Combine(serviceDirectory, "Service.exe");
            var installedVersion = File.Exists(serviceExePath)
                ? PatchStatusInspector.GetInstalledVersion(serviceExePath)
                : "unknown";

            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                GpgConstants.AppDataDirectoryName,
                "backup",
                installedVersion);

            var legacyBackupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                GpgConstants.LegacyAppDataDirectoryName,
                "backup",
                installedVersion);

            var hookSourcePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                GpgConstants.HookAssemblyFileName);

            return new PlayGamesInstallLayout(
                installRoot,
                serviceDirectory,
                localLogsDirectory,
                backupDirectory,
                legacyBackupDirectory,
                hookSourcePath);
        }

        public void EnsureInstallationExists()
        {
            EnsureFileExists(ServiceExePath, "Google Play Games service executable");
            EnsureFileExists(ServiceLibPath, "Google Play Games service library");
            EnsureFileExists(ServiceConfigPath, "Google Play Games service config");
        }

        public void EnsureHookBuildExists()
        {
            EnsureFileExists(HookSourcePath, "hook assembly");
        }

        private static void EnsureFileExists(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FriendlyException(
                    description + " was not found at '" + path + "'. Build the solution first and confirm Google Play Games is installed.");
            }
        }
    }
}

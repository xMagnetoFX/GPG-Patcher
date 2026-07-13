using System;
using System.IO;

namespace GpgPatcher
{
    public static class ResolutionProfileStorage
    {
        public static string SelectedResolutionPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    GpgConstants.AppDataDirectoryName,
                    ResolutionProfiles.SelectedFileName);
            }
        }

        public static ResolutionProfile ReadSelected()
        {
            return ReadSelected(SelectedResolutionPath);
        }

        public static void WriteSelected(ResolutionProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            WriteSelected(SelectedResolutionPath, profile);
        }

        internal static ResolutionProfile ReadSelected(string path)
        {
            return ResolutionProfiles.ReadFileOrDefault(path);
        }

        internal static void WriteSelected(string path, ResolutionProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, profile.Value + Environment.NewLine);
        }

        internal static ResolutionProfile ReadApplied(PlayGamesInstallLayout layout)
        {
            return ResolutionProfiles.ReadFileOrDefault(layout.InstalledResolutionPath);
        }

        internal static void WriteApplied(PlayGamesInstallLayout layout, ResolutionProfile profile)
        {
            File.WriteAllText(layout.InstalledResolutionPath, profile.Value + Environment.NewLine);
        }

        internal static void DeleteApplied(PlayGamesInstallLayout layout)
        {
            if (File.Exists(layout.InstalledResolutionPath))
            {
                File.Delete(layout.InstalledResolutionPath);
            }
        }
    }
}

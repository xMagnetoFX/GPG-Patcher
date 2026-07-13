using System.Diagnostics;

namespace GpgPatcher
{
    internal static class AccountPickerController
    {
        public static string OpenPicker(int profileSlot)
        {
            var launchUrl = GpgConstants.BuildLaunchProtocolUrl(profileSlot);
            Process.Start(new ProcessStartInfo
            {
                FileName = launchUrl,
                UseShellExecute = true,
            });

            return launchUrl;
        }
    }
}

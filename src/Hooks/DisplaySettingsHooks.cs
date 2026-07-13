using System;
using System.Collections.Generic;
using System.IO;
using Google.Hpe.Service.Util;
using Google.Hpe.Service.V1;

namespace GpgPatcher.Hooks
{
    public static class DisplaySettingsHooks
    {
        public const string TargetPackageName = "com.gof.global";
        public const int AccountLimitBypassVisibleCount = 4;

        private static readonly ResolutionProfile TargetProfile;

        static DisplaySettingsHooks()
        {
            TargetProfile = ResolutionProfiles.ReadFileOrDefault(
                Path.Combine(
                    Path.GetDirectoryName(typeof(DisplaySettingsHooks).Assembly.Location) ?? string.Empty,
                    ResolutionProfiles.InstalledFileName));
        }

        public static int TargetWidth
        {
            get { return TargetProfile.Width; }
        }

        public static int TargetHeight
        {
            get { return TargetProfile.Height; }
        }

        public static string TargetResolution
        {
            get { return TargetProfile.Value; }
        }

        public static AndroidDisplay.AvailableSettings PatchAvailableSettings(
            AndroidDisplay.AvailableSettings settings,
            LaunchGameRequest request)
        {
            if (!ShouldPatch(request))
            {
                return settings;
            }

            if (settings.DisplaySizeList == null)
            {
                settings.DisplaySizeList = new List<DisplaySize>();
            }

            AddIfMissing(settings.DisplaySizeList);
            return settings;
        }

        public static AndroidDisplaySettings PatchAndroidDisplaySettings(
            AndroidDisplaySettings settings,
            LaunchGameRequest request)
        {
            if (settings == null || !ShouldPatch(request))
            {
                return settings;
            }

            var originalSelected = settings.DisplaySize == null ? null : Clone(settings.DisplaySize);
            var originalMax = FindMax(settings.AvailableDisplaySizes);

            AddIfMissing(settings.AvailableDisplaySizes);

            var originalHeight = GetKnownHeight(originalSelected, originalMax);
            var originalDensity = settings.DisplayDensity;

            settings.DisplaySize = CreateTargetSize();

            if (originalDensity > 0 && originalHeight > 0)
            {
                settings.DisplayDensity = ScaleDensity(originalDensity, originalHeight, TargetHeight);
            }

            return settings;
        }

        public static AndroidDisplaySettings PatchRuntimeAndroidDisplaySettings(
            AndroidDisplaySettings settings,
            LaunchGameRequest request)
        {
            return PatchAndroidDisplaySettings(settings, request);
        }

        public static void PatchAddGuestDisplayRequest(
            AddGuestDisplayRequest request,
            LaunchGamePcsRequest launchRequest)
        {
            if (request == null || !ShouldPatch(launchRequest))
            {
                return;
            }

            if (request.DisplaySettings == null)
            {
                request.DisplaySettings = new AndroidDisplaySettings();
            }

            PatchAndroidDisplaySettings(request.DisplaySettings, true);
        }

        public static void PatchShowWindowRequest(ShowWindowRequest request)
        {
            if (request == null || !ShouldPatch(request))
            {
                return;
            }

            request.GuestDisplaySize = CreateTargetSize();
            request.DisplayRatio = KiwiVmState.Types.DisplayRatio.Portrait916;
            if (request.VmWindowMode == WindowState.Types.Mode.Fullscreen)
            {
                request.VmWindowMode = WindowState.Types.Mode.Windowed;
            }
        }

        public static DisplaySize PatchMonitorDisplaySize(
            DisplaySize displaySize,
            LaunchGameRequest request)
        {
            if (!ShouldPatch(request))
            {
                return displaySize;
            }

            return CreateTargetSize();
        }

        public static int PatchOnboardedAccountCount(int count)
        {
            return count >= 5 ? AccountLimitBypassVisibleCount : count;
        }

        private static bool ShouldPatch(LaunchGameRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (!string.Equals(request.PackageName, TargetPackageName, StringComparison.Ordinal))
            {
                return false;
            }

            switch (request.ScreenOrientation)
            {
                case App.Types.ScreenOrientation.Portrait:
                case App.Types.ScreenOrientation.SensorPortrait:
                case App.Types.ScreenOrientation.UserPortrait:
                case App.Types.ScreenOrientation.ReversePortrait:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ShouldPatch(LaunchGamePcsRequest request)
        {
            return request != null
                && string.Equals(request.PackageName, TargetPackageName, StringComparison.Ordinal);
        }

        private static bool ShouldPatch(ShowWindowRequest request)
        {
            return request != null
                && string.Equals(request.PackageName, TargetPackageName, StringComparison.Ordinal);
        }

        private static AndroidDisplaySettings PatchAndroidDisplaySettings(
            AndroidDisplaySettings settings,
            bool shouldPatch)
        {
            if (settings == null || !shouldPatch)
            {
                return settings;
            }

            var originalSelected = settings.DisplaySize == null ? null : Clone(settings.DisplaySize);
            var originalMax = FindMax(settings.AvailableDisplaySizes);

            AddIfMissing(settings.AvailableDisplaySizes);

            var originalHeight = GetKnownHeight(originalSelected, originalMax);
            var originalDensity = settings.DisplayDensity;

            settings.DisplaySize = CreateTargetSize();
            settings.SharpeningEnabled = true;

            if (originalDensity > 0 && originalHeight > 0)
            {
                settings.DisplayDensity = ScaleDensity(originalDensity, originalHeight, TargetHeight);
            }

            return settings;
        }

        private static void AddIfMissing(IList<DisplaySize> sizes)
        {
            if (sizes == null)
            {
                return;
            }

            for (var i = 0; i < sizes.Count; i++)
            {
                if (IsSameSize(sizes[i], TargetWidth, TargetHeight))
                {
                    return;
                }
            }

            sizes.Add(CreateTargetSize());
        }

        private static DisplaySize FindMax(IList<DisplaySize> sizes)
        {
            if (sizes == null || sizes.Count == 0)
            {
                return null;
            }

            DisplaySize max = null;
            for (var i = 0; i < sizes.Count; i++)
            {
                var current = sizes[i];
                if (current == null)
                {
                    continue;
                }

                if (max == null || CompareByArea(current, max) > 0)
                {
                    max = current;
                }
            }

            return max == null ? null : Clone(max);
        }

        private static int GetKnownHeight(DisplaySize selected, DisplaySize originalMax)
        {
            if (selected != null && selected.Height > 0)
            {
                return selected.Height;
            }

            if (originalMax != null && originalMax.Height > 0)
            {
                return originalMax.Height;
            }

            return 0;
        }

        private static int ScaleDensity(int originalDensity, int originalHeight, int targetHeight)
        {
            var scaled = (int)Math.Round(
                originalDensity * (double)targetHeight / originalHeight,
                MidpointRounding.AwayFromZero);

            return scaled > 0 ? scaled : originalDensity;
        }

        private static int CompareByArea(DisplaySize left, DisplaySize right)
        {
            var leftArea = (long)left.Width * left.Height;
            var rightArea = (long)right.Width * right.Height;

            if (leftArea != rightArea)
            {
                return leftArea.CompareTo(rightArea);
            }

            if (left.Height != right.Height)
            {
                return left.Height.CompareTo(right.Height);
            }

            return left.Width.CompareTo(right.Width);
        }

        private static bool IsSameSize(DisplaySize size, int width, int height)
        {
            return size != null && size.Width == width && size.Height == height;
        }

        private static DisplaySize Clone(DisplaySize size)
        {
            return new DisplaySize
            {
                Width = size.Width,
                Height = size.Height,
            };
        }

        private static DisplaySize CreateTargetSize()
        {
            return new DisplaySize
            {
                Width = TargetWidth,
                Height = TargetHeight,
            };
        }
    }
}

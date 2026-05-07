using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace GpgPatcher
{
    internal static class LogParser
    {
        private static readonly Regex DisplayDensityRegex =
            new Regex("\"displayDensity\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        private static readonly Regex DisplaySizeRegex =
            new Regex("\"displaySize\"\\s*:\\s*\\{\\s*\"width\"\\s*:\\s*(\\d+)\\s*,\\s*\"height\"\\s*:\\s*(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex AvailableSizesBlockRegex =
            new Regex("\"availableDisplaySizes\"\\s*:\\s*\\[(.*)\\]",
                RegexOptions.Compiled);

        private static readonly Regex WidthHeightRegex =
            new Regex("\"width\"\\s*:\\s*(\\d+)\\s*,\\s*\"height\"\\s*:\\s*(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex DisplayIdRegex =
            new Regex("on displayId\\s+(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex AndroidSerialFinalSizeRegex =
            new Regex("AndroidDisplayManagerIm: Display \\d+ size is (\\d+)x(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex AndroidSerialDisplaySizeSetRegex =
            new Regex("DisplayControllerImpl: Display size set to (\\d+)x(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex AndroidSerialWindowManagerSizeRegex =
            new Regex("WindowManager: Using new display size: (\\d+)x(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex AndroidSerialAddedRegex =
            new Regex("Display device added: .*? (\\d+) x (\\d+), .*?DeviceProductInfo\\{name=CrosvmDisplay",
                RegexOptions.Compiled);

        private static readonly Regex AndroidLogicalDisplayRegex =
            new Regex(
                "LogicalDisplayMapper: Adding new display:\\s*(\\d+): DisplayInfo\\{.*?real\\s+(\\d+)\\s+x\\s+(\\d+).*?app\\s+(\\d+)\\s+x\\s+(\\d+).*?density\\s+(\\d+)",
                RegexOptions.Compiled);

        private static readonly Regex HostViewportRegex =
            new Regex("\\[Scanout\\s+(\\d+)\\]\\s+Updating host viewport size to\\s+(\\d+)x(\\d+)",
                RegexOptions.Compiled);

        public static ServiceLaunchLogEntry TryGetLatestLaunch(string serviceLogPath, string packageName)
        {
            if (!File.Exists(serviceLogPath))
            {
                return null;
            }

            var marker = "KiwiEmulatorModule: launching " + packageName + " with Android display settings ";
            var lines = ReadAllLinesShared(serviceLogPath);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (!line.Contains(marker))
                {
                    continue;
                }

                var density = TryMatchInt(DisplayDensityRegex, line);
                var displaySize = TryMatchDisplaySize(DisplaySizeRegex, line);
                var availableSizes = ParseAvailableSizes(line);
                var displayId = TryMatchInt(DisplayIdRegex, line);
                return new ServiceLaunchLogEntry(
                    ParseServiceTimestamp(line),
                    density,
                    displaySize,
                    availableSizes,
                    displayId,
                    line);
            }

            return null;
        }

        public static ResolutionCapLogEntry TryGetLatestResolutionCap(string serviceLogPath)
        {
            if (!File.Exists(serviceLogPath))
            {
                return null;
            }

            var marker = "AppSessionScope: Default Android screen resolution cap:";
            var lines = ReadAllLinesShared(serviceLogPath);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                var cap = line.Substring(markerIndex + marker.Length).Trim();
                return new ResolutionCapLogEntry(ParseServiceTimestamp(line), cap, line);
            }

            return null;
        }

        public static AndroidSerialDisplayEntry TryGetLatestAndroidSerialDisplay(string androidSerialLogPath)
        {
            if (!File.Exists(androidSerialLogPath))
            {
                return null;
            }

            var lines = ReadAllLinesShared(androidSerialLogPath);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                var finalSizeMatch = AndroidSerialFinalSizeRegex.Match(line);
                if (finalSizeMatch.Success)
                {
                    return CreateAndroidSerialDisplayEntry(finalSizeMatch, line);
                }

                var displaySizeSetMatch = AndroidSerialDisplaySizeSetRegex.Match(line);
                if (displaySizeSetMatch.Success)
                {
                    return CreateAndroidSerialDisplayEntry(displaySizeSetMatch, line);
                }

                var windowManagerSizeMatch = AndroidSerialWindowManagerSizeRegex.Match(line);
                if (windowManagerSizeMatch.Success)
                {
                    return CreateAndroidSerialDisplayEntry(windowManagerSizeMatch, line);
                }

                var addedMatch = AndroidSerialAddedRegex.Match(line);
                if (addedMatch.Success)
                {
                    return CreateAndroidSerialDisplayEntry(addedMatch, line);
                }
            }

            return null;
        }

        public static AndroidLogicalDisplayEntry TryGetLatestLogicalDisplay(string androidSerialLogPath, int? displayId)
        {
            if (!File.Exists(androidSerialLogPath))
            {
                return null;
            }

            var lines = ReadAllLinesShared(androidSerialLogPath);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                var match = AndroidLogicalDisplayRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var currentDisplayId = ParseInt(match.Groups[1].Value);
                if (displayId.HasValue && currentDisplayId != displayId.Value)
                {
                    continue;
                }

                return new AndroidLogicalDisplayEntry(
                    currentDisplayId,
                    new DisplaySizeSnapshot(
                        ParseInt(match.Groups[2].Value),
                        ParseInt(match.Groups[3].Value)),
                    new DisplaySizeSnapshot(
                        ParseInt(match.Groups[4].Value),
                        ParseInt(match.Groups[5].Value)),
                    ParseInt(match.Groups[6].Value),
                    line);
            }

            return null;
        }

        public static HostViewportLogEntry TryGetLatestHostViewport(string gpuSyslogPath)
        {
            if (!File.Exists(gpuSyslogPath))
            {
                return null;
            }

            var lines = ReadAllLinesShared(gpuSyslogPath);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                var match = HostViewportRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                return new HostViewportLogEntry(
                    ParseInt(match.Groups[1].Value),
                    new DisplaySizeSnapshot(
                        ParseInt(match.Groups[2].Value),
                        ParseInt(match.Groups[3].Value)),
                    line);
            }

            return null;
        }

        public static string FormatDisplaySizes(IReadOnlyList<DisplaySizeSnapshot> sizes)
        {
            if (sizes == null || sizes.Count == 0)
            {
                return "(none)";
            }

            var formatted = new string[sizes.Count];
            for (var i = 0; i < sizes.Count; i++)
            {
                formatted[i] = sizes[i].ToString();
            }

            return string.Join(", ", formatted);
        }

        private static List<DisplaySizeSnapshot> ParseAvailableSizes(string line)
        {
            var result = new List<DisplaySizeSnapshot>();
            var blockMatch = AvailableSizesBlockRegex.Match(line);
            if (!blockMatch.Success)
            {
                return result;
            }

            var sizesBlock = blockMatch.Groups[1].Value;
            var sizeMatches = WidthHeightRegex.Matches(sizesBlock);
            foreach (Match sizeMatch in sizeMatches)
            {
                result.Add(new DisplaySizeSnapshot(
                    ParseInt(sizeMatch.Groups[1].Value),
                    ParseInt(sizeMatch.Groups[2].Value)));
            }

            return result;
        }

        private static DisplaySizeSnapshot TryMatchDisplaySize(Regex regex, string input)
        {
            var match = regex.Match(input);
            if (!match.Success)
            {
                return null;
            }

            return new DisplaySizeSnapshot(
                ParseInt(match.Groups[1].Value),
                ParseInt(match.Groups[2].Value));
        }

        private static int? TryMatchInt(Regex regex, string input)
        {
            var match = regex.Match(input);
            if (!match.Success)
            {
                return null;
            }

            return ParseInt(match.Groups[1].Value);
        }

        private static AndroidSerialDisplayEntry CreateAndroidSerialDisplayEntry(Match match, string rawLine)
        {
            return new AndroidSerialDisplayEntry(
                new DisplaySizeSnapshot(
                    ParseInt(match.Groups[1].Value),
                    ParseInt(match.Groups[2].Value)),
                rawLine);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static string[] ReadAllLinesShared(string path)
        {
            var lines = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    lines.Add(reader.ReadLine());
                }
            }

            return lines.ToArray();
        }

        private static DateTimeOffset? ParseServiceTimestamp(string line)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 25)
            {
                return null;
            }

            var candidate = line.Substring(0, 25);
            DateTimeOffset parsed;
            if (DateTimeOffset.TryParseExact(
                candidate,
                "yyMMdd HH:mm:ss.fffzzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
            {
                return parsed;
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace GpgPatcher
{
    public sealed class ResolutionProfile
    {
        internal ResolutionProfile(string name, string value, int width, int height)
        {
            Name = name;
            Value = value;
            Width = width;
            Height = height;
        }

        public string Name { get; }

        public string Value { get; }

        public int Width { get; }

        public int Height { get; }

        public bool IsUhd
        {
            get { return ReferenceEquals(this, ResolutionProfiles.Uhd); }
        }

        public string DisplayLabel
        {
            get { return Name + "  " + Width + " × " + Height; }
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public static class ResolutionProfiles
    {
        public const string InstalledFileName = "GpgPatcher.resolution";
        public const string SelectedFileName = "selected-resolution.txt";

        public static readonly ResolutionProfile Fhd = new ResolutionProfile("FHD", "1080x1920", 1080, 1920);
        public static readonly ResolutionProfile Qhd = new ResolutionProfile("QHD", "1440x2560", 1440, 2560);
        public static readonly ResolutionProfile Uhd = new ResolutionProfile("UHD", "2160x3840", 2160, 3840);

        private static readonly ResolutionProfile[] SupportedProfiles = { Fhd, Qhd, Uhd };

        public static ResolutionProfile Default
        {
            get { return Uhd; }
        }

        public static IReadOnlyList<ResolutionProfile> All
        {
            get { return SupportedProfiles; }
        }

        public static bool TryParse(string value, out ResolutionProfile profile)
        {
            profile = null;
            if (value == null)
            {
                return false;
            }

            for (var index = 0; index < SupportedProfiles.Length; index++)
            {
                var candidate = SupportedProfiles[index];
                if (string.Equals(value, candidate.Value, StringComparison.Ordinal))
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

        public static ResolutionProfile Parse(string value)
        {
            ResolutionProfile profile;
            if (!TryParse(value, out profile))
            {
                throw new ArgumentException(
                    "Unsupported resolution '" + (value ?? string.Empty)
                    + "'. Supported values: " + SupportedValues + ".",
                    nameof(value));
            }

            return profile;
        }

        public static ResolutionProfile ReadFileOrDefault(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return Default;
                }

                ResolutionProfile profile;
                return TryParse(File.ReadAllText(path).Trim(), out profile) ? profile : Default;
            }
            catch
            {
                return Default;
            }
        }

        public static string SupportedValues
        {
            get { return Fhd.Value + "|" + Qhd.Value + "|" + Uhd.Value; }
        }
    }
}

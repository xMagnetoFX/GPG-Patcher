using System;

namespace GpgPatcher
{
    public sealed class PatchCommandOptions
    {
        private PatchCommandOptions(ResolutionProfile resolution, bool enablePhenotypeFallback)
        {
            Resolution = resolution;
            EnablePhenotypeFallback = enablePhenotypeFallback;
        }

        public ResolutionProfile Resolution { get; }

        public bool EnablePhenotypeFallback { get; }

        public static PatchCommandOptions Parse(string[] arguments)
        {
            var resolution = ResolutionProfiles.Default;
            var resolutionSeen = false;
            var phenotypeFallback = false;

            for (var index = 0; index < arguments.Length; index++)
            {
                var argument = arguments[index];
                if (string.Equals(argument, "--resolution", StringComparison.OrdinalIgnoreCase))
                {
                    if (resolutionSeen)
                    {
                        throw new FriendlyException("The --resolution option may only be specified once.");
                    }

                    resolutionSeen = true;
                    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new FriendlyException(
                            "The --resolution option requires one of: " + ResolutionProfiles.SupportedValues + ".");
                    }

                    var value = arguments[++index];
                    if (!ResolutionProfiles.TryParse(value, out resolution))
                    {
                        throw new FriendlyException(
                            "Unsupported resolution '" + value + "'. Supported values: " + ResolutionProfiles.SupportedValues + ".");
                    }

                    continue;
                }

                if (string.Equals(argument, "--phenotype-fallback", StringComparison.OrdinalIgnoreCase))
                {
                    if (phenotypeFallback)
                    {
                        throw new FriendlyException("The --phenotype-fallback option may only be specified once.");
                    }

                    phenotypeFallback = true;
                    continue;
                }

                throw new FriendlyException("Unknown patch option '" + argument + "'.");
            }

            if (phenotypeFallback && !resolution.IsUhd)
            {
                throw new FriendlyException("--phenotype-fallback is only supported with the UHD 2160x3840 profile.");
            }

            return new PatchCommandOptions(resolution, phenotypeFallback);
        }

        public static string BuildArguments(ResolutionProfile resolution, bool enablePhenotypeFallback)
        {
            if (resolution == null)
            {
                throw new ArgumentNullException(nameof(resolution));
            }

            if (enablePhenotypeFallback && !resolution.IsUhd)
            {
                throw new ArgumentException("Phenotype fallback is only available for UHD.", nameof(enablePhenotypeFallback));
            }

            return "patch --resolution " + resolution.Value
                + (enablePhenotypeFallback ? " --phenotype-fallback" : string.Empty);
        }
    }
}

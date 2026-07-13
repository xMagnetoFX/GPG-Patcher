using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GpgPatcher
{
    internal static class PlayGamesInstanceLauncher
    {
        public static PlayGamesAccount ResolveByInstanceId(
            IReadOnlyList<PlayGamesAccount> accounts,
            string instanceId)
        {
            Guid parsed;
            if (string.IsNullOrWhiteSpace(instanceId) || !Guid.TryParse(instanceId, out parsed))
            {
                throw new FriendlyException("Instance ID must be a valid Google Play Games UUID.");
            }

            var match = (accounts ?? Array.Empty<PlayGamesAccount>())
                .FirstOrDefault(account => string.Equals(
                    account.InstanceId,
                    instanceId.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new FriendlyException(
                    "Instance '" + instanceId.Trim() + "' is stale or unknown. Refresh Accounts and try again.");
            }

            EnsureLaunchable(match);
            return match;
        }

        public static PlayGamesAccount ResolveByIndex(
            IReadOnlyList<PlayGamesAccount> accounts,
            int oneBasedIndex)
        {
            if (oneBasedIndex < 1 || accounts == null || oneBasedIndex > accounts.Count)
            {
                throw new FriendlyException(
                    "Account row " + oneBasedIndex + " is not available. Refresh Accounts and choose a current row.");
            }

            var account = accounts[oneBasedIndex - 1];
            EnsureLaunchable(account);
            return account;
        }

        public static PlayGamesAccount ResolveByGamerTag(
            IReadOnlyList<PlayGamesAccount> accounts,
            string gamerTag)
        {
            if (string.IsNullOrWhiteSpace(gamerTag))
            {
                throw new FriendlyException("Gamer tag is required.");
            }

            var matches = (accounts ?? Array.Empty<PlayGamesAccount>())
                .Where(account => string.Equals(
                    account.GamerTag,
                    gamerTag.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0)
            {
                throw new FriendlyException(
                    "Gamer tag '" + gamerTag.Trim() + "' was not found. Run accounts to see the current list.");
            }

            if (matches.Count > 1)
            {
                throw new FriendlyException(
                    "Gamer tag '" + gamerTag.Trim() + "' matches multiple instances. Use launch-instance with the instance ID instead.");
            }

            EnsureLaunchable(matches[0]);
            return matches[0];
        }

        public static string BuildLaunchUrl(PlayGamesAccount account)
        {
            EnsureLaunchable(account);
            return GpgConstants.BuildExactLaunchProtocolUrl(account.AndroidAppLibraryId);
        }

        public static string Launch(PlayGamesAccount account)
        {
            var launchUrl = BuildLaunchUrl(account);
            Process.Start(new ProcessStartInfo
            {
                FileName = launchUrl,
                UseShellExecute = true,
            });
            return launchUrl;
        }

        private static void EnsureLaunchable(PlayGamesAccount account)
        {
            if (account == null || string.IsNullOrWhiteSpace(account.AndroidAppLibraryId))
            {
                throw new FriendlyException(
                    "This Google Play Games instance does not have an Android app library ID yet. Refresh after its profile finishes loading.");
            }
        }
    }
}

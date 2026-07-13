using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GpgPatcher
{
    public sealed class PlayGamesAccount
    {
        public string InstanceId { get; internal set; }

        public string AndroidAppLibraryId { get; internal set; }

        public string GamerTag { get; internal set; }

        public string MaskedEmail { get; internal set; }

        public string AvatarPath { get; internal set; }

        public bool IsForeground { get; internal set; }

        public string Initials
        {
            get { return PlayGamesAccountMapper.GetInitials(GamerTag); }
        }
    }

    internal sealed class PersistedPlayGamesAccount
    {
        public string InstanceId { get; set; }

        public string AndroidAppLibraryId { get; set; }

        public string GamerTag { get; set; }

        public string Email { get; set; }
    }

    internal static class PlayGamesAccountMapper
    {
        public static IReadOnlyList<PlayGamesAccount> Map(
            IEnumerable<PersistedPlayGamesAccount> persistedAccounts,
            string foregroundInstanceId,
            string profileImageCacheDirectory)
        {
            var results = new List<PlayGamesAccount>();
            var index = 0;
            foreach (var persisted in persistedAccounts ?? Enumerable.Empty<PersistedPlayGamesAccount>())
            {
                index++;
                if (persisted == null || string.IsNullOrWhiteSpace(persisted.InstanceId))
                {
                    continue;
                }

                var gamerTag = string.IsNullOrWhiteSpace(persisted.GamerTag)
                    ? "Google Play Games account " + index
                    : persisted.GamerTag.Trim();
                var avatarPath = FindAvatar(profileImageCacheDirectory, persisted.InstanceId);

                results.Add(new PlayGamesAccount
                {
                    InstanceId = persisted.InstanceId.Trim(),
                    AndroidAppLibraryId = (persisted.AndroidAppLibraryId ?? string.Empty).Trim(),
                    GamerTag = gamerTag,
                    MaskedEmail = MaskEmail(persisted.Email),
                    AvatarPath = avatarPath,
                    IsForeground = string.Equals(
                        persisted.InstanceId,
                        foregroundInstanceId,
                        StringComparison.OrdinalIgnoreCase),
                });
            }

            return results;
        }

        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return "Email unavailable";
            }

            var trimmed = email.Trim();
            var at = trimmed.LastIndexOf('@');
            if (at <= 0 || at == trimmed.Length - 1)
            {
                return "Email unavailable";
            }

            var local = trimmed.Substring(0, at);
            var domain = trimmed.Substring(at + 1);
            var visible = local.Length == 1
                ? local.Substring(0, 1)
                : local.Substring(0, Math.Min(2, local.Length));
            var suffix = local.Length > 3 ? local.Substring(local.Length - 1, 1) : string.Empty;
            return visible + "***" + suffix + "@" + domain;
        }

        public static string GetInitials(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GP";
            }

            var parts = value.Trim()
                .Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
            }

            var single = parts.Length == 0 ? value.Trim() : parts[0];
            return single.Substring(0, Math.Min(2, single.Length)).ToUpperInvariant();
        }

        private static string FindAvatar(string directory, string instanceId)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                var candidate = Path.Combine(directory, instanceId + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    public sealed class PlayGamesAccountRepository
    {
        private const string PersistedStateTypeName = "Google.Hpe.Service.Instances.PersistedInstancesModuleState";
        private const string EncryptionManagerTypeName = "Google.Play.Games.Encryption.EncryptionManager";
        private readonly string localDataDirectory;
        private readonly string serviceDirectory;

        public PlayGamesAccountRepository()
            : this(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google",
                    "Play Games"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Google",
                    "Play Games",
                    "current",
                    "service"))
        {
        }

        internal PlayGamesAccountRepository(string localDataDirectory, string serviceDirectory)
        {
            this.localDataDirectory = localDataDirectory;
            this.serviceDirectory = serviceDirectory;
        }

        public IReadOnlyList<PlayGamesAccount> ReadAccounts()
        {
            var databasePath = Path.Combine(localDataDirectory, "store.db");
            var keyPath = Path.Combine(localDataDirectory, "instances_encryption_key");
            EnsureFile(databasePath, "Google Play Games account store");
            EnsureFile(keyPath, "Google Play Games instances encryption key");
            EnsureFile(Path.Combine(serviceDirectory, "Encryption.dll"), "Google Play Games encryption library");
            EnsureFile(Path.Combine(serviceDirectory, "Service.Protos.dll"), "Google Play Games instance-state library");

            try
            {
                var encrypted = WindowsSqlite.ReadSingleBlob(
                    databasePath,
                    "SELECT EncryptedPersistedInstancesModuleState FROM PersistedInstancesModuleState LIMIT 1");
                if (encrypted == null || encrypted.Length == 0)
                {
                    return Array.Empty<PlayGamesAccount>();
                }

                var plainState = Decrypt(encrypted, keyPath);
                return Parse(plainState);
            }
            catch (FriendlyException)
            {
                throw;
            }
            catch (TargetInvocationException ex)
            {
                throw new FriendlyException(
                    "Google Play Games account data could not be decrypted or parsed. "
                    + FriendlyMessage(ex.InnerException ?? ex),
                    ex.InnerException ?? ex);
            }
            catch (Exception ex)
            {
                throw new FriendlyException(
                    "Google Play Games account data is unavailable. " + FriendlyMessage(ex),
                    ex);
            }
        }

        private byte[] Decrypt(byte[] encrypted, string keyPath)
        {
            var nativeLibrary = LoadLibrary(Path.Combine(serviceDirectory, "boringssl_wrapper.dll"));
            if (nativeLibrary == IntPtr.Zero)
            {
                throw new FriendlyException("Google Play Games encryption support could not be loaded.");
            }

            try
            {
                var encryptionAssembly = Assembly.LoadFrom(Path.Combine(serviceDirectory, "Encryption.dll"));
                var encryptionType = encryptionAssembly.GetType(EncryptionManagerTypeName, true);
                var manager = Activator.CreateInstance(encryptionType);
                var loadKey = encryptionType.GetMethod("LoadEncryptionKey", new[] { typeof(string), typeof(byte[]).MakeByRefType() });
                var decrypt = encryptionType.GetMethod("Decrypt", new[] { typeof(byte[]) });
                if (loadKey == null || decrypt == null)
                {
                    throw new FriendlyException("The installed Google Play Games encryption API is not compatible with this build.");
                }

                object[] keyArguments = { keyPath, null };
                var loaded = (bool)loadKey.Invoke(manager, keyArguments);
                if (!loaded || !(keyArguments[1] is byte[]) || ((byte[])keyArguments[1]).Length == 0)
                {
                    throw new FriendlyException(
                        "The Google Play Games instances encryption key could not be opened for the current Windows user.");
                }

                return (byte[])decrypt.Invoke(manager, new object[] { encrypted });
            }
            finally
            {
                FreeLibrary(nativeLibrary);
            }
        }

        private IReadOnlyList<PlayGamesAccount> Parse(byte[] plainState)
        {
            LoadManagedDependency("System.Runtime.CompilerServices.Unsafe.dll");
            LoadManagedDependency("System.Memory.dll");
            LoadManagedDependency("System.Buffers.dll");
            LoadManagedDependency("System.Numerics.Vectors.dll");
            LoadManagedDependency("System.Threading.Tasks.Extensions.dll");
            LoadManagedDependency("Google.Protobuf.dll");
            LoadManagedDependency("ProtobufUtils.dll");
            LoadManagedDependency("AndroidUsersAPI.dll");
            LoadManagedDependency("AppLibraryAPI.dll");
            LoadManagedDependency("PcGameLibraryAPI.dll");
            var protos = Assembly.LoadFrom(Path.Combine(serviceDirectory, "Service.Protos.dll"));
            var persistedType = protos.GetType(PersistedStateTypeName, true);
            var parser = persistedType.GetProperty("Parser", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
            var parseFrom = parser.GetType().GetMethod("ParseFrom", new[] { typeof(byte[]) });
            if (parseFrom == null)
            {
                throw new FriendlyException("The installed Google Play Games instance-state parser is not compatible with this build.");
            }

            var persistedState = parseFrom.Invoke(parser, new object[] { plainState });
            var state = GetProperty(persistedState, "State");
            if (state == null)
            {
                return Array.Empty<PlayGamesAccount>();
            }

            var foreground = GetRawString(GetProperty(state, "ForegroundInstanceId"));
            var instances = GetProperty(state, "Instances") as IEnumerable;
            var records = new List<PersistedPlayGamesAccount>();
            if (instances != null)
            {
                foreach (var entry in instances)
                {
                    var key = GetProperty(entry, "Key");
                    var value = GetProperty(entry, "Value");
                    var associations = GetProperty(value, "Associations");
                    var playerInfo = GetProperty(associations, "PgsPlayerInfo");
                    records.Add(new PersistedPlayGamesAccount
                    {
                        InstanceId = GetRawString(key),
                        AndroidAppLibraryId = GetIdentifier(GetProperty(associations, "AppLibraryId")),
                        GamerTag = GetString(playerInfo, "GamerTag"),
                        Email = GetString(associations, "Email"),
                    });
                }
            }

            return PlayGamesAccountMapper.Map(
                records,
                foreground,
                Path.Combine(localDataDirectory, "profile_image_cache"));
        }

        private void LoadManagedDependency(string fileName)
        {
            var path = Path.Combine(serviceDirectory, fileName);
            if (File.Exists(path))
            {
                Assembly.LoadFrom(path);
            }
        }

        private static object GetProperty(object value, string name)
        {
            if (value == null)
            {
                return null;
            }

            var property = value.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(value, null);
        }

        private static string GetString(object value, string name)
        {
            return GetProperty(value, name) as string ?? string.Empty;
        }

        private static string GetRawString(object value)
        {
            return GetString(value, "RawString");
        }

        private static string GetIdentifier(object value)
        {
            var rawToken = GetString(value, "RawTokenString");
            return string.IsNullOrWhiteSpace(rawToken) ? GetRawString(value) : rawToken;
        }

        private static void EnsureFile(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FriendlyException(description + " was not found at '" + path + "'.");
            }
        }

        private static string FriendlyMessage(Exception exception)
        {
            if (exception is IOException || exception is UnauthorizedAccessException)
            {
                return "Close or restart Google Play Games, then use Refresh Accounts again.";
            }

            return "Use Refresh Accounts to try again; no account credentials were stored or logged.";
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);
    }

    internal static class WindowsSqlite
    {
        private const int SqliteOk = 0;
        private const int SqliteRow = 100;
        private const int SqliteDone = 101;
        private const int SqliteOpenReadOnly = 1;

        public static byte[] ReadSingleBlob(string databasePath, string sql)
        {
            IntPtr database;
            var openResult = sqlite3_open_v2(databasePath, out database, SqliteOpenReadOnly, IntPtr.Zero);
            if (openResult != SqliteOk)
            {
                var message = database == IntPtr.Zero ? "unknown SQLite error" : ReadUtf8(sqlite3_errmsg(database));
                if (database != IntPtr.Zero)
                {
                    sqlite3_close(database);
                }

                throw new FriendlyException("Google Play Games account store could not be opened: " + message + ".");
            }

            try
            {
                sqlite3_busy_timeout(database, 2000);
                IntPtr statement;
                IntPtr remainder;
                var prepareResult = sqlite3_prepare_v2(database, sql, -1, out statement, out remainder);
                if (prepareResult != SqliteOk)
                {
                    throw new FriendlyException(
                        "Google Play Games account store has an unsupported or corrupt schema: "
                        + ReadUtf8(sqlite3_errmsg(database)) + ".");
                }

                try
                {
                    var stepResult = sqlite3_step(statement);
                    if (stepResult == SqliteDone)
                    {
                        return null;
                    }

                    if (stepResult != SqliteRow)
                    {
                        throw new FriendlyException(
                            "Google Play Games account state could not be read: "
                            + ReadUtf8(sqlite3_errmsg(database)) + ".");
                    }

                    var length = sqlite3_column_bytes(statement, 0);
                    var pointer = sqlite3_column_blob(statement, 0);
                    if (pointer == IntPtr.Zero || length <= 0)
                    {
                        return Array.Empty<byte>();
                    }

                    var result = new byte[length];
                    Marshal.Copy(pointer, result, 0, length);
                    return result;
                }
                finally
                {
                    sqlite3_finalize(statement);
                }
            }
            finally
            {
                sqlite3_close(database);
            }
        }

        private static string ReadUtf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            var length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int sqlite3_open_v2(
            string filename,
            out IntPtr database,
            int flags,
            IntPtr virtualFileSystem);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int sqlite3_prepare_v2(
            IntPtr database,
            string sql,
            int sqlLength,
            out IntPtr statement,
            out IntPtr remainder);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr statement, int column);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr statement, int column);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);
    }
}

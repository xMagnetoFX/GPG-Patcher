using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using GpgPatcher;

namespace GpgPatcher.Gui
{
    internal sealed class PatcherProcessRunner
    {
        private readonly string hostPath;

        public PatcherProcessRunner()
        {
            using (var process = Process.GetCurrentProcess())
            {
                hostPath = process.MainModule == null
                    ? Application.ExecutablePath
                    : process.MainModule.FileName;
            }
        }

        public string HostPath
        {
            get { return hostPath; }
        }

        public bool HostExists
        {
            get { return File.Exists(hostPath); }
        }

        public Task<CommandResult> RunCapturedAsync(string arguments)
        {
            return Task.Run(() => RunCaptured(arguments));
        }

        public Task<CommandResult> RunElevatedAsync(string arguments)
        {
            return Task.Run(() => RunElevated(arguments));
        }

        private CommandResult RunCaptured(string arguments)
        {
            EnsureHostExists();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = BuildHeadlessArguments(arguments),
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                process.Start();
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CommandResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdout,
                    StandardError = stderr,
                };
            }
        }

        private CommandResult RunElevated(string arguments)
        {
            EnsureHostExists();

            if (IsAdministrator())
            {
                return RunCaptured(arguments);
            }

            var logPath = CreateCommandLogPath();

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = BuildHeadlessArgumentsWithLog(arguments, logPath),
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas",
                };

                process.Start();
                process.WaitForExit();

                return new CommandResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = ReadLogFile(logPath),
                    StandardError = File.Exists(logPath)
                        ? string.Empty
                        : "The elevated command did not write a log file: " + logPath,
                };
            }
        }

        private void EnsureHostExists()
        {
            if (!HostExists)
            {
                throw new FileNotFoundException("Could not find the GPG Patcher executable required to run maintenance commands.", hostPath);
            }
        }

        private static string BuildHeadlessArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return "--headless";
            }

            return "--headless " + arguments.Trim();
        }

        private static string BuildHeadlessArgumentsWithLog(string arguments, string logPath)
        {
            var commandArguments = string.IsNullOrWhiteSpace(arguments)
                ? "--log-file " + QuoteArgument(logPath)
                : arguments.Trim() + " --log-file " + QuoteArgument(logPath);

            return BuildHeadlessArguments(commandArguments);
        }

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        private static string CreateCommandLogPath()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                GpgConstants.AppDataDirectoryName,
                "command-logs");
            Directory.CreateDirectory(directory);

            return Path.Combine(
                directory,
                DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".log");
        }

        private static string ReadLogFile(string path)
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            return File.ReadAllText(path);
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}

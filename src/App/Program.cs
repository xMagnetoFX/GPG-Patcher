using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GpgPatcher;

namespace GpgPatcher.Gui
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args != null
                && args.Length > 0
                && string.Equals(args[0], "--headless", StringComparison.OrdinalIgnoreCase))
            {
                return RunHeadless(args.Skip(1).ToArray());
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static int RunHeadless(string[] args)
        {
            var commandArgs = RemoveLogFileOption(args ?? Array.Empty<string>(), out var logPath);
            if (string.IsNullOrWhiteSpace(logPath))
            {
                return PatcherCommandHost.Run(commandArgs);
            }

            var logDirectory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            using (var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.AutoFlush = true;
                Console.SetOut(writer);
                Console.SetError(writer);
                return PatcherCommandHost.Run(commandArgs);
            }
        }

        private static string[] RemoveLogFileOption(string[] args, out string logPath)
        {
            logPath = null;
            var cleaned = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "--log-file", StringComparison.OrdinalIgnoreCase)
                    && index + 1 < args.Length)
                {
                    logPath = args[index + 1];
                    index++;
                    continue;
                }

                cleaned.Add(args[index]);
            }

            return cleaned.ToArray();
        }
    }
}

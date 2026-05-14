using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm
    {
        private async Task RefreshInspectAsync()
        {
            await RunCapturedCommandAsync("inspect", "Refreshing status");
        }

        private async Task VerifyAsync()
        {
            await RunCapturedCommandAsync("verify", "Verifying latest launch");
        }

        private async Task PatchAsync()
        {
            var arguments = phenotypeFallbackToggle.Checked ? "patch --phenotype-fallback" : "patch";
            await RunElevatedCommandAsync(arguments, "Applying patch");
        }

        private async Task AddAccountAsync()
        {
            await RunCapturedCommandAsync("add-account", "Starting add-account flow");
        }

        private async Task RestoreAsync()
        {
            await RunElevatedCommandAsync("restore", "Restoring original files");
        }

        private async Task RunCapturedCommandAsync(string arguments, string busyMessage)
        {
            SetBusy(true, busyMessage + "...");
            try
            {
                var result = await runner.RunCapturedAsync(arguments);
                UpdateOutput(arguments, result.StandardOutput, result.StandardError, result.ExitCode);

                if (string.Equals(arguments, "inspect", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyInspectSummary(result.StandardOutput);
                }
                else
                {
                    var inspectResult = await runner.RunCapturedAsync("inspect");
                    AppendInspectRefresh(arguments, result, inspectResult);
                    ApplyInspectSummary(inspectResult.StandardOutput);
                }

                if (result.Success)
                {
                    UpdateStatusPresentation("Ready", palette.SuccessSoft, palette.Success, palette.IsDark ? Color.FromArgb(220, 252, 231) : palette.Success, busyMessage + " complete.");
                }
                else
                {
                    UpdateStatusPresentation("Attention", palette.WarningSoft, palette.Warning, palette.TextPrimary, busyMessage + " finished with errors.");
                }
            }
            catch (Exception ex)
            {
                SetCommandErrorOutput(arguments, ex);
                UpdateStatusPresentation("Error", palette.DangerSoft, palette.Danger, palette.IsDark ? Color.FromArgb(254, 226, 226) : palette.Danger, busyMessage + " failed.");
            }
            finally
            {
                SetBusy(false, statusDetailLabel.Text);
            }
        }

        private async Task RunElevatedCommandAsync(string arguments, string busyMessage)
        {
            SetBusy(true, busyMessage + "... accept the Windows UAC prompt if it appears.");
            try
            {
                var exitCode = await runner.RunElevatedAsync(arguments);
                var inspectResult = await runner.RunCapturedAsync("inspect");
                UpdateElevatedOutput(arguments, exitCode, inspectResult);
                ApplyInspectSummary(inspectResult.StandardOutput);

                if (exitCode == 0)
                {
                    UpdateStatusPresentation("Ready", palette.SuccessSoft, palette.Success, palette.IsDark ? Color.FromArgb(220, 252, 231) : palette.Success, busyMessage + " complete.");
                }
                else
                {
                    UpdateStatusPresentation("Attention", palette.WarningSoft, palette.Warning, palette.TextPrimary, busyMessage + " exited with code " + exitCode + ".");
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                outputTextBox.Text = ">>> " + arguments + Environment.NewLine + "UAC prompt was canceled by the user.";
                UpdateStatusPresentation("Canceled", palette.WarningSoft, palette.Warning, palette.TextPrimary, busyMessage + " canceled.");
            }
            catch (Exception ex)
            {
                SetCommandErrorOutput(arguments, ex);
                UpdateStatusPresentation("Error", palette.DangerSoft, palette.Danger, palette.IsDark ? Color.FromArgb(254, 226, 226) : palette.Danger, busyMessage + " failed.");
            }
            finally
            {
                SetBusy(false, statusDetailLabel.Text);
            }
        }

        private void ApplyInspectSummary(string output)
        {
            var summary = InspectSummary.Parse(output);

            versionCard.ValueText = summary.Version ?? "-";
            versionCard.DetailText = summary.IsCompatible
                ? "Installed build passed compatibility checks"
                : (string.IsNullOrWhiteSpace(summary.CompatibilityMessage) ? "Compatibility check failed" : summary.CompatibilityMessage);
            versionCard.ApplyTheme(palette, summary.IsCompatible);

            compatibilityCard.ValueText = summary.IsCompatible ? "Compatible" : "Incompatible";
            compatibilityCard.DetailText = summary.IsCompatible
                ? "Safe to patch this build"
                : (string.IsNullOrWhiteSpace(summary.CompatibilityState) ? "Patch blocked by compatibility checks" : summary.CompatibilityState);
            compatibilityCard.ApplyTheme(palette, summary.IsCompatible);

            patchCard.ValueText = summary.IsPatched ? "Patched" : "Not patched";
            patchCard.DetailText = "Service " + FormatState(summary.ServiceLibPatched)
                + "  •  launch " + FormatState(summary.LaunchSettingsHook)
                + "  •  viewport " + FormatState(summary.MonitorDisplayHook)
                + "  •  sharp " + FormatState(summary.SharpeningFilterHook);
            patchCard.DetailText += "  |  accounts " + FormatState(summary.AccountLimitBypassHook)
                + "  |  add " + FormatState(summary.AddAccountDeepLinkHook)
                + "  |  hook " + FormatState(summary.HookDllCompatible);
            patchCard.ApplyTheme(palette, summary.IsPatched);

            backupCard.ValueText = summary.HasBackup ? "Available" : "Missing";
            backupCard.DetailText = summary.HasPhenotypeOverride ? "Backup ready  •  phenotype override on" : "Backup ready for restore";
            backupCard.ApplyTheme(palette, summary.HasBackup);

            launchSizeCard.ValueText = summary.DisplaySize ?? "-";
            launchSizeCard.DetailText = "Latest Whiteout Survival launch size";
            launchSizeCard.ApplyTheme(palette, summary.IsPatched);

            densityCard.ValueText = summary.Density ?? "-";
            densityCard.DetailText = "Scaled Android display density";
            densityCard.ApplyTheme(palette, false);

            guestDisplayCard.ValueText = summary.GuestDisplay ?? "-";
            guestDisplayCard.DetailText = "Android serial display creation size";
            guestDisplayCard.ApplyTheme(palette, summary.IsPatched);

            resolutionCapCard.ValueText = summary.ResolutionCap ?? "-";
            resolutionCapCard.DetailText = summary.HasPhenotypeOverride ? "Phenotype fallback currently enabled" : "Current host-side resolution cap";
            resolutionCapCard.ApplyTheme(palette, false);

            heroSubtitleLabel.Text = summary.IsPatched
                ? "Whiteout Survival is currently using the host-side patched UHD portrait mode."
                : "Whiteout Survival is currently on the stock Google Play Games launch path.";
            heroMetaLabel.Text = "Target package: com.gof.global  •  UHD portrait target: " + GpgConstants.TargetResolutionLabel + "  •  Density " + (summary.Density ?? "-");

            UpdatePatchStatePresentation(summary.IsPatched);
            UpdateCompatibilityPresentation(summary);
        }

        private void UpdateOutput(string arguments, string stdout, string stderr, int exitCode)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + arguments);
            builder.AppendLine("Exit code: " + exitCode);

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                builder.AppendLine();
                builder.AppendLine(stdout.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                builder.AppendLine();
                builder.AppendLine("stderr:");
                builder.AppendLine(stderr.TrimEnd());
            }

            outputCaptionLabel.Text = "Command Output  •  " + arguments;
            SetOutputText(builder.ToString());
        }

        private void UpdateElevatedOutput(string arguments, int exitCode, CommandResult inspectResult)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + arguments);
            builder.AppendLine("Elevated command exited with code " + exitCode + ".");
            builder.AppendLine();
            builder.AppendLine(">>> inspect");
            builder.AppendLine("Exit code: " + inspectResult.ExitCode);

            if (!string.IsNullOrWhiteSpace(inspectResult.StandardOutput))
            {
                builder.AppendLine();
                builder.AppendLine(inspectResult.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(inspectResult.StandardError))
            {
                builder.AppendLine();
                builder.AppendLine("stderr:");
                builder.AppendLine(inspectResult.StandardError.TrimEnd());
            }

            outputCaptionLabel.Text = "Command Output  •  " + arguments;
            SetOutputText(builder.ToString());
        }

        private void SetCommandErrorOutput(string arguments, Exception ex)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + arguments);
            builder.AppendLine("The command could not be completed.");
            builder.AppendLine();
            builder.AppendLine(ex.Message);

            outputCaptionLabel.Text = "Command Output  •  " + arguments;
            SetOutputText(builder.ToString());
        }

        private void AppendInspectRefresh(string commandName, CommandResult commandResult, CommandResult inspectResult)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + commandName);
            builder.AppendLine("Exit code: " + commandResult.ExitCode);

            if (!string.IsNullOrWhiteSpace(commandResult.StandardOutput))
            {
                builder.AppendLine();
                builder.AppendLine(commandResult.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(commandResult.StandardError))
            {
                builder.AppendLine();
                builder.AppendLine("stderr:");
                builder.AppendLine(commandResult.StandardError.TrimEnd());
            }

            builder.AppendLine();
            builder.AppendLine(">>> inspect");
            builder.AppendLine("Exit code: " + inspectResult.ExitCode);

            if (!string.IsNullOrWhiteSpace(inspectResult.StandardOutput))
            {
                builder.AppendLine();
                builder.AppendLine(inspectResult.StandardOutput.TrimEnd());
            }

            outputCaptionLabel.Text = "Command Output  •  " + commandName;
            SetOutputText(builder.ToString());
        }

        private void SetBusy(bool busy, string message)
        {
            refreshButton.Enabled = !busy;
            verifyButton.Enabled = !busy;
            patchButton.Enabled = !busy;
            addAccountButton.Enabled = !busy;
            restoreButton.Enabled = !busy;
            phenotypeFallbackToggle.Enabled = !busy;
            busyProgressBar.Visible = busy;
            UseWaitCursor = busy;

            if (busy)
            {
                UpdateStatusPresentation("Working", palette.AccentSoft, palette.Accent, palette.TextPrimary, message);
            }
        }

        private void UpdatePatchStatePresentation(bool isPatched)
        {
            patchStateChip.Text = isPatched ? "Patched" : "Stock";
            patchStateChip.FillColor = isPatched ? palette.SuccessSoft : palette.WarningSoft;
            patchStateChip.BorderColor = isPatched ? palette.Success : palette.Warning;
            patchStateChip.TextColor = isPatched
                ? (palette.IsDark ? Color.FromArgb(220, 252, 231) : palette.Success)
                : (palette.IsDark ? Color.FromArgb(255, 243, 214) : palette.Warning);
            patchStateChip.Invalidate();
        }

        private void UpdateCompatibilityPresentation(InspectSummary summary)
        {
            if (summary == null)
            {
                compatibilityChip.Text = "Checking";
                compatibilityChip.FillColor = palette.Surface;
                compatibilityChip.BorderColor = palette.Border;
                compatibilityChip.TextColor = palette.TextPrimary;
                compatibilityChip.Invalidate();
                return;
            }

            compatibilityChip.Text = summary.IsCompatible ? "Compatible" : "Incompatible";
            compatibilityChip.FillColor = summary.IsCompatible ? palette.AccentSoft : palette.DangerSoft;
            compatibilityChip.BorderColor = summary.IsCompatible ? palette.Accent : palette.Danger;
            compatibilityChip.TextColor = summary.IsCompatible
                ? (palette.IsDark ? Color.FromArgb(219, 234, 254) : palette.Accent)
                : (palette.IsDark ? Color.FromArgb(254, 226, 226) : palette.Danger);
            compatibilityChip.Invalidate();
        }

        private void UpdateStatusPresentation(string chipText, Color fill, Color border, Color textColor, string detail)
        {
            statusChip.Text = chipText;
            statusChip.FillColor = fill;
            statusChip.BorderColor = border;
            statusChip.TextColor = textColor;
            statusChip.Invalidate();
            statusDetailLabel.Text = detail;
        }

        private static string FormatState(string value)
        {
            if (string.Equals(value, "True", StringComparison.OrdinalIgnoreCase))
            {
                return "on";
            }

            if (string.Equals(value, "False", StringComparison.OrdinalIgnoreCase))
            {
                return "off";
            }

            return "-";
        }

        private void SetOutputText(string value)
        {
            outputTextBox.Text = value;
            outputTextBox.Select(0, 0);
            outputTextBox.ScrollToCaret();
        }
    }
}

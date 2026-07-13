using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        private async Task DiagnoseViewportAsync()
        {
            await RunCapturedCommandAsync("viewport-diagnose", "Diagnosing viewport");
        }

        private async Task PatchAsync()
        {
            var arguments = PatchCommandOptions.BuildArguments(
                selectedResolutionProfile,
                phenotypeFallbackToggle.Checked && selectedResolutionProfile.IsUhd);
            await RunElevatedCommandAsync(arguments, "Applying patch");
        }

        private async Task AddAccountAsync()
        {
            await RunCapturedCommandAsync("add-account", "Starting add-account flow");
            await RefreshAccountsAsync();
        }

        private async Task LaunchAccountAsync(PlayGamesAccount account)
        {
            await RunCapturedCommandAsync(
                "launch-instance " + account.InstanceId,
                "Launching " + account.GamerTag);
        }

        private async Task RefreshAccountsAsync()
        {
            if (accountsRefreshInProgress || accountsFlowPanel == null)
            {
                return;
            }

            accountsRefreshInProgress = true;
            refreshAccountsButton.Enabled = false;
            accountCountChip.Text = "Loading";
            accountsStateLabel.Text = "READING PLAY GAMES ACCOUNTS";
            ShowAccountsMessage(
                "Refreshing accounts",
                "Decrypting the local Play Games instance index for this Windows user.",
                palette.AccentSoft,
                palette.BorderStrong);
            try
            {
                var accounts = await Task.Run(() => accountRepository.ReadAccounts());
                RenderAccounts(accounts);
            }
            catch (Exception ex)
            {
                accountCountChip.Text = "Unavailable";
                accountCountChip.FillColor = palette.DangerSoft;
                accountCountChip.BorderColor = palette.Danger;
                accountCountChip.Invalidate();
                accountsStateLabel.Text = "ACCOUNT DATA UNAVAILABLE";
                ShowAccountsMessage(
                    "Accounts could not be loaded",
                    ex.Message,
                    palette.DangerSoft,
                    palette.Danger);
            }
            finally
            {
                accountsRefreshInProgress = false;
                refreshAccountsButton.Enabled = !UseWaitCursor;
            }
        }

        private void RenderAccounts(System.Collections.Generic.IReadOnlyList<PlayGamesAccount> accounts)
        {
            DisposeAccountCards();
            var count = accounts == null ? 0 : accounts.Count;
            accountCountChip.Text = count + (count == 1 ? " account" : " accounts");
            accountCountChip.FillColor = count == 0 ? palette.WarningSoft : palette.AccentSoft;
            accountCountChip.BorderColor = count == 0 ? palette.Warning : palette.BorderStrong;
            accountCountChip.Invalidate();

            if (count == 0)
            {
                accountsStateLabel.Text = "NO ACCOUNTS FOUND";
                ShowAccountsMessage(
                    "No Play Games accounts yet",
                    "Use Add Account, finish the Google sign-in flow, then choose Refresh Accounts.",
                    palette.WarningSoft,
                    palette.Warning);
                return;
            }

            accountsStateLabel.Text = exactInstanceLaunchReady
                ? count + " ACCOUNTS  •  EXACT LAUNCH READY"
                : count + " ACCOUNTS  •  PATCH REQUIRED FOR EXACT LAUNCH";
            foreach (var account in accounts)
            {
                var card = new AccountCard(palette, account, exactInstanceLaunchReady);
                card.LaunchRequested += async (sender, args) => await LaunchAccountAsync(card.Account);
                accountsFlowPanel.Controls.Add(card);
            }

            ResizeAccountCards();
        }

        private void ShowAccountsMessage(string title, string detail, Color fill, Color border)
        {
            DisposeAccountCards();
            var surface = new ModernSurfacePanel
            {
                Width = Math.Max(360, accountsFlowPanel.ClientSize.Width - 8),
                Height = 118,
                CornerRadius = 18,
                FillColor = fill,
                BorderColor = border,
                Padding = new Padding(20, 16, 20, 16),
                Margin = new Padding(0, 4, 0, 0),
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = fill,
                Margin = new Padding(0),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(CreatePageLabel(title, 11f, FontStyle.Bold, palette.TextPrimary), 0, 0);
            layout.Controls.Add(CreatePageLabel(detail, 9f, FontStyle.Regular, palette.TextSecondary), 0, 1);
            surface.Controls.Add(layout);
            accountsFlowPanel.Controls.Add(surface);
        }

        private void ResizeAccountCards()
        {
            if (accountsFlowPanel == null || accountsFlowPanel.ClientSize.Width <= 0)
            {
                return;
            }

            var cards = accountsFlowPanel.Controls.OfType<AccountCard>().ToList();
            if (cards.Count == 0)
            {
                var message = accountsFlowPanel.Controls.OfType<ModernSurfacePanel>().FirstOrDefault();
                if (message != null)
                {
                    message.Width = Math.Max(360, accountsFlowPanel.ClientSize.Width - 8);
                }

                return;
            }

            var metrics = AccountGridLayout.Calculate(
                accountsFlowPanel.ClientSize.Width,
                DeviceDpi / 96f);
            foreach (var card in cards)
            {
                card.Width = metrics.CardWidth;
            }
        }

        private void DisposeAccountCards()
        {
            if (accountsFlowPanel == null)
            {
                return;
            }

            var controls = accountsFlowPanel.Controls.Cast<Control>().ToArray();
            accountsFlowPanel.Controls.Clear();
            foreach (var control in controls)
            {
                control.Dispose();
            }
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
                var result = await runner.RunElevatedAsync(arguments);
                var inspectResult = await runner.RunCapturedAsync("inspect");
                UpdateElevatedOutput(arguments, result, inspectResult);
                ApplyInspectSummary(inspectResult.StandardOutput);

                if (result.Success)
                {
                    UpdateStatusPresentation("Ready", palette.SuccessSoft, palette.Success, palette.IsDark ? Color.FromArgb(220, 252, 231) : palette.Success, busyMessage + " complete.");
                }
                else
                {
                    UpdateStatusPresentation("Attention", palette.WarningSoft, palette.Warning, palette.TextPrimary, busyMessage + " exited with code " + result.ExitCode + ".");
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
            exactInstanceLaunchReady = summary.HasExactInstanceLaunch;

            versionCard.ValueText = summary.Version ?? "-";
            versionCard.DetailText = summary.IsCompatible
                ? "Compatibility checks passed"
                : (string.IsNullOrWhiteSpace(summary.CompatibilityMessage) ? "Compatibility check failed" : summary.CompatibilityMessage);
            versionCard.ApplyTheme(palette, summary.IsCompatible);

            compatibilityCard.ValueText = summary.IsCompatible ? "Compatible" : "Incompatible";
            compatibilityCard.DetailText = summary.IsCompatible
                ? "Safe to patch this build"
                : (string.IsNullOrWhiteSpace(summary.CompatibilityState) ? "Patch blocked by compatibility checks" : summary.CompatibilityState);
            compatibilityCard.ApplyTheme(palette, summary.IsCompatible);

            patchCard.ValueText = summary.IsPatched ? "Patched" : "Not patched";
            patchCard.DetailText = summary.IsPatched
                ? "All managed hooks ready"
                : "Managed hooks incomplete";
            patchCard.ApplyTheme(palette, summary.IsPatched);

            backupCard.ValueText = summary.HasBackup ? "Available" : "Missing";
            backupCard.DetailText = summary.HasPhenotypeOverride ? "Backup ready; fallback on" : "Restore backup ready";
            backupCard.ApplyTheme(palette, summary.HasBackup);

            launchSizeCard.ValueText = summary.DisplaySize ?? "-";
            launchSizeCard.DetailText = "Latest launch size";
            launchSizeCard.ApplyTheme(palette, summary.IsPatched);

            densityCard.ValueText = summary.Density ?? "-";
            densityCard.DetailText = "Scaled for target height";
            densityCard.ApplyTheme(palette, false);

            guestDisplayCard.ValueText = summary.GuestDisplay ?? "-";
            guestDisplayCard.DetailText = "Serial display  •  virtual " + FormatState(summary.VirtualGuestDisplayHook);
            guestDisplayCard.ApplyTheme(palette, summary.IsPatched);

            ResolutionProfile inspectedProfile;
            if (ResolutionProfiles.TryParse(summary.TargetResolution, out inspectedProfile))
            {
                appliedResolutionProfile = inspectedProfile;
            }

            targetResolutionCard.ValueText = appliedResolutionProfile.Value;
            targetResolutionCard.DetailText = appliedResolutionProfile.Name + " profile currently applied";
            targetResolutionCard.ApplyTheme(palette, summary.IsPatched);
            homeTargetProfileChip.Text = appliedResolutionProfile.Name
                + "  •  "
                + appliedResolutionProfile.Width
                + " × "
                + appliedResolutionProfile.Height;
            ApplyResolutionSelectionPresentation();

            if (instancesPage != null
                && instancesPage.Visible
                && !accountsRefreshInProgress)
            {
                BeginInvoke(new Action(async () => await RefreshAccountsAsync()));
            }

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

            outputCaptionLabel.Text = "Activity log  •  " + arguments;
            SetOutputText(builder.ToString());
        }

        private void UpdateElevatedOutput(string arguments, CommandResult result, CommandResult inspectResult)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + arguments);
            builder.AppendLine("Elevated command exited with code " + result.ExitCode + ".");

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                builder.AppendLine();
                builder.AppendLine(result.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                builder.AppendLine();
                builder.AppendLine("stderr:");
                builder.AppendLine(result.StandardError.TrimEnd());
            }

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

            outputCaptionLabel.Text = "Activity log  •  " + arguments;
            SetOutputText(builder.ToString());
        }

        private void SetCommandErrorOutput(string arguments, Exception ex)
        {
            var builder = new StringBuilder();
            builder.AppendLine(">>> " + arguments);
            builder.AppendLine("The command could not be completed.");
            builder.AppendLine();
            builder.AppendLine(ex.Message);

            outputCaptionLabel.Text = "Activity log  •  " + arguments;
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

            outputCaptionLabel.Text = "Activity log  •  " + commandName;
            SetOutputText(builder.ToString());
        }

        private void SetBusy(bool busy, string message)
        {
            refreshButton.Enabled = !busy;
            verifyButton.Enabled = !busy;
            diagnoseViewportButton.Enabled = !busy;
            patchButton.Enabled = !busy;
            addAccountButton.Enabled = !busy;
            refreshAccountsButton.Enabled = !busy && !accountsRefreshInProgress;
            accountsFlowPanel.Enabled = !busy;
            restoreButton.Enabled = !busy;
            phenotypeFallbackToggle.Enabled = !busy && selectedResolutionProfile.IsUhd;
            if (resolutionProfileButtons != null)
            {
                foreach (var button in resolutionProfileButtons)
                {
                    button.Enabled = !busy;
                }
            }
            busyProgressBar.Visible = busy;
            UseWaitCursor = busy;

            if (busy)
            {
                UpdateStatusPresentation("Working", palette.AccentSoft, palette.Accent, palette.TextPrimary, message);
            }
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

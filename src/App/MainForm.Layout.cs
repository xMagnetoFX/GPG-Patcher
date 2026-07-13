using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm
    {
        private Control CreateHomeHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var heading = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = palette.WindowBackground,
                Margin = new Padding(2, 0, 0, 10),
            };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            heading.Controls.Add(CreatePageLabel("Overview", 22f, FontStyle.Bold, palette.TextPrimary), 0, 0);
            heading.Controls.Add(CreatePageLabel(
                "Monitor compatibility, manage the display profile, and validate the latest game launch.",
                9.5f,
                FontStyle.Regular,
                palette.TextSecondary), 0, 1);
            var patchNote = CreatePageLabel(
                "PATCH NOTE  •  Changes apply after the Play Games service restarts",
                8.5f,
                FontStyle.Bold,
                palette.Accent);
            patchNote.Margin = new Padding(0, 2, 0, 0);
            heading.Controls.Add(patchNote, 0, 2);
            header.Controls.Add(heading, 0, 0);

            var profileSummary = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                BackColor = palette.WindowBackground,
                Margin = new Padding(18, 10, 2, 22),
            };
            profileSummary.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            profileSummary.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var profileCaption = CreatePageLabel("ACTIVE PROFILE", 8f, FontStyle.Bold, palette.TextSecondary);
            profileCaption.TextAlign = ContentAlignment.MiddleRight;
            profileSummary.Controls.Add(profileCaption, 0, 0);

            homeTargetProfileChip = CreateChip();
            homeTargetProfileChip.Text = "UHD  •  2160 × 3840";
            homeTargetProfileChip.FillColor = palette.AccentSoft;
            homeTargetProfileChip.BorderColor = palette.BorderStrong;
            homeTargetProfileChip.TextColor = palette.IsDark ? palette.TextPrimary : palette.AccentPressed;
            homeTargetProfileChip.Anchor = AnchorStyles.Right;
            profileSummary.Controls.Add(homeTargetProfileChip, 0, 1);
            header.Controls.Add(profileSummary, 1, 0);

            return header;
        }

        private Control CreateActionPanel()
        {
            var actionPanel = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                Height = 76,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(18, 12, 18, 12),
                CornerRadius = 22,
                FillColor = palette.Surface,
                BorderColor = palette.Border,
            };

            var actionLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = actionPanel.FillColor,
                Margin = new Padding(0),
            };
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            actionPanel.Controls.Add(actionLayout);

            actionLayout.Controls.Add(CreatePageLabel("Quick actions", 10f, FontStyle.Bold, palette.TextPrimary), 0, 0);

            var maintenanceRow = CreateButtonRow(actionPanel.FillColor, 0, 0, 0, 0);
            maintenanceRow.Padding = new Padding(0, 5, 0, 0);
            actionLayout.Controls.Add(maintenanceRow, 1, 0);

            patchButton = CreateButton("Patch", ModernButtonTone.Primary, async (sender, args) => await PatchAsync());
            refreshButton = CreateButton("Refresh", ModernButtonTone.Secondary, async (sender, args) => await RefreshInspectAsync());
            verifyButton = CreateButton("Verify", ModernButtonTone.Secondary, async (sender, args) => await VerifyAsync());
            diagnoseViewportButton = CreateButton("Diagnose", ModernButtonTone.Secondary, async (sender, args) => await DiagnoseViewportAsync());
            restoreButton = CreateButton("Restore", ModernButtonTone.Danger, async (sender, args) => await RestoreAsync());

            patchButton.IconGlyph = "\uE898";
            refreshButton.IconGlyph = "\uE72C";
            verifyButton.IconGlyph = "\uE73E";
            diagnoseViewportButton.IconGlyph = "\uE9D9";
            restoreButton.IconGlyph = "\uE777";

            SetButtonWidth(patchButton, 124);
            SetButtonWidth(refreshButton, 136);
            SetButtonWidth(verifyButton, 112);
            SetButtonWidth(diagnoseViewportButton, 148);
            SetButtonWidth(restoreButton, 124);

            patchButton.Margin = new Padding(0, 0, 12, 0);
            refreshButton.Margin = new Padding(0, 0, 12, 0);
            verifyButton.Margin = new Padding(0, 0, 12, 0);
            diagnoseViewportButton.Margin = new Padding(0, 0, 12, 0);
            restoreButton.Margin = new Padding(0);

            maintenanceRow.Controls.Add(patchButton);
            maintenanceRow.Controls.Add(refreshButton);
            maintenanceRow.Controls.Add(verifyButton);
            maintenanceRow.Controls.Add(diagnoseViewportButton);
            maintenanceRow.Controls.Add(restoreButton);

            return actionPanel;
        }

        private Control CreateMetricsGrid()
        {
            var metricsGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 230,
                ColumnCount = 4,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 16),
                BackColor = palette.WindowBackground,
            };
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 115f));
            metricsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 115f));

            versionCard = CreateMetricCard("Version");
            compatibilityCard = CreateMetricCard("Compatibility");
            patchCard = CreateMetricCard("Patch State");
            backupCard = CreateMetricCard("Backup");
            launchSizeCard = CreateMetricCard("Launch Size");
            densityCard = CreateMetricCard("Density");
            guestDisplayCard = CreateMetricCard("Guest Display");
            targetResolutionCard = CreateMetricCard("Target Resolution");

            AddCard(metricsGrid, versionCard, 0, 0);
            AddCard(metricsGrid, compatibilityCard, 1, 0);
            AddCard(metricsGrid, patchCard, 2, 0);
            AddCard(metricsGrid, backupCard, 3, 0);
            AddCard(metricsGrid, launchSizeCard, 0, 1);
            AddCard(metricsGrid, densityCard, 1, 1);
            AddCard(metricsGrid, guestDisplayCard, 2, 1);
            AddCard(metricsGrid, targetResolutionCard, 3, 1);

            return metricsGrid;
        }

        private Control CreateOutputPanel()
        {
            var outputPanel = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(18, 14, 18, 14),
                CornerRadius = 22,
                FillColor = palette.Surface,
                BorderColor = palette.Border,
            };

            var outputLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = outputPanel.FillColor,
            };
            outputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            outputLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outputPanel.Controls.Add(outputLayout);

            var outputHeaderPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = outputPanel.FillColor,
                Margin = new Padding(0),
            };
            outputHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outputHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            outputHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            outputLayout.Controls.Add(outputHeaderPanel, 0, 0);

            outputCaptionLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = palette.TextPrimary,
                Font = palette.CreateUiFont(11f, FontStyle.Bold),
                Text = "Activity log",
                TextAlign = ContentAlignment.MiddleLeft,
            };
            outputHeaderPanel.Controls.Add(outputCaptionLabel, 0, 0);

            var outputHintLabel = new Label
            {
                AutoSize = true,
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Right,
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(8.75f, FontStyle.Regular),
                Text = "Latest maintenance transcript",
                TextAlign = ContentAlignment.MiddleRight,
            };
            outputHeaderPanel.Controls.Add(outputHintLabel, 1, 0);

            var codePanel = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(12),
                CornerRadius = 18,
                FillColor = palette.CodeBackground,
                BorderColor = palette.Border,
            };
            outputLayout.Controls.Add(codePanel, 0, 1);

            outputTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                BackColor = palette.CodeBackground,
                ForeColor = palette.CodeText,
                Font = palette.CreateMonoFont(10f),
                Margin = new Padding(0),
            };
            codePanel.Controls.Add(outputTextBox);

            return outputPanel;
        }

        private Control CreateFooterPanel()
        {
            var footerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 10, 0, 0),
                BackColor = palette.WindowBackground,
            };
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footerPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            statusChip = CreateChip();
            statusChip.Anchor = AnchorStyles.Left;
            footerPanel.Controls.Add(statusChip, 0, 0);

            statusDetailLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(9.25f, FontStyle.Regular),
                Margin = new Padding(12, 0, 12, 0),
                Text = "Ready.",
                TextAlign = ContentAlignment.MiddleLeft,
            };
            footerPanel.Controls.Add(statusDetailLabel, 1, 0);

            busyProgressBar = new ProgressBar
            {
                Width = 150,
                Height = 10,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                MarqueeAnimationSpeed = 24,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 9, 0, 9),
            };
            footerPanel.Controls.Add(busyProgressBar, 2, 0);

            return footerPanel;
        }

        private StatusChip CreateChip()
        {
            return new StatusChip
            {
                Font = palette.CreateUiFont(9f, FontStyle.Bold),
                FillColor = palette.Surface,
                BorderColor = palette.Border,
                TextColor = palette.TextPrimary,
            };
        }

        private ModernButton CreateButton(string text, ModernButtonTone tone, EventHandler onClick)
        {
            var button = new ModernButton
            {
                Text = text,
                Tone = tone,
                Palette = palette,
                CornerRadius = 14,
                Margin = new Padding(0, 0, 8, 0),
                Font = palette.CreateUiFont(9.5f, FontStyle.Bold),
            };
            button.Click += onClick;
            return button;
        }

        private static FlowLayoutPanel CreateButtonRow(Color backColor, int left, int top, int right, int bottom)
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                BackColor = backColor,
                Margin = new Padding(left, top, right, bottom),
            };
        }

        private static void SetButtonWidth(Control button, int width)
        {
            button.Width = width;
        }

        private MetricCard CreateMetricCard(string title)
        {
            var card = new MetricCard
            {
                TitleText = title,
            };
            card.ApplyTheme(palette, false);
            return card;
        }

        private static void AddCard(TableLayoutPanel grid, Control control, int column, int row)
        {
            control.Margin = new Padding(column == 0 ? 0 : 8, row == 0 ? 0 : 8, column == 3 ? 0 : 8, row == 1 ? 0 : 8);
            grid.Controls.Add(control, column, row);
        }
    }
}

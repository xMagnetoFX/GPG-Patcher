using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm
    {
        private Control CreateHeaderPanel()
        {
            var headerPanel = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                Height = 176,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(22, 18, 22, 18),
                CornerRadius = 24,
                FillColor = palette.SurfaceRaised,
                BorderColor = palette.BorderStrong,
            };

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = headerPanel.FillColor,
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            headerPanel.Controls.Add(headerLayout);

            var brandArtPanel = new BrandArtPanel
            {
                LogoImage = brandLogo,
                BorderColor = palette.IsDark ? Color.FromArgb(76, 118, 178) : Color.FromArgb(99, 146, 229),
                GradientStartColor = palette.IsDark ? Color.FromArgb(21, 72, 132) : Color.FromArgb(22, 104, 204),
                GradientEndColor = palette.IsDark ? Color.FromArgb(33, 135, 161) : Color.FromArgb(42, 194, 214),
                GlowColor = palette.IsDark ? Color.FromArgb(82, 255, 255, 255) : Color.FromArgb(112, 255, 255, 255),
                OrbColor = palette.IsDark ? Color.FromArgb(70, 147, 197, 253) : Color.FromArgb(86, 191, 219, 254),
                TextColor = Color.White,
                Anchor = AnchorStyles.Left,
            };
            headerLayout.Controls.Add(brandArtPanel, 0, 0);

            var heroTextPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 6, 0, 0),
                BackColor = headerPanel.FillColor,
            };
            heroTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            heroTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            heroTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            headerLayout.Controls.Add(heroTextPanel, 1, 0);

            var heroTitleLabel = new Label
            {
                AutoSize = true,
                ForeColor = palette.TextPrimary,
                Font = palette.CreateUiFont(21f, FontStyle.Bold),
                Text = "GPG Patcher",
                Margin = new Padding(0, 0, 0, 2),
            };
            heroTextPanel.Controls.Add(heroTitleLabel, 0, 0);

            heroSubtitleLabel = new Label
            {
                AutoSize = true,
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(10.5f, FontStyle.Regular),
                Text = "Personal host-side patch manager for Whiteout Survival on Google Play Games for PC.",
                Margin = new Padding(0, 0, 0, 6),
            };
            heroTextPanel.Controls.Add(heroSubtitleLabel, 0, 1);

            heroMetaLabel = new Label
            {
                AutoSize = true,
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(9.25f, FontStyle.Regular),
                Text = "Target package: com.gof.global  •  UHD portrait target: " + GpgConstants.TargetResolutionLabel,
            };
            heroTextPanel.Controls.Add(heroMetaLabel, 0, 2);

            var chipPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = headerPanel.FillColor,
                Anchor = AnchorStyles.Right,
                WrapContents = false,
                Margin = new Padding(12, 0, 0, 0),
            };
            headerLayout.Controls.Add(chipPanel, 2, 0);

            patchStateChip = CreateChip();
            compatibilityChip = CreateChip();
            chipPanel.Controls.Add(patchStateChip);
            chipPanel.Controls.Add(compatibilityChip);

            return headerPanel;
        }

        private Control CreateActionPanel()
        {
            var actionPanel = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                Height = 104,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(18, 14, 18, 14),
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
            };
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            actionPanel.Controls.Add(actionLayout);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                WrapContents = true,
                BackColor = actionPanel.FillColor,
                Margin = new Padding(0),
            };
            actionLayout.Controls.Add(buttonPanel, 0, 0);

            patchButton = CreateButton("Patch", ModernButtonTone.Primary, async (sender, args) => await PatchAsync());
            addAccountButton = CreateButton("Add Account", ModernButtonTone.Secondary, async (sender, args) => await AddAccountAsync());
            refreshButton = CreateButton("Refresh", ModernButtonTone.Secondary, async (sender, args) => await RefreshInspectAsync());
            verifyButton = CreateButton("Verify", ModernButtonTone.Secondary, async (sender, args) => await VerifyAsync());
            restoreButton = CreateButton("Restore", ModernButtonTone.Danger, async (sender, args) => await RestoreAsync());

            buttonPanel.Controls.Add(patchButton);
            buttonPanel.Controls.Add(addAccountButton);
            buttonPanel.Controls.Add(refreshButton);
            buttonPanel.Controls.Add(verifyButton);
            buttonPanel.Controls.Add(restoreButton);

            var togglePanel = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = actionPanel.FillColor,
                Dock = DockStyle.Right,
                Margin = new Padding(18, 0, 0, 0),
            };
            togglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            togglePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionLayout.Controls.Add(togglePanel, 1, 0);

            phenotypeFallbackToggle = new ToggleSwitch
            {
                Palette = palette,
                Margin = new Padding(0, 2, 10, 0),
            };
            togglePanel.Controls.Add(phenotypeFallbackToggle, 0, 0);
            togglePanel.SetRowSpan(phenotypeFallbackToggle, 2);

            var toggleTitleLabel = new Label
            {
                AutoSize = true,
                Text = "Phenotype fallback",
                ForeColor = palette.TextPrimary,
                Font = palette.CreateUiFont(9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 2),
            };
            togglePanel.Controls.Add(toggleTitleLabel, 1, 0);

            var toggleHintLabel = new Label
            {
                AutoSize = true,
                Text = "Only use this if the normal host patch stops exposing the UHD mode.",
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(8.75f, FontStyle.Regular),
                MaximumSize = new Size(340, 0),
            };
            togglePanel.Controls.Add(toggleHintLabel, 1, 1);

            return actionPanel;
        }

        private Control CreateMetricsGrid()
        {
            var metricsGrid = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 22),
                BackColor = palette.WindowBackground,
            };
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            metricsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 114f));
            metricsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 114f));

            versionCard = CreateMetricCard("Version");
            compatibilityCard = CreateMetricCard("Compatibility");
            patchCard = CreateMetricCard("Patch State");
            backupCard = CreateMetricCard("Backup");
            launchSizeCard = CreateMetricCard("Launch Size");
            densityCard = CreateMetricCard("Density");
            guestDisplayCard = CreateMetricCard("Guest Display");
            resolutionCapCard = CreateMetricCard("Resolution Cap");

            AddCard(metricsGrid, versionCard, 0, 0);
            AddCard(metricsGrid, compatibilityCard, 1, 0);
            AddCard(metricsGrid, patchCard, 2, 0);
            AddCard(metricsGrid, backupCard, 3, 0);
            AddCard(metricsGrid, launchSizeCard, 0, 1);
            AddCard(metricsGrid, densityCard, 1, 1);
            AddCard(metricsGrid, guestDisplayCard, 2, 1);
            AddCard(metricsGrid, resolutionCapCard, 3, 1);

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
                Text = "Command Output",
                TextAlign = ContentAlignment.MiddleLeft,
            };
            outputHeaderPanel.Controls.Add(outputCaptionLabel, 0, 0);

            var outputHintLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(8.75f, FontStyle.Regular),
                Text = "Live transcript from the latest maintenance command",
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
                Margin = new Padding(0, 0, 10, 0),
                Font = palette.CreateUiFont(9.5f, FontStyle.Bold),
            };
            button.Click += onClick;
            return button;
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
            control.Margin = new Padding(column == 0 ? 0 : 6, row == 0 ? 0 : 6, column == 3 ? 0 : 6, row == 1 ? 0 : 6);
            grid.Controls.Add(control, column, row);
        }
    }
}

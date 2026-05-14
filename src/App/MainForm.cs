using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm : Form
    {
        private readonly ThemePalette palette;
        private readonly PatcherProcessRunner runner;
        private readonly Image brandLogo;
        private readonly Icon appIcon;
        private ModernButton refreshButton;
        private ModernButton verifyButton;
        private ModernButton patchButton;
        private ModernButton addAccountButton;
        private ModernButton restoreButton;
        private ToggleSwitch phenotypeFallbackToggle;
        private StatusChip patchStateChip;
        private StatusChip compatibilityChip;
        private StatusChip statusChip;
        private Label heroMetaLabel;
        private Label heroSubtitleLabel;
        private Label outputCaptionLabel;
        private Label statusDetailLabel;
        private MetricCard versionCard;
        private MetricCard compatibilityCard;
        private MetricCard patchCard;
        private MetricCard backupCard;
        private MetricCard launchSizeCard;
        private MetricCard densityCard;
        private MetricCard guestDisplayCard;
        private MetricCard resolutionCapCard;
        private RichTextBox outputTextBox;
        private ProgressBar busyProgressBar;

        public MainForm()
        {
            palette = ThemePalette.Detect();
            runner = new PatcherProcessRunner();
            brandLogo = BrandingAssets.TryLoadLogoImage();
            appIcon = BrandingAssets.TryLoadApplicationIcon();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);

            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = palette.WindowBackground;
            ForeColor = palette.TextPrimary;
            Font = palette.CreateUiFont(9.5f, FontStyle.Regular);
            MinimumSize = new Size(1180, 850);
            Size = new Size(1280, 900);
            StartPosition = FormStartPosition.CenterScreen;
            Text = "GPG Patcher";
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20),
                BackColor = palette.WindowBackground,
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            Controls.Add(root);

            root.Controls.Add(CreateHeaderPanel(), 0, 0);
            root.Controls.Add(CreateActionPanel(), 0, 1);
            root.Controls.Add(CreateMetricsGrid(), 0, 2);
            root.Controls.Add(CreateOutputPanel(), 0, 3);
            root.Controls.Add(CreateFooterPanel(), 0, 4);

            ApplyInitialChrome();
            Shown += async (sender, args) => await RefreshInspectAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (brandLogo != null)
                {
                    brandLogo.Dispose();
                }

                if (appIcon != null)
                {
                    appIcon.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void ApplyInitialChrome()
        {
            UpdateStatusPresentation("Ready", palette.AccentSoft, palette.BorderStrong, palette.TextPrimary, "Refresh the current patch state or run Verify after launching the game.");
            UpdatePatchStatePresentation(false);
            UpdateCompatibilityPresentation(null);
        }
    }
}

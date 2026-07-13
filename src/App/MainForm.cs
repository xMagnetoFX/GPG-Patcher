using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm : Form
    {
        private readonly ThemePalette palette;
        private readonly PatcherProcessRunner runner;
        private readonly PlayGamesAccountRepository accountRepository;
        private readonly Image brandLogo;
        private readonly Icon appIcon;
        private Panel pageHost;
        private Control homePage;
        private Control instancesPage;
        private Control settingsPage;
        private ModernButton homeNavigationButton;
        private ModernButton instancesNavigationButton;
        private ModernButton settingsNavigationButton;
        private ModernButton refreshButton;
        private ModernButton verifyButton;
        private ModernButton diagnoseViewportButton;
        private ModernButton patchButton;
        private ModernButton addAccountButton;
        private ModernButton refreshAccountsButton;
        private FlowLayoutPanel accountsFlowPanel;
        private StatusChip accountCountChip;
        private Label accountsStateLabel;
        private bool exactInstanceLaunchReady;
        private bool accountsRefreshInProgress;
        private ModernButton restoreButton;
        private ModernButton[] resolutionProfileButtons;
        private ToggleSwitch phenotypeFallbackToggle;
        private Label phenotypeFallbackStateLabel;
        private Label phenotypeFallbackNoteLabel;
        private Label appliedResolutionLabel;
        private Label resolutionApplyMessageLabel;
        private StatusChip homeTargetProfileChip;
        private ResolutionProfile selectedResolutionProfile;
        private ResolutionProfile appliedResolutionProfile;
        private StatusChip statusChip;
        private Label outputCaptionLabel;
        private Label statusDetailLabel;
        private MetricCard versionCard;
        private MetricCard compatibilityCard;
        private MetricCard patchCard;
        private MetricCard backupCard;
        private MetricCard launchSizeCard;
        private MetricCard densityCard;
        private MetricCard guestDisplayCard;
        private MetricCard targetResolutionCard;
        private RichTextBox outputTextBox;
        private ProgressBar busyProgressBar;

        public MainForm()
        {
            palette = ThemePalette.Detect();
            runner = new PatcherProcessRunner();
            accountRepository = new PlayGamesAccountRepository();
            selectedResolutionProfile = ResolutionProfileStorage.ReadSelected();
            appliedResolutionProfile = ResolutionProfiles.Default;
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
            MinimumSize = new Size(1120, 780);
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(
                Math.Max(MinimumSize.Width, Math.Min(1280, workingArea.Width)),
                Math.Max(MinimumSize.Height, Math.Min(960, workingArea.Height)));
            StartPosition = FormStartPosition.CenterScreen;
            Text = string.Empty;
            ShowIcon = false;
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = palette.WindowBackground,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            root.Controls.Add(CreateNavigationPanel(), 0, 0);
            root.Controls.Add(CreatePageHost(), 1, 0);

            ApplyInitialChrome();
            NavigateTo(AppPage.Home);
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
        }
    }
}

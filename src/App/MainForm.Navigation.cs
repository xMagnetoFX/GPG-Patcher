using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed partial class MainForm
    {
        private enum AppPage
        {
            Home,
            Instances,
            Settings,
        }

        private Control CreateNavigationPanel()
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.Surface,
                Padding = new Padding(16, 18, 16, 16),
                Margin = new Padding(0),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = sidebar.BackColor,
                Margin = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            sidebar.Controls.Add(layout);

            var brand = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = sidebar.BackColor,
                Margin = new Padding(0, 0, 0, 12),
            };
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            layout.Controls.Add(brand, 0, 0);

            var logo = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = brandLogo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 4, 10, 4),
                BackColor = sidebar.BackColor,
            };
            brand.Controls.Add(logo, 0, 0);
            brand.SetRowSpan(logo, 2);

            brand.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "GPG Patcher",
                ForeColor = palette.TextPrimary,
                Font = palette.CreateUiFont(11f, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0),
            }, 1, 0);
            brand.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "CONTROL CENTER",
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(7.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(0, 2, 0, 0),
            }, 1, 1);

            layout.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "NAVIGATION",
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(8, 0, 0, 4),
            }, 0, 1);

            homeNavigationButton = CreateNavigationButton("Home", "\uE80F", (sender, args) => NavigateTo(AppPage.Home));
            instancesNavigationButton = CreateNavigationButton("Accounts", "\uE716", (sender, args) => NavigateTo(AppPage.Instances));
            settingsNavigationButton = CreateNavigationButton("Settings", "\uE713", (sender, args) => NavigateTo(AppPage.Settings));
            layout.Controls.Add(homeNavigationButton, 0, 2);
            layout.Controls.Add(instancesNavigationButton, 0, 3);
            layout.Controls.Add(settingsNavigationButton, 0, 4);

            return sidebar;
        }

        private Control CreatePageHost()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));

            pageHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            layout.Controls.Add(pageHost, 0, 0);

            homePage = CreateHomePage();
            instancesPage = CreateInstancesPage();
            settingsPage = CreateSettingsPage();
            pageHost.Controls.Add(homePage);
            pageHost.Controls.Add(instancesPage);
            pageHost.Controls.Add(settingsPage);

            var footerHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.WindowBackground,
                Padding = new Padding(20, 0, 20, 8),
                Margin = new Padding(0),
            };
            footerHost.Controls.Add(CreateFooterPanel());
            layout.Controls.Add(footerHost, 0, 1);

            return layout;
        }

        private Control CreateHomePage()
        {
            var page = CreatePageContainer();
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(24, 20, 24, 0),
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 246f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(CreateHomeHeader(), 0, 0);
            root.Controls.Add(CreateActionPanel(), 0, 1);
            root.Controls.Add(CreateMetricsGrid(), 0, 2);
            root.Controls.Add(CreateOutputPanel(), 0, 3);
            page.Controls.Add(root);
            return page;
        }

        private Control CreateInstancesPage()
        {
            var page = CreatePageContainer();
            var root = CreateSecondaryPageLayout("Accounts", "Launch Whiteout Survival directly into an exact Google Play Games account.");
            page.Controls.Add(root);

            var instanceCard = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 24,
                FillColor = palette.Surface,
                BorderColor = palette.Border,
                Padding = new Padding(22),
                Margin = new Padding(0),
            };
            root.Controls.Add(instanceCard, 0, 1);

            var cardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = instanceCard.FillColor,
                Margin = new Padding(0),
            };
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            instanceCard.Controls.Add(cardLayout);

            var cardHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = instanceCard.FillColor,
                Margin = new Padding(0, 0, 0, 12),
            };
            cardHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            cardHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cardHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            cardHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            cardHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            cardLayout.Controls.Add(cardHeader, 0, 0);

            cardHeader.Controls.Add(CreatePageLabel("Play Games accounts", 13f, FontStyle.Bold, palette.TextPrimary), 0, 0);
            cardHeader.Controls.Add(CreatePageLabel("Profiles are read locally from Google Play Games and emails stay masked.", 9f, FontStyle.Regular, palette.TextSecondary), 0, 1);

            accountCountChip = new StatusChip
            {
                Text = "Loading",
                Font = palette.CreateUiFont(8f, FontStyle.Bold),
                FillColor = palette.AccentSoft,
                BorderColor = palette.BorderStrong,
                TextColor = palette.TextPrimary,
                Margin = new Padding(12, 10, 12, 0),
            };
            cardHeader.Controls.Add(accountCountChip, 1, 0);
            cardHeader.SetRowSpan(accountCountChip, 2);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = instanceCard.FillColor,
                Margin = new Padding(0, 4, 0, 0),
            };
            refreshAccountsButton = CreateButton("Refresh Accounts", ModernButtonTone.Secondary, async (sender, args) => await RefreshAccountsAsync());
            refreshAccountsButton.Size = new Size(148, 44);
            refreshAccountsButton.Margin = new Padding(0, 0, 8, 0);
            actions.Controls.Add(refreshAccountsButton);
            addAccountButton = CreateButton("Add Account", ModernButtonTone.Primary, async (sender, args) => await AddAccountAsync());
            addAccountButton.Size = new Size(126, 44);
            addAccountButton.Margin = new Padding(0);
            actions.Controls.Add(addAccountButton);
            cardHeader.Controls.Add(actions, 2, 0);
            cardHeader.SetRowSpan(actions, 2);

            accountsStateLabel = CreatePageLabel("LOADING ACCOUNTS", 8f, FontStyle.Bold, palette.TextSecondary);
            cardLayout.Controls.Add(accountsStateLabel, 0, 1);

            accountsFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = instanceCard.FillColor,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0),
            };
            accountsFlowPanel.ClientSizeChanged += (sender, args) => ResizeAccountCards();
            cardLayout.Controls.Add(accountsFlowPanel, 0, 2);

            return page;
        }

        private Control CreateSettingsPage()
        {
            var page = CreatePageContainer();
            var root = CreateSecondaryPageLayout("Settings", "Choose the portrait resolution that the next Patch will apply.");
            page.Controls.Add(root);

            var settingsContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            root.Controls.Add(settingsContent, 0, 1);

            var settingsStack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 404,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            settingsStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            settingsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 216f));
            settingsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 12f));
            settingsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 176f));
            settingsContent.Controls.Add(settingsStack);

            settingsStack.Controls.Add(CreateResolutionProfileCard(), 0, 0);
            settingsStack.Controls.Add(CreatePhenotypeFallbackCard(), 0, 2);
            ApplyResolutionSelectionPresentation();

            return page;
        }

        private Control CreateResolutionProfileCard()
        {
            var resolutionCard = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 22,
                FillColor = palette.Surface,
                BorderColor = palette.Border,
                Padding = new Padding(24, 16, 24, 16),
                Margin = new Padding(0),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = resolutionCard.FillColor,
                Margin = new Padding(0),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            resolutionCard.Controls.Add(layout);

            layout.Controls.Add(CreatePageLabel("TARGET DISPLAY", 8f, FontStyle.Bold, palette.TextSecondary), 0, 0);
            layout.Controls.Add(CreatePageLabel("Portrait resolution", 12f, FontStyle.Bold, palette.TextPrimary), 0, 1);
            layout.Controls.Add(CreatePageLabel("Select the display size used by every managed display hook.", 9f, FontStyle.Regular, palette.TextSecondary), 0, 2);

            var buttonRow = CreateButtonRow(resolutionCard.FillColor, 0, 4, 0, 4);
            layout.Controls.Add(buttonRow, 0, 3);

            var profiles = ResolutionProfiles.All;
            resolutionProfileButtons = new ModernButton[profiles.Count];
            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                var button = CreateButton(
                    profile.Name + "   " + profile.Value,
                    ModernButtonTone.Secondary,
                    (sender, args) => SelectResolutionProfile(profile));
                button.Width = 188;
                button.Height = 44;
                button.AccessibleName = profile.Name + " resolution " + profile.Value;
                button.Tag = profile.Value;
                resolutionProfileButtons[index] = button;
                buttonRow.Controls.Add(button);
            }

            var stateLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = resolutionCard.FillColor,
                Margin = new Padding(0),
            };
            stateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            stateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.Controls.Add(stateLayout, 0, 4);

            appliedResolutionLabel = CreatePageLabel(string.Empty, 8.5f, FontStyle.Bold, palette.Accent);
            appliedResolutionLabel.AutoSize = true;
            appliedResolutionLabel.Dock = DockStyle.Left;
            appliedResolutionLabel.Padding = new Padding(0, 0, 22, 0);
            stateLayout.Controls.Add(appliedResolutionLabel, 0, 0);

            resolutionApplyMessageLabel = CreatePageLabel(string.Empty, 8.5f, FontStyle.Regular, palette.TextSecondary);
            stateLayout.Controls.Add(resolutionApplyMessageLabel, 1, 0);

            return resolutionCard;
        }

        private Control CreatePhenotypeFallbackCard()
        {
            var fallbackCard = new ModernSurfacePanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 22,
                FillColor = palette.Surface,
                BorderColor = palette.Border,
                Padding = new Padding(24, 20, 24, 20),
                Margin = new Padding(0),
            };

            var settingLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4,
                BackColor = fallbackCard.FillColor,
                Margin = new Padding(0),
            };
            settingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58f));
            settingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            settingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116f));
            settingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            settingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            settingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            settingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            fallbackCard.Controls.Add(settingLayout);

            var settingIcon = new Label
            {
                Dock = DockStyle.Fill,
                Text = "\uE713",
                ForeColor = palette.Accent,
                Font = new Font("Segoe Fluent Icons", 18f, FontStyle.Regular, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 10, 0),
            };
            settingLayout.Controls.Add(settingIcon, 0, 0);
            settingLayout.SetRowSpan(settingIcon, 4);

            settingLayout.Controls.Add(CreatePageLabel("PATCH BEHAVIOR", 8f, FontStyle.Bold, palette.TextSecondary), 1, 0);
            settingLayout.Controls.Add(CreatePageLabel("Phenotype fallback", 12f, FontStyle.Bold, palette.TextPrimary), 1, 1);
            settingLayout.Controls.Add(CreatePageLabel("Adds the optional phenotype override when Patch runs. Leave this off unless the normal host patch stops exposing UHD mode.", 9f, FontStyle.Regular, palette.TextSecondary), 1, 2);
            phenotypeFallbackNoteLabel = CreatePageLabel("Changing this switch does not modify files until you run Patch.", 8.5f, FontStyle.Regular, palette.Warning);
            settingLayout.Controls.Add(phenotypeFallbackNoteLabel, 1, 3);

            phenotypeFallbackToggle = new ToggleSwitch
            {
                Palette = palette,
                AccessibleName = "Phenotype fallback",
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 16, 0, 0),
            };
            settingLayout.Controls.Add(phenotypeFallbackToggle, 2, 0);
            settingLayout.SetRowSpan(phenotypeFallbackToggle, 3);

            phenotypeFallbackStateLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Off",
                ForeColor = palette.TextSecondary,
                Font = palette.CreateUiFont(8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.TopCenter,
                Margin = new Padding(0),
            };
            settingLayout.Controls.Add(phenotypeFallbackStateLabel, 2, 3);
            phenotypeFallbackToggle.CheckedChanged += (sender, args) =>
            {
                ApplyPhenotypeAvailabilityPresentation();
            };

            return fallbackCard;
        }

        private void SelectResolutionProfile(ResolutionProfile profile)
        {
            selectedResolutionProfile = profile;
            if (!profile.IsUhd)
            {
                phenotypeFallbackToggle.Checked = false;
            }

            try
            {
                ResolutionProfileStorage.WriteSelected(profile);
            }
            catch (Exception ex)
            {
                UpdateStatusPresentation(
                    "Error",
                    palette.DangerSoft,
                    palette.Danger,
                    palette.TextPrimary,
                    "Could not remember the selected resolution: " + ex.Message);
            }

            ApplyResolutionSelectionPresentation();
        }

        private void ApplyResolutionSelectionPresentation()
        {
            if (resolutionProfileButtons != null)
            {
                foreach (var button in resolutionProfileButtons)
                {
                    var selected = string.Equals(
                        button.Tag as string,
                        selectedResolutionProfile.Value,
                        StringComparison.Ordinal);
                    button.Tone = selected ? ModernButtonTone.Primary : ModernButtonTone.Secondary;
                    button.Invalidate();
                }
            }

            if (appliedResolutionLabel != null)
            {
                appliedResolutionLabel.Text = "APPLIED  " + appliedResolutionProfile.Name + "  " + appliedResolutionProfile.Value;
            }

            if (resolutionApplyMessageLabel != null)
            {
                var matchesApplied = string.Equals(
                    selectedResolutionProfile.Value,
                    appliedResolutionProfile.Value,
                    StringComparison.Ordinal);
                resolutionApplyMessageLabel.Text = matchesApplied
                    ? "This profile is currently active."
                    : selectedResolutionProfile.Name + " is selected • Applies on next Patch";
                resolutionApplyMessageLabel.ForeColor = matchesApplied ? palette.TextSecondary : palette.Warning;
            }

            ApplyPhenotypeAvailabilityPresentation();
        }

        private void ApplyPhenotypeAvailabilityPresentation()
        {
            if (phenotypeFallbackToggle == null)
            {
                return;
            }

            var available = selectedResolutionProfile.IsUhd;
            phenotypeFallbackToggle.Enabled = available && !UseWaitCursor;
            if (!available && phenotypeFallbackToggle.Checked)
            {
                phenotypeFallbackToggle.Checked = false;
            }

            if (phenotypeFallbackStateLabel != null)
            {
                phenotypeFallbackStateLabel.Text = available
                    ? (phenotypeFallbackToggle.Checked ? "On" : "Off")
                    : "Unavailable";
                phenotypeFallbackStateLabel.ForeColor = available && phenotypeFallbackToggle.Checked
                    ? palette.Accent
                    : palette.TextSecondary;
            }

            if (phenotypeFallbackNoteLabel != null)
            {
                phenotypeFallbackNoteLabel.Text = available
                    ? "Changing this switch does not modify files until you run Patch."
                    : "Disabled for FHD and QHD because this Google setting specifically enables 4K UHD.";
            }
        }

        private TableLayoutPanel CreateSecondaryPageLayout(string title, string subtitle)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(28, 28, 28, 18),
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var heading = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0, 0, 0, 18),
            };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            heading.Controls.Add(CreatePageLabel(title, 22f, FontStyle.Bold, palette.TextPrimary), 0, 0);
            heading.Controls.Add(CreatePageLabel(subtitle, 10f, FontStyle.Regular, palette.TextSecondary), 0, 1);
            root.Controls.Add(heading, 0, 0);
            return root;
        }

        private Panel CreatePageContainer()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = palette.WindowBackground,
                Margin = new Padding(0),
            };
        }

        private Label CreatePageLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                ForeColor = color,
                Font = palette.CreateUiFont(size, style),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0),
            };
        }

        private ModernButton CreateNavigationButton(string text, string iconGlyph, EventHandler onClick)
        {
            var button = CreateButton(text, ModernButtonTone.Ghost, onClick);
            button.IconGlyph = iconGlyph;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 4, 0, 4);
            return button;
        }

        private void NavigateTo(AppPage page)
        {
            if (homePage == null || instancesPage == null || settingsPage == null)
            {
                return;
            }

            homePage.Visible = page == AppPage.Home;
            instancesPage.Visible = page == AppPage.Instances;
            settingsPage.Visible = page == AppPage.Settings;

            var activePage = page == AppPage.Home
                ? homePage
                : page == AppPage.Instances ? instancesPage : settingsPage;
            activePage.BringToFront();

            SetNavigationButtonState(homeNavigationButton, page == AppPage.Home);
            SetNavigationButtonState(instancesNavigationButton, page == AppPage.Instances);
            SetNavigationButtonState(settingsNavigationButton, page == AppPage.Settings);

            if (page == AppPage.Instances)
            {
                BeginInvoke(new Action(async () => await RefreshAccountsAsync()));
            }
        }

        private static void SetNavigationButtonState(ModernButton button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.Tone = isActive ? ModernButtonTone.Primary : ModernButtonTone.Ghost;
            button.Invalidate();
        }
    }
}

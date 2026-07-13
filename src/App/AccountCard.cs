using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed class AccountCard : ModernSurfacePanel
    {
        private readonly PlayGamesAccount account;

        public AccountCard(ThemePalette palette, PlayGamesAccount account, bool exactLaunchReady)
        {
            this.account = account;
            CornerRadius = 20;
            FillColor = palette.SurfaceTint;
            BorderColor = account.IsForeground ? palette.Success : palette.Border;
            Padding = new Padding(16);
            Margin = new Padding(0, 0, 12, 12);
            Height = 176;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = FillColor,
                Margin = new Padding(0),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            Controls.Add(layout);

            var identity = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = FillColor,
                Margin = new Padding(0),
            };
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70f));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.Controls.Add(identity, 0, 0);

            identity.Controls.Add(new AccountAvatar(palette, account), 0, 0);

            var labels = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = FillColor,
                Margin = new Padding(0, 6, 8, 2),
            };
            labels.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            labels.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            labels.Controls.Add(CreateLabel(account.GamerTag, palette.CreateUiFont(11f, FontStyle.Bold), palette.TextPrimary), 0, 0);
            labels.Controls.Add(CreateLabel(account.MaskedEmail, palette.CreateUiFont(8.5f, FontStyle.Regular), palette.TextSecondary), 0, 1);
            identity.Controls.Add(labels, 1, 0);

            var state = new StatusChip
            {
                Text = account.IsForeground ? "Active" : "Ready",
                Font = palette.CreateUiFont(8f, FontStyle.Bold),
                FillColor = account.IsForeground ? palette.SuccessSoft : palette.Surface,
                BorderColor = account.IsForeground ? palette.Success : palette.Border,
                TextColor = account.IsForeground
                    ? (palette.IsDark ? Color.FromArgb(220, 252, 231) : palette.Success)
                    : palette.TextSecondary,
                Margin = new Padding(4, 8, 0, 0),
            };
            identity.Controls.Add(state, 2, 0);

            var action = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = FillColor,
                Margin = new Padding(0),
            };
            action.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            action.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
            layout.Controls.Add(action, 0, 1);

            var canLaunch = exactLaunchReady && !string.IsNullOrWhiteSpace(account.AndroidAppLibraryId);
            var note = canLaunch
                ? "Exact instance  •  " + ShortId(account.InstanceId)
                : exactLaunchReady
                    ? "Profile is still preparing its game library"
                    : "Patch required for exact launch";
            action.Controls.Add(CreateLabel(note, palette.CreateUiFont(8f, FontStyle.Regular), palette.TextSecondary), 0, 0);

            var launch = new ModernButton
            {
                Text = "Launch",
                Tone = ModernButtonTone.Primary,
                Palette = palette,
                CornerRadius = 13,
                Dock = DockStyle.Fill,
                Enabled = canLaunch,
                Font = palette.CreateUiFont(9f, FontStyle.Bold),
                Margin = new Padding(8, 2, 0, 2),
            };
            launch.Click += (sender, args) => OnLaunchRequested();
            action.Controls.Add(launch, 1, 0);
        }

        public event EventHandler LaunchRequested;

        public PlayGamesAccount Account
        {
            get { return account; }
        }

        private void OnLaunchRequested()
        {
            var handler = LaunchRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static Label CreateLabel(string text, Font font, Color color)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                Font = font,
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0),
            };
        }

        private static string ShortId(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Length <= 8
                ? value
                : value.Substring(0, 8);
        }
    }

    internal sealed class AccountAvatar : Control
    {
        private readonly ThemePalette palette;
        private readonly string initials;
        private readonly Font initialsFont;
        private Image image;

        public AccountAvatar(ThemePalette palette, PlayGamesAccount account)
        {
            this.palette = palette;
            initials = account.Initials;
            initialsFont = palette.CreateUiFont(11f, FontStyle.Bold);
            Size = new Size(58, 58);
            Margin = new Padding(0, 7, 12, 0);
            DoubleBuffered = true;
            image = TryLoadImage(account.AvatarPath);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                if (image != null)
                {
                    var graphicsState = e.Graphics.Save();
                    e.Graphics.SetClip(path);
                    e.Graphics.DrawImage(image, bounds);
                    e.Graphics.Restore(graphicsState);
                }
                else
                {
                    using (var brush = new SolidBrush(palette.AccentSoft))
                    {
                        e.Graphics.FillPath(brush, path);
                    }

                    TextRenderer.DrawText(
                        e.Graphics,
                        initials,
                        initialsFont,
                        bounds,
                        palette.Accent,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                using (var pen = new Pen(palette.BorderStrong))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image != null)
            {
                image.Dispose();
                image = null;
            }

            if (disposing)
            {
                initialsFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Image TryLoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

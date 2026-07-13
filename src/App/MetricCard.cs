using System;
using System.Drawing;
using System.Windows.Forms;

namespace GpgPatcher.Gui
{
    internal sealed class MetricCard : ModernSurfacePanel
    {
        private readonly Label titleLabel;
        private readonly Label valueLabel;
        private readonly Label detailLabel;
        private readonly Label accentDotLabel;

        public MetricCard()
        {
            CornerRadius = 20;
            Padding = new Padding(16, 14, 16, 14);
            Height = 112;
            Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(layout);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16f));

            titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            accentDotLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Text = "●",
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0),
            };
            valueLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            detailLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
            };

            headerLayout.Controls.Add(titleLabel, 0, 0);
            headerLayout.Controls.Add(accentDotLabel, 1, 0);
            layout.Controls.Add(headerLayout, 0, 0);
            layout.Controls.Add(valueLabel, 0, 1);
            layout.Controls.Add(detailLabel, 0, 2);
        }

        public string TitleText
        {
            get { return titleLabel.Text; }
            set { titleLabel.Text = value; }
        }

        public string ValueText
        {
            get { return valueLabel.Text; }
            set { valueLabel.Text = value; }
        }

        public string DetailText
        {
            get { return detailLabel.Text; }
            set { detailLabel.Text = value; }
        }

        public void ApplyTheme(ThemePalette palette, bool highlighted)
        {
            FillColor = highlighted ? palette.SurfaceTint : palette.Surface;
            BorderColor = highlighted ? palette.BorderStrong : palette.Border;
            titleLabel.ForeColor = palette.TextSecondary;
            valueLabel.ForeColor = palette.TextPrimary;
            detailLabel.ForeColor = palette.TextSecondary;
            accentDotLabel.ForeColor = highlighted ? palette.Success : palette.Accent;
            titleLabel.Font = palette.CreateUiFont(8.5f, FontStyle.Bold);
            accentDotLabel.Font = palette.CreateUiFont(8f, FontStyle.Bold);
            valueLabel.Font = palette.CreateUiFont(15.5f, FontStyle.Bold);
            detailLabel.Font = palette.CreateUiFont(8.5f, FontStyle.Regular);
        }
    }
}

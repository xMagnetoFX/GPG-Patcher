using System;

namespace GpgPatcher
{
    internal struct AccountGridMetrics
    {
        public int Columns { get; set; }

        public int CardWidth { get; set; }
    }

    internal static class AccountGridLayout
    {
        public static AccountGridMetrics Calculate(int clientWidth, float dpiScale)
        {
            var scale = dpiScale <= 0f ? 1f : dpiScale;
            var scrollbarAllowance = (int)Math.Ceiling(22f * scale);
            var gap = (int)Math.Ceiling(12f * scale);
            var usable = Math.Max(0, clientWidth - scrollbarAllowance);
            var columns = usable >= (int)Math.Ceiling(900f * scale) ? 3 : 2;
            var minimumCardWidth = (int)Math.Ceiling(280f * scale);
            var cardWidth = Math.Max(
                minimumCardWidth,
                (usable - (columns - 1) * gap) / columns);
            return new AccountGridMetrics
            {
                Columns = columns,
                CardWidth = cardWidth,
            };
        }
    }
}

using System.Windows;

namespace AutoCapture.Wpf.Views
{
    internal enum SelectionHandle { TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

    /// <summary>영역 선택 오버레이의 좌표 계산(캔버스 좌표, DIP).</summary>
    internal static class SelectionMath
    {
        public static Rect FromPoints(Point first, Point second, Rect bounds) => Clamp(new Rect(first, second), bounds);

        public static Rect Clamp(Rect rect, Rect bounds)
        {
            var clamped = Rect.Intersect(rect, bounds);
            return clamped.IsEmpty ? Rect.Empty : clamped;
        }

        /// <summary>잡은 손잡이 쪽 변만 움직인다. 반대쪽 변은 고정하고 최소 크기를 지킨다.</summary>
        public static Rect Resize(Rect origin, SelectionHandle handle, Vector delta, double minimum, Rect bounds)
        {
            double left = origin.Left, top = origin.Top, right = origin.Right, bottom = origin.Bottom;
            if (handle is SelectionHandle.TopLeft or SelectionHandle.Left or SelectionHandle.BottomLeft)
                left = Math.Max(bounds.Left, Math.Min(left + delta.X, right - minimum));
            if (handle is SelectionHandle.TopRight or SelectionHandle.Right or SelectionHandle.BottomRight)
                right = Math.Min(bounds.Right, Math.Max(right + delta.X, left + minimum));
            if (handle is SelectionHandle.TopLeft or SelectionHandle.Top or SelectionHandle.TopRight)
                top = Math.Max(bounds.Top, Math.Min(top + delta.Y, bottom - minimum));
            if (handle is SelectionHandle.BottomLeft or SelectionHandle.Bottom or SelectionHandle.BottomRight)
                bottom = Math.Min(bounds.Bottom, Math.Max(bottom + delta.Y, top + minimum));
            return new Rect(new Point(left, top), new Point(right, bottom));
        }

        /// <summary>크기를 유지한 채 옮기되 화면 밖으로 나가지 않게 한다.</summary>
        public static Rect Move(Rect origin, Vector delta, Rect bounds)
        {
            var x = Math.Clamp(origin.X + delta.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - origin.Width));
            var y = Math.Clamp(origin.Y + delta.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - origin.Height));
            return new Rect(x, y, origin.Width, origin.Height);
        }

        /// <summary>툴바는 선택 영역 아래, 자리가 없으면 위, 그래도 없으면 안쪽 아래에 둔다.</summary>
        public static Point ToolbarPosition(Rect selection, Size toolbar, Rect bounds, double gap)
        {
            var x = Math.Clamp(selection.Left + (selection.Width - toolbar.Width) / 2,
                bounds.Left + gap, Math.Max(bounds.Left + gap, bounds.Right - toolbar.Width - gap));
            var below = selection.Bottom + gap;
            if (below + toolbar.Height <= bounds.Bottom - gap) return new Point(x, below);
            var above = selection.Top - gap - toolbar.Height;
            if (above >= bounds.Top + gap) return new Point(x, above);
            return new Point(x, selection.Bottom - gap - toolbar.Height);
        }
    }
}

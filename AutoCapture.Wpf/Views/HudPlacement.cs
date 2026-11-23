using System.Windows;

namespace AutoCapture.Wpf.Views
{
    /// <summary>HUD를 둘 위치(모두 물리 픽셀). 기본은 작업 영역 위쪽 가운데다.</summary>
    internal static class HudPlacement
    {
        public static Point Compute(Rect workArea, Rect captureArea, Size hud, double margin)
        {
            var x = Math.Round(workArea.Left + (workArea.Width - hud.Width) / 2);
            var top = new Rect(x, workArea.Top + margin, hud.Width, hud.Height);
            if (!top.IntersectsWith(captureArea)) return top.TopLeft;

            // 위쪽이 캡처 영역을 가리면 아래쪽으로 옮긴다. 둘 다 겹치면 위쪽에 둔다(캡처에서는 제외된다).
            var bottom = new Rect(x, workArea.Bottom - margin - hud.Height, hud.Width, hud.Height);
            return bottom.IntersectsWith(captureArea) ? top.TopLeft : bottom.TopLeft;
        }
    }
}

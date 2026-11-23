using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace AutoCapture.Wpf.Utilities
{
    /// <summary>
    /// 커서 아래에서 고를 만한 창 영역을 찾는다(물리 픽셀). 맨 위 창부터 훑고,
    /// 자식 창(웹 뷰어의 본문 영역 등)으로 내려가 충분히 큰 가장 안쪽 영역을 고른다.
    /// </summary>
    internal static class WindowSnap
    {
        public static readonly Size MinimumChild = new(160, 120);

        public static Rect? Find(int x, int y, uint ownProcessId)
        {
            var point = new POINT { X = x, Y = y };
            nint found = 0;
            EnumWindows((window, _) =>
            {
                if (!IsCandidate(window, ownProcessId)) return true;
                if (!TryFrame(window, out var frame) || !Contains(frame, point)) return true;
                found = window;
                return false;
            }, 0);
            if (found == 0) return null;

            var chain = new List<Rect> { ToRect(FrameOf(found)) };
            var current = found;
            for (var depth = 0; depth < 8; depth++)
            {
                var client = point;
                if (!ScreenToClient(current, ref client)) break;
                var child = ChildWindowFromPointEx(current, client, CWP_SKIPINVISIBLE | CWP_SKIPTRANSPARENT);
                if (child == 0 || child == current || !GetWindowRect(child, out var rect)) break;
                chain.Add(ToRect(rect));
                current = child;
            }
            return Pick(chain, MinimumChild);
        }

        /// <summary>가장 안쪽부터 보며 최소 크기를 넘는 첫 영역을 고른다. 없으면 최상위 창.</summary>
        internal static Rect Pick(IReadOnlyList<Rect> chain, Size minimum)
        {
            for (var i = chain.Count - 1; i > 0; i--)
                if (chain[i].Width >= minimum.Width && chain[i].Height >= minimum.Height)
                    return chain[i];
            return chain[0];
        }

        private static bool IsCandidate(nint window, uint ownProcessId)
        {
            if (!IsWindowVisible(window) || IsIconic(window)) return false;
            GetWindowThreadProcessId(window, out var pid);
            if (pid == ownProcessId) return false;
            if (DwmGetWindowAttribute(window, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
                return false;
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            // 바탕 화면은 고르지 않는다.
            return name.ToString() is not ("Progman" or "WorkerW");
        }

        private static bool TryFrame(nint window, out RECT frame)
        {
            frame = FrameOf(window);
            return frame.Right > frame.Left && frame.Bottom > frame.Top;
        }

        // 그림자를 뺀 실제 창 경계
        private static RECT FrameOf(nint window)
        {
            if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frame, Marshal.SizeOf<RECT>()) == 0)
                return frame;
            GetWindowRect(window, out frame);
            return frame;
        }

        private static bool Contains(RECT rect, POINT point) =>
            point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;

        private static Rect ToRect(RECT rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int DWMWA_CLOAKED = 14;
        private const uint CWP_SKIPINVISIBLE = 0x0001;
        private const uint CWP_SKIPTRANSPARENT = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private delegate bool EnumWindowsProc(nint window, nint parameter);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
        [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out RECT rect);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window, ref POINT point);
        [DllImport("user32.dll")] private static extern nint ChildWindowFromPointEx(nint parent, POINT point, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out RECT value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    }
}

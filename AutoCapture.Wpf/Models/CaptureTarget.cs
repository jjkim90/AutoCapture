using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoCapture.Wpf.Models;

public interface ICaptureTarget
{
    string Title { get; }
    bool IsOpen { get; }
    bool IsForeground { get; }

    /// <summary>최소화돼 있으면 복원하고 앞으로 가져온다.</summary>
    void Activate();
}

internal sealed class CaptureTarget(nint handle, uint processId, string title) : ICaptureTarget
{
    public string Title => title;
    public bool IsOpen => GetWindowThreadProcessId(handle, out var current) != 0 && current == processId;
    public bool IsForeground => GetForegroundWindow() == handle && IsOpen;

    public void Activate()
    {
        if (!IsOpen) return;
        if (IsIconic(handle)) ShowWindow(handle, 9); // SW_RESTORE
        SetForegroundWindow(handle);
    }

    public static ICaptureTarget? FromArea(CaptureArea area)
    {
        var center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
        var window = GetAncestor(WindowFromPoint(center), 2); // GA_ROOT
        if (GetWindowThreadProcessId(window, out var pid) == 0 || pid == Environment.ProcessId)
            return null;

        var name = new StringBuilder(512);
        GetWindowText(window, name, name.Capacity);
        return new CaptureTarget(window, pid, name.Length > 0 ? name.ToString() : $"PID {pid}");
    }

    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint window, StringBuilder title, int capacity);
}

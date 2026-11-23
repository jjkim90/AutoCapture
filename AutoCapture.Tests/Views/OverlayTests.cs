using AutoCapture.Wpf.Utilities;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Views;
using Moq;
using Serilog;
using System.Windows;
using System.Windows.Media;

namespace AutoCapture.Tests.Views;

/// <summary>영역 선택 오버레이의 좌표 계산과 바로 시작 요청</summary>
public class OverlayTests
{
    private static readonly Rect Screen = new(0, 0, 1920, 1080);
    private static readonly Rect Box = new(100, 100, 400, 300);

    [Fact]
    public void FromPoints_NormalizesReverseDragAndClampsToScreen()
    {
        Assert.Equal(new Rect(50, 0, 150, 200), SelectionMath.FromPoints(new Point(200, 200), new Point(50, -40), Screen));
    }

    [Theory]
    [InlineData("TopLeft", -20, -10, 80, 90, 420, 310)]
    [InlineData("Right", 30, 99, 100, 100, 430, 300)]       // 세로 이동은 무시
    [InlineData("Bottom", 99, -50, 100, 100, 400, 250)]     // 가로 이동은 무시
    [InlineData("BottomRight", 10, 20, 100, 100, 410, 320)]
    public void Resize_MovesOnlyGrabbedEdges(string handle, double dx, double dy,
        double x, double y, double w, double h)
    {
        Assert.Equal(new Rect(x, y, w, h),
            SelectionMath.Resize(Box, Enum.Parse<SelectionHandle>(handle), new Vector(dx, dy), 8, Screen));
    }

    [Fact]
    public void Resize_KeepsMinimumSizeAndScreenBounds()
    {
        var shrunk = SelectionMath.Resize(Box, SelectionHandle.Left, new Vector(1000, 0), 8, Screen);
        Assert.Equal(new Rect(492, 100, 8, 300), shrunk);

        var grown = SelectionMath.Resize(Box, SelectionHandle.TopLeft, new Vector(-500, -500), 8, Screen);
        Assert.Equal(new Rect(0, 0, 500, 400), grown);
    }

    [Fact]
    public void Move_KeepsSizeInsideScreen()
    {
        Assert.Equal(new Rect(150, 130, 400, 300), SelectionMath.Move(Box, new Vector(50, 30), Screen));
        Assert.Equal(new Rect(1520, 780, 400, 300), SelectionMath.Move(Box, new Vector(5000, 5000), Screen));
        Assert.Equal(new Rect(0, 0, 400, 300), SelectionMath.Move(Box, new Vector(-5000, -5000), Screen));
    }

    [Fact]
    public void Toolbar_GoesBelowThenAboveThenInside()
    {
        var toolbar = new Size(300, 40);
        Assert.Equal(new Point(150, 410), SelectionMath.ToolbarPosition(Box, toolbar, Screen, 10));

        var low = new Rect(100, 900, 400, 160);
        Assert.Equal(new Point(150, 850), SelectionMath.ToolbarPosition(low, toolbar, Screen, 10));

        var full = new Rect(0, 0, 1920, 1080);
        Assert.Equal(new Point(810, 1030), SelectionMath.ToolbarPosition(full, toolbar, Screen, 10));
    }

    [Fact]
    public void Toolbar_StaysOnScreenNearEdges()
    {
        var corner = new Rect(1800, 100, 100, 100);
        Assert.Equal(1610, SelectionMath.ToolbarPosition(corner, new Size(300, 40), Screen, 10).X);
    }

    [Fact]
    public void Snap_PicksDeepestLargeEnoughWindow()
    {
        var frame = new Rect(0, 0, 1200, 900);
        var content = new Rect(0, 80, 1200, 820);
        var tinyButton = new Rect(10, 10, 32, 32);

        Assert.Equal(content, WindowSnap.Pick(new[] { frame, content }, WindowSnap.MinimumChild));
        Assert.Equal(content, WindowSnap.Pick(new[] { frame, content, tinyButton }, WindowSnap.MinimumChild));
        Assert.Equal(frame, WindowSnap.Pick(new[] { frame, tinyButton }, WindowSnap.MinimumChild));
        Assert.Equal(frame, WindowSnap.Pick(new[] { frame }, WindowSnap.MinimumChild));
    }

    [Theory]
    [InlineData("Right", "50장 · 2초 · → 키")]
    [InlineData("Space", "50장 · 2초 · Space 키")]
    [InlineData("", "50장 · 2초 · 넘김 없음")]
    public void Summary_DescribesCurrentSettings(string key, string expected)
    {
        var settings = Mock.Of<ICaptureSettingsViewModel>(s =>
            s.CaptureCount == 50 && s.IntervalSeconds == 2 && s.AutoInputKey == key);

        Assert.Equal(expected, AreaSelectionViewModel.Summarize(settings));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestStart_ReportsWhetherHandlerStarted(bool started)
    {
        var viewModel = new AreaSelectionViewModel(Mock.Of<ILogger>(), _ => Task.FromResult<ImageSource?>(null));
        viewModel.StartRequested += (_, e) => e.Started = started;

        Assert.Equal(started, viewModel.RequestStart());
    }

    [Fact]
    public void RequestStart_WithoutHandler_DoesNotStart()
    {
        var viewModel = new AreaSelectionViewModel(Mock.Of<ILogger>(), _ => Task.FromResult<ImageSource?>(null));

        Assert.False(viewModel.RequestStart());
    }
}

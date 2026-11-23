using AutoCapture.Wpf.Views;
using System.Windows;

namespace AutoCapture.Tests.Views;

public class AreaSelectionTests
{
    [Theory]
    [InlineData(0, 1.0, 10, 40)]
    [InlineData(-1920, 1.0, -1910, 40)]
    [InlineData(-1920, 1.5, -1905, 60)]
    public void ReverseDrag_UsesPhysicalCorners(int origin, double scale, int left, int width)
    {
        var bounds = AreaSelectionWindow.ScreenBounds(new Point(50, 30), new Point(10, 10),
            p => new Point(origin + p.X * scale, p.Y * scale));
        Assert.Equal(new Rect(left, 10 * scale, width, 20 * scale), bounds);
    }
}

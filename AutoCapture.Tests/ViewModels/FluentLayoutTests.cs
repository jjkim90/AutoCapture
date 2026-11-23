using AutoCapture.Wpf.Controls;
using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using Moq;
using Serilog;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoCapture.Tests.ViewModels
{
    /// <summary>Fluent 한 화면 재설계에서 추가한 표시용 로직</summary>
    [Collection(MessengerCollection.Name)]
    public class FluentLayoutTests
    {
        [Theory]
        [InlineData(1, 5, 2)]       // 시작 대기만
        [InlineData(10, 5, 59)]     // 2 + 9 × 6.25 = 58.25
        [InlineData(50, 2, 125)]    // 2 + 49 × 2.5 = 124.5
        [InlineData(3, 0, 0)]
        [InlineData(0, 5, 0)]
        public void EstimateDuration_FollowsEngineTiming(int count, int interval, int seconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(seconds), CaptureSettingsViewModel.EstimateDuration(count, interval));
        }

        [Theory]
        [InlineData(59, "59초")]
        [InlineData(60, "1분")]
        [InlineData(125, "2분 5초")]
        [InlineData(3600, "1시간")]
        [InlineData(37427, "10시간 23분")]
        public void FormatDuration_UsesKoreanUnits(int seconds, string expected)
        {
            Assert.Equal(expected, CaptureSettingsViewModel.FormatDuration(TimeSpan.FromSeconds(seconds)));
        }

        [Theory]
        [InlineData("42", 10, 42)]
        [InlineData(" 7 ", 10, 7)]
        [InlineData("0", 10, 1)]       // 최솟값으로 맞춤
        [InlineData("9999", 10, 500)]  // 최댓값으로 맞춤
        [InlineData("abc", 10, 10)]    // 숫자가 아니면 유지
        [InlineData("", 10, 10)]
        [InlineData(null, 10, 10)]
        public void NumberStepper_Parse_ClampsToRange(string? text, int current, int expected)
        {
            Assert.Equal(expected, NumberStepper.Parse(text, current, 1, 500));
        }

        [Theory]
        [InlineData("Space", "Space")]
        [InlineData("Right", "Right")]
        [InlineData("", "None")]
        [InlineData(null, "None")]
        [InlineData("PageDown", "Custom")]
        [InlineData("Ctrl+Right", "Custom")]
        public void KeySelection_MapsStoredKeyToSegment(string? key, string segment)
        {
            Assert.Equal(segment, KeySelectionControl.SegmentOf(key).ToString());
        }

        private static ImageSource Pixel()
        {
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            image.Freeze();
            return image;
        }

        [Fact]
        public async Task ApplySelection_SetsAreaTargetAndPreview()
        {
            var preview = Pixel();
            var target = Mock.Of<ICaptureTarget>(t => t.Title == "뷰어");
            var viewModel = new AreaSelectionViewModel(Mock.Of<ILogger>(), _ => Task.FromResult<ImageSource?>(preview));

            await viewModel.ApplySelectionAsync(new CaptureArea(10, 20, 300, 200), _ => target);

            Assert.True(viewModel.HasSelection);
            Assert.Equal(300, viewModel.Width);
            Assert.Same(target, viewModel.Target);
            Assert.Same(preview, viewModel.Preview);
        }

        [Fact]
        public async Task ApplySelection_KeepsSelectionWhenPreviewFails()
        {
            var viewModel = new AreaSelectionViewModel(Mock.Of<ILogger>(),
                _ => Task.FromException<ImageSource?>(new System.ComponentModel.Win32Exception(5)));

            await viewModel.ApplySelectionAsync(new CaptureArea(0, 0, 100, 100), _ => null);

            Assert.True(viewModel.HasSelection);
            Assert.Null(viewModel.Preview);
        }

        [Fact]
        public async Task ClearSelection_RemovesPreview()
        {
            var viewModel = new AreaSelectionViewModel(Mock.Of<ILogger>(), _ => Task.FromResult<ImageSource?>(Pixel()));
            await viewModel.ApplySelectionAsync(new CaptureArea(0, 0, 100, 100), _ => null);

            viewModel.ClearSelectionCommand.Execute(null);

            Assert.False(viewModel.HasSelection);
            Assert.Null(viewModel.Preview);
        }

        [Fact]
        public void MacroControl_ExposesActiveStateAndTotal()
        {
            var engine = new Mock<IMacroEngine>();
            var viewModel = new MacroControlViewModel(Mock.Of<ILogger>(), Mock.Of<ICaptureSettingsViewModel>(),
                Mock.Of<IAreaSelectionViewModel>(), engine.Object, (_, _) => { });
            var changed = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            engine.Raise(x => x.ProgressChanged += null, new MacroProgressEventArgs { CurrentCount = 3, TotalCount = 8 });
            Assert.Equal(8, viewModel.TotalCaptureCount);
            Assert.Contains(nameof(MacroControlViewModel.TotalCaptureCount), changed);

            Assert.False(viewModel.IsActive);
            viewModel.MacroState.Status = MacroStatus.Paused;
            Assert.True(viewModel.IsActive);
            Assert.False(viewModel.IsRunning);

            changed.Clear();
            engine.Raise(x => x.Error += null, new MacroErrorEventArgs(new IOException("디스크")));
            Assert.False(viewModel.IsActive);
            Assert.Contains(nameof(MacroControlViewModel.IsActive), changed);
        }
    }
}

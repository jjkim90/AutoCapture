using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Models.Messages;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Views;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Serilog;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AutoCapture.Tests.ViewModels
{
    [Collection(MessengerCollection.Name)]
    public class CaptureHudViewModelTests : IDisposable
    {
        private readonly Mock<IMacroEngine> _engine = new();
        private readonly Mock<ICaptureSettingsViewModel> _settings = new();
        private readonly Mock<IAreaSelectionViewModel> _area = new();
        private readonly Mock<IMacroControlViewModel> _control = new();
        private readonly Mock<ICommand> _stop = new();
        private readonly Mock<ICaptureTarget> _target = new();
        private readonly List<string> _thumbnails = new();
        private readonly List<string> _opened = new();
        private readonly ImageSource _image;
        private readonly CaptureHudViewModel _hud;

        public CaptureHudViewModelTests()
        {
            var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            image.Freeze();
            _image = image;

            _settings.SetupGet(s => s.CaptureCount).Returns(10);
            _settings.SetupGet(s => s.IntervalSeconds).Returns(4);
            _settings.SetupGet(s => s.SaveFolderPath).Returns(@"C:\captures");
            _settings.SetupGet(s => s.AutoInputKey).Returns("Right");
            _target.SetupGet(t => t.IsOpen).Returns(true);
            _area.SetupGet(a => a.Target).Returns(_target.Object);
            _stop.Setup(c => c.CanExecute(null)).Returns(true);
            _control.SetupGet(c => c.StopMacroCommand).Returns(_stop.Object);

            _hud = new CaptureHudViewModel(_engine.Object, _settings.Object, _area.Object, _control.Object,
                Mock.Of<ILogger>(),
                path => { _thumbnails.Add(path); return Task.FromResult<ImageSource?>(_image); },
                path => _opened.Add(path));
        }

        public void Dispose() => _hud.Dispose();

        private static void Status(MacroStatus status) =>
            WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(status));

        private void Progress(int current, int total = 10, string? file = null) =>
            _engine.Raise(e => e.ProgressChanged += null,
                new MacroProgressEventArgs { CurrentCount = current, TotalCount = total, FilePath = file });

        [Fact]
        public void Start_ShowsRunningPillWithEstimate()
        {
            Status(MacroStatus.Running);

            Assert.Equal(HudState.Running, _hud.State);
            Assert.Equal(10, _hud.Total);
            Assert.Equal(0, _hud.Current);
            Assert.Equal("약 50초 남음", _hud.RemainingText); // 10장 × 4초 × 1.25
            Assert.True(_hud.PauseCommand.CanExecute(null));
            Assert.False(_hud.ResumeCommand.CanExecute(null));
        }

        [Fact]
        public void Progress_UpdatesRingTimeAndThumbnail()
        {
            Status(MacroStatus.Running);

            Progress(4, file: @"C:\captures\p4.png");

            Assert.Equal(4, _hud.Current);
            Assert.Equal(0.4, _hud.Progress, 3);
            Assert.Equal("약 30초 남음", _hud.RemainingText);
            Assert.Equal(@"C:\captures\p4.png", Assert.Single(_thumbnails));
            Assert.Same(_image, _hud.LastThumbnail);
        }

        [Fact]
        public void ResumeMessage_DoesNotResetProgress()
        {
            Status(MacroStatus.Running);
            Progress(6);
            _engine.Raise(e => e.PauseChanged += null, _engine.Object, true);
            Assert.Equal(HudState.Paused, _hud.State);

            _engine.Raise(e => e.PauseChanged += null, _engine.Object, false);
            Status(MacroStatus.Running);

            Assert.Equal(HudState.Running, _hud.State);
            Assert.Equal(6, _hud.Current);
        }

        [Fact]
        public async Task Resume_BringsBackTargetBeforeResuming()
        {
            Status(MacroStatus.Running);
            _engine.Raise(e => e.PauseChanged += null, _engine.Object, true);
            var order = new List<string>();
            _target.SetupGet(t => t.IsForeground).Returns(false);
            _target.Setup(t => t.Activate()).Callback(() => order.Add("activate"));
            _engine.Setup(e => e.ResumeAsync()).Callback(() => order.Add("resume")).Returns(Task.CompletedTask);

            await _hud.ResumeCommand.ExecuteAsync(null);

            Assert.Equal(new[] { "activate", "resume" }, order);
        }

        [Fact]
        public async Task Pause_AsksEngine()
        {
            Status(MacroStatus.Running);

            await _hud.PauseCommand.ExecuteAsync(null);

            _engine.Verify(e => e.PauseAsync(), Times.Once);
        }

        [Fact]
        public void Stop_UsesMacroControlCommand()
        {
            Status(MacroStatus.Running);

            _hud.StopCommand.Execute(null);

            _stop.Verify(c => c.Execute(null), Times.Once);
        }

        [Fact]
        public void Completed_ShowsResultCardAndOpensOutputs()
        {
            Status(MacroStatus.Running);
            _engine.Raise(e => e.Completed += null, new MacroCompletedEventArgs
            {
                TotalCaptured = 10,
                ElapsedTime = TimeSpan.FromSeconds(102),
                PdfPath = @"C:\captures\book.pdf",
                CapturedFiles = new List<string> { @"C:\captures\p10.png" }
            });

            Assert.Equal(HudState.Completed, _hud.State);
            Assert.Equal("10장 저장 완료", _hud.CompletedTitle);
            Assert.Equal("PNG 10 · PDF 1 · 1분 42초", _hud.CompletedDetail);
            Assert.True(_hud.CanOpenPdf);
            Assert.Same(_image, _hud.LastThumbnail);

            _hud.OpenFolderCommand.Execute(null);
            _hud.OpenPdfCommand.Execute(null);
            Assert.Equal(new[] { @"C:\captures", @"C:\captures\book.pdf" }, _opened);

            _hud.CloseCommand.Execute(null);
            Assert.Equal(HudState.Hidden, _hud.State);
        }

        [Fact]
        public void CompletedWithoutPdf_HidesPdfAction()
        {
            Status(MacroStatus.Running);
            _engine.Raise(e => e.Completed += null, new MacroCompletedEventArgs
            {
                TotalCaptured = 3, ElapsedTime = TimeSpan.FromSeconds(9)
            });

            Assert.False(_hud.CanOpenPdf);
            Assert.Equal("PNG 3 · 9초", _hud.CompletedDetail);
        }

        [Fact]
        public void CancelStopOrError_HidesHud()
        {
            Status(MacroStatus.Running);
            _engine.Raise(e => e.Completed += null, new MacroCompletedEventArgs { WasCancelled = true });
            Assert.Equal(HudState.Hidden, _hud.State);

            Status(MacroStatus.Running);
            Status(MacroStatus.Idle);
            Assert.Equal(HudState.Hidden, _hud.State);

            Status(MacroStatus.Running);
            _engine.Raise(e => e.Error += null, new MacroErrorEventArgs(new IOException("디스크")));
            Assert.Equal(HudState.Hidden, _hud.State);
        }

        [Fact]
        public void CompletedCard_IgnoresLaterIdle()
        {
            Status(MacroStatus.Running);
            _engine.Raise(e => e.Completed += null, new MacroCompletedEventArgs { TotalCaptured = 1 });

            Status(MacroStatus.Idle);

            Assert.Equal(HudState.Completed, _hud.State);
        }

        [Theory]
        // 작업 영역 1920×1040, HUD 300×60, 여백 8
        [InlineData(400, 200, 1000, 700, 810, 8)]       // 위쪽이 비어 있으면 위
        [InlineData(400, 0, 1000, 600, 810, 972)]       // 위쪽을 가리면 아래
        [InlineData(0, 0, 1920, 1040, 810, 8)]          // 둘 다 가리면 위
        public void Placement_AvoidsCaptureArea(int x, int y, int w, int h, double left, double top)
        {
            var position = HudPlacement.Compute(new Rect(0, 0, 1920, 1040), new Rect(x, y, w, h), new Size(300, 60), 8);

            Assert.Equal(new Point(left, top), position);
        }

        [Fact]
        public void Placement_UsesMonitorOffset()
        {
            var position = HudPlacement.Compute(new Rect(-1920, 0, 1920, 1040), new Rect(-1500, 300, 800, 600),
                new Size(300, 60), 8);

            Assert.Equal(new Point(-1110, 8), position);
        }
    }
}

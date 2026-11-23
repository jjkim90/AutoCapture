using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using Moq;
using Serilog;

namespace AutoCapture.Tests.ViewModels
{
    /// <summary>오버레이 툴바의 바로 시작</summary>
    [Collection(MessengerCollection.Name)]
    public class OverlayStartTests
    {
        private readonly Mock<IMacroEngine> _engine = new();
        private readonly Mock<IAreaSelectionViewModel> _area = new();
        private readonly Mock<ICaptureSettingsViewModel> _settings = new();
        private readonly Mock<ICaptureTarget> _target = new();
        private readonly List<string> _order = new();
        private readonly MacroControlViewModel _control;

        public OverlayStartTests()
        {
            _target.SetupGet(t => t.IsOpen).Returns(true);
            _target.Setup(t => t.Activate()).Callback(() => _order.Add("activate"));
            _area.SetupGet(a => a.Target).Returns(_target.Object);
            _area.SetupGet(a => a.HasSelection).Returns(true);
            _area.SetupGet(a => a.Width).Returns(100);
            _area.SetupGet(a => a.Height).Returns(100);
            _settings.SetupGet(s => s.CaptureCount).Returns(3);
            _settings.SetupGet(s => s.IntervalSeconds).Returns(1);
            _settings.SetupGet(s => s.AutoInputKey).Returns("Right");
            _settings.Setup(s => s.Validate()).Returns(true);
            _engine.Setup(e => e.StartAsync(It.IsAny<CaptureSettings>(), It.IsAny<CaptureArea>(), It.IsAny<CancellationToken>()))
                .Callback(() => _order.Add("start"))
                .Returns(Task.CompletedTask);
            _control = new MacroControlViewModel(Mock.Of<ILogger>(), _settings.Object, _area.Object, _engine.Object, (_, _) => { });
        }

        private StartRequestedEventArgs Request()
        {
            var args = new StartRequestedEventArgs();
            _area.Raise(a => a.StartRequested += null, _area.Object, args);
            return args;
        }

        [Fact]
        public void StartNow_ActivatesTargetThenStarts()
        {
            var args = Request();

            Assert.True(args.Started);
            Assert.Equal(new[] { "activate", "start" }, _order);
        }

        [Fact]
        public void StartNow_WithoutKey_DoesNotTouchTarget()
        {
            _settings.SetupGet(s => s.AutoInputKey).Returns("");

            var args = Request();

            Assert.True(args.Started);
            Assert.Equal(new[] { "start" }, _order);
        }

        [Fact]
        public void StartNow_WithoutFolder_ExplainsAndStaysIdle()
        {
            _settings.Setup(s => s.Validate()).Returns(false);

            var args = Request();

            Assert.False(args.Started);
            Assert.Empty(_order);
            Assert.Equal("저장 폴더를 선택한 뒤 시작하세요", _control.StatusMessage);
        }

        [Fact]
        public void StartNow_WithoutTarget_AsksToReselect()
        {
            _area.SetupGet(a => a.Target).Returns((ICaptureTarget?)null);

            var args = Request();

            Assert.False(args.Started);
            Assert.StartsWith("영역 중앙에서 입력 대상 창을 찾지 못했습니다", _control.StatusMessage);
        }
    }
}

using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;

namespace AutoCapture.Tests.Services
{
    public class MacroEngineTests
    {
        private readonly Mock<IScreenCaptureService> _mockScreenCaptureService;
        private readonly Mock<IInputService> _mockInputService;
        private readonly Mock<ILogger> _mockLogger;
        private readonly MacroEngine _macroEngine;

        public MacroEngineTests()
        {
            _mockScreenCaptureService = new Mock<IScreenCaptureService>();
            _mockInputService = new Mock<IInputService>();
            _mockLogger = new Mock<ILogger>();
            _macroEngine = new MacroEngine(
                _mockLogger.Object,
                _mockScreenCaptureService.Object,
                _mockInputService.Object);
        }

        [Fact]
        public async Task StartAsync_ValidSettings_CompletesSuccessfully()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 3,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "Right",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((CaptureArea a, string path, string prefix) => $"{path}\\{prefix}_test.png");

            var completedEventRaised = false;
            _macroEngine.Completed += (sender, args) =>
            {
                completedEventRaised = true;
                Assert.Equal(3, args.TotalCaptured);
            };

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            Assert.True(completedEventRaised);
            _mockScreenCaptureService.Verify(
                x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Exactly(3));
            _mockInputService.Verify(
                x => x.SendKeyWithRandomTimingAsync("Right"), 
                Times.Exactly(3));
        }

        [Fact]
        public async Task StartAsync_WithPdfSave_CallsPdfSave()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 2,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = true,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((CaptureArea a, string path, string prefix) => $"{path}\\{prefix}_test.png");

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            _mockScreenCaptureService.Verify(
                x => x.SaveAsPdfAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Once);
        }

        [Fact]
        public async Task StopAsync_WhileRunning_StopsMacro()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 10,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "Right",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            var captureCount = 0;
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("test.png")
                .Callback(() => captureCount++);

            // Act
            var executeTask = _macroEngine.StartAsync(settings, area);
            
            // 일부 캡처가 실행될 때까지 대기
            await Task.Delay(500);
            await _macroEngine.StopAsync();
            await executeTask;

            // Assert
            Assert.True(captureCount < 10, $"Expected less than 10 captures, but got {captureCount}");
            Assert.False(_macroEngine.IsRunning);
        }

        [Fact]
        public async Task StartAsync_ProgressEvent_FiredCorrectly()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 3,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            var progressEvents = new List<MacroProgressEventArgs>();
            _macroEngine.ProgressChanged += (sender, args) => progressEvents.Add(args);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("test.png");

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            Assert.Equal(3, progressEvents.Count);
            Assert.Equal(1, progressEvents[0].CurrentCount);
            Assert.Equal(2, progressEvents[1].CurrentCount);
            Assert.Equal(3, progressEvents[2].CurrentCount);
            Assert.All(progressEvents, e => Assert.Equal(3, e.TotalCount));
        }

        [Fact]
        public async Task StartAsync_CaptureThrowsException_HandlesGracefully()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 3,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Capture failed"));

            Exception? capturedException = null;
            _macroEngine.Completed += (sender, args) => { };
            _macroEngine.Error += (sender, args) => capturedException = args.Exception;

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            Assert.NotNull(capturedException);
            Assert.Equal("Capture failed", capturedException.Message);
            // Serilog의 구조화된 로깅은 복잡한 제네릭 시그니처를 사용하므로
            // 단순히 Error 메서드가 호출되었는지만 확인
            Assert.Contains(_mockLogger.Invocations, i => i.Method.Name == "Error");
        }

        [Fact]
        public async Task IsRunning_ReflectsCurrentState()
        {
            // Arrange & Act & Assert
            Assert.False(_macroEngine.IsRunning);
            
            var settings = new CaptureSettings
            {
                CaptureCount = 1,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("test.png");

            var executeTask = _macroEngine.StartAsync(settings, area);
            Assert.True(_macroEngine.IsRunning);
            
            await executeTask;
            Assert.False(_macroEngine.IsRunning);
        }

        [Fact]
        public async Task StartAsync_NoAutoInputKey_SkipsKeyInput()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                CaptureCount = 2,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "", // 빈 문자열
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("test.png");

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            _mockInputService.Verify(
                x => x.SendKeyWithRandomTimingAsync(It.IsAny<string>()), 
                Times.Never);
        }
    }
}
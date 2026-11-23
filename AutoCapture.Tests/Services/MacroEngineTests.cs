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
        private readonly Mock<ICaptureTarget> _target = new();
        private readonly MacroEngine _macroEngine;

        public MacroEngineTests()
        {
            _target.SetupGet(t => t.IsOpen).Returns(true);
            _target.SetupGet(t => t.IsForeground).Returns(true);
            _mockScreenCaptureService = new Mock<IScreenCaptureService>();
            _mockInputService = new Mock<IInputService>();
            _mockLogger = new Mock<ILogger>();
            _macroEngine = new MacroEngine(
                _mockLogger.Object,
                _mockScreenCaptureService.Object,
                _mockInputService.Object, async (_, token) =>
                {
                    await Task.Yield();
                    token.ThrowIfCancellationRequested();
                });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FocusLoss_WaitsAtCurrentBoundary(bool afterSave)
        {
            bool focused = afterSave;
            _target.SetupGet(t => t.IsForeground).Returns(() => Volatile.Read(ref focused));
            var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _macroEngine.PauseChanged += (_, waiting) => { if (waiting) paused.TrySetResult(); };
            int saved = 0;
            _mockScreenCaptureService.Setup(s => s.CaptureAndSaveAsync(It.IsAny<CaptureArea>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync(() =>
                {
                    if (Interlocked.Increment(ref saved) == 1 && afterSave)
                        Volatile.Write(ref focused, false);
                    return "page.png";
                });
            var task = _macroEngine.StartAsync(new CaptureSettings
            {
                Target = _target.Object, CaptureCount = 2, IntervalSeconds = 1
            }, new CaptureArea(0, 0, 10, 10));
            try
            {
                await paused.Task.WaitAsync(TimeSpan.FromSeconds(6));
                Assert.Equal(afterSave ? 1 : 0, saved);
                _mockInputService.VerifyNoOtherCalls();
                Volatile.Write(ref focused, true);
                await _macroEngine.ResumeAsync();
                await task.WaitAsync(TimeSpan.FromSeconds(8));
                Assert.Equal(2, saved);
                _mockInputService.Verify(s => s.SendKeyWithRandomTimingAsync("Space", It.IsAny<Func<Task>>()), Times.Once);
            }
            finally { await _macroEngine.StopAsync(); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task PausedRun_ExitsOnStopOrClosedTarget(bool close)
        {
            _target.SetupGet(t => t.IsForeground).Returns(false);
            var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _macroEngine.PauseChanged += (_, waiting) => { if (waiting) paused.TrySetResult(); };
            MacroErrorEventArgs? error = null;
            _macroEngine.Error += (_, e) => error = e;
            var task = _macroEngine.StartAsync(new CaptureSettings { Target = _target.Object }, new CaptureArea(0, 0, 10, 10));
            try
            {
                await paused.Task.WaitAsync(TimeSpan.FromSeconds(6));
                if (close) _target.SetupGet(t => t.IsOpen).Returns(false);
                else await _macroEngine.StopAsync();
                await task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Equal(close, error != null);
                _mockInputService.VerifyNoOtherCalls();
                _mockScreenCaptureService.VerifyNoOtherCalls();
            }
            finally { await _macroEngine.StopAsync(); }
        }

        [Fact]
        public async Task ImmediateFocusLoss_RegistersRunBeforeFirstPause()
        {
            _target.SetupGet(t => t.IsForeground).Returns(false);
            var engine = new MacroEngine(_mockLogger.Object, _mockScreenCaptureService.Object,
                _mockInputService.Object, (_, _) => Task.CompletedTask);
            var settings = new CaptureSettings { Target = _target.Object };
            var area = new CaptureArea(0, 0, 1, 1);
            var run = engine.StartAsync(settings, area);
            Assert.True(engine.IsPaused);
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.StartAsync(settings, area));
            await engine.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            await run;
            Assert.False(engine.IsPaused);
            Assert.False(engine.IsRunning);
        }

        [Fact]
        public async Task FailedSecondSave_PreservesFirstPageAndStopsBeforeNextInput()
        {
            var settings = new CaptureSettings { Target = _target.Object, CaptureCount = 3, IntervalSeconds = 1, AutoInputKey = "Right", SaveAsPdf = true };
            _mockScreenCaptureService.SetupSequence(s => s.CaptureAndSaveAsync(
                It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync("first.png").ReturnsAsync((string?)null);
            MacroErrorEventArgs? error = null;
            _macroEngine.Error += (_, e) => error = e;
            await _macroEngine.StartAsync(settings, new CaptureArea(0, 0, 10, 10));
            Assert.Contains("요청 3장, 시도 2장, 저장 1장", error?.Message);
            _mockInputService.Verify(s => s.SendKeyWithRandomTimingAsync("Right", It.IsAny<Func<Task>>()), Times.Once);
            _mockScreenCaptureService.Verify(s => s.SaveAsPdfAsync(
                It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SinglePage_SavesWithoutAdvancing()
        {
            _mockScreenCaptureService.Setup(s => s.CaptureAndSaveAsync(
                It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>())).ReturnsAsync("one.png");
            await _macroEngine.StartAsync(new CaptureSettings { Target = _target.Object, CaptureCount = 1 }, new CaptureArea(0, 0, 10, 10));
            _mockInputService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task StartAsync_ValidSettings_CompletesSuccessfully()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                Target = _target.Object,
                CaptureCount = 3,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "Right",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync((CaptureArea a, string path, string prefix, Func<Task> guard) => $"{path}\\{prefix}_test.png");

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
                x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()),
                Times.Exactly(3));
            _mockInputService.Verify(
                x => x.SendKeyWithRandomTimingAsync("Right", It.IsAny<Func<Task>>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task StartAsync_WithPdfSave_CallsPdfSave()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                Target = _target.Object,
                CaptureCount = 2,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = true,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync((CaptureArea a, string path, string prefix, Func<Task> guard) => $"{path}\\{prefix}_test.png");

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            _mockScreenCaptureService.Verify(
                x => x.SaveAsPdfAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>()), 
                Times.Once);
        }

        [Fact]
        public async Task PdfFailure_ReportsSavedCountAndNeverSignalsSuccess()
        {
            var settings = new CaptureSettings { CaptureCount = 1, AutoInputKey = "", SaveAsPdf = true };
            _mockScreenCaptureService.Setup(s => s.CaptureAndSaveAsync(It.IsAny<CaptureArea>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>())).ReturnsAsync("page.png");
            _mockScreenCaptureService.Setup(s => s.SaveAsPdfAsync(It.IsAny<List<string>>(),
                It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IOException("PDF write failed"));
            MacroErrorEventArgs? error = null;
            bool completed = false;
            _macroEngine.Error += (_, e) => error = e;
            _macroEngine.Completed += (_, _) => completed = true;
            await _macroEngine.StartAsync(settings, new CaptureArea(0, 0, 10, 10));
            Assert.False(completed);
            Assert.Contains("PDF 생성 실패 · 요청 1장, 시도 1장, 저장 1장", error?.Message);
        }

        [Fact]
        public async Task StopAsync_WhileRunning_StopsMacro()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                Target = _target.Object,
                CaptureCount = 10,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "Right",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            var captureCount = 0;
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .Returns(() =>
                {
                    captureCount++;
                    started.TrySetResult();
                    return release.Task;
                });

            // Act
            var executeTask = _macroEngine.StartAsync(settings, area);
            
            await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            MacroCompletedEventArgs? result = null;
            _macroEngine.Completed += (_, e) => result = e;
            var stopping = _macroEngine.StopAsync();
            release.SetResult("test.png");
            await stopping;
            await executeTask;

            // Assert
            Assert.Equal(1, captureCount);
            Assert.NotNull(result);
            Assert.True(result.WasCancelled);
            Assert.Equal(1, result.TotalAttempted);
            Assert.Equal(1, result.TotalCaptured);
            _mockInputService.VerifyNoOtherCalls();
            Assert.False(_macroEngine.IsRunning);
        }

        [Fact]
        public async Task StartAsync_ProgressEvent_FiredCorrectly()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                Target = _target.Object,
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
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
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
                Target = _target.Object,
                CaptureCount = 3,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ThrowsAsync(new Exception("Capture failed"));

            Exception? capturedException = null;
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
                Target = _target.Object,
                CaptureCount = 1,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "",
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            var saved = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .Returns(saved.Task);

            var executeTask = _macroEngine.StartAsync(settings, area);
            Assert.True(_macroEngine.IsRunning);
            saved.SetResult("test.png");
            await executeTask;
            Assert.False(_macroEngine.IsRunning);
        }

        [Fact]
        public async Task StartAsync_NoAutoInputKey_SkipsKeyInput()
        {
            // Arrange
            var settings = new CaptureSettings
            {
                Target = _target.Object,
                CaptureCount = 2,
                IntervalSeconds = 1,
                SaveFolderPath = @"C:\temp",
                AutoInputKey = "", // 빈 문자열
                SaveAsPdf = false,
                FileNamePrefix = "test"
            };
            var area = new CaptureArea(0, 0, 100, 100);
            
            _mockScreenCaptureService
                .Setup(x => x.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync("test.png");

            // Act
            await _macroEngine.StartAsync(settings, area);

            // Assert
            _mockInputService.Verify(
                x => x.SendKeyWithRandomTimingAsync(It.IsAny<string>(), It.IsAny<Func<Task>>()),
                Times.Never);
        }
    }
}

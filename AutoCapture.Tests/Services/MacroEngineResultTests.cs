using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;

namespace AutoCapture.Tests.Services
{
    /// <summary>HUD가 쓰는 엔진 결과 정보(저장 파일, PDF 경로)</summary>
    public class MacroEngineResultTests
    {
        private readonly Mock<IScreenCaptureService> _capture = new();
        private readonly MacroEngine _engine;
        private int _page;

        public MacroEngineResultTests()
        {
            _capture.Setup(c => c.CaptureAndSaveAsync(It.IsAny<CaptureArea>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<Func<Task>>()))
                .ReturnsAsync(() => $@"C:\out\p{Interlocked.Increment(ref _page)}.png");
            _capture.Setup(c => c.SaveAsPdfAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(@"C:\out\book.pdf");
            _engine = new MacroEngine(Mock.Of<ILogger>(), _capture.Object, Mock.Of<IInputService>(),
                (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        }

        private static CaptureSettings Settings(bool pdf) => new()
        {
            CaptureCount = 3, IntervalSeconds = 1, SaveFolderPath = @"C:\out", AutoInputKey = "", SaveAsPdf = pdf
        };

        [Fact]
        public async Task Progress_CarriesSavedFile()
        {
            var files = new List<string?>();
            _engine.ProgressChanged += (_, e) => files.Add(e.FilePath);

            await _engine.StartAsync(Settings(false), new CaptureArea(0, 0, 10, 10));

            Assert.Equal(new[] { @"C:\out\p1.png", @"C:\out\p2.png", @"C:\out\p3.png" }, files);
        }

        [Theory]
        [InlineData(true, @"C:\out\book.pdf")]
        [InlineData(false, null)]
        public async Task Completed_CarriesPdfPathOnlyWhenMade(bool pdf, string? expected)
        {
            MacroCompletedEventArgs? result = null;
            _engine.Completed += (_, e) => result = e;

            await _engine.StartAsync(Settings(pdf), new CaptureArea(0, 0, 10, 10));

            Assert.NotNull(result);
            Assert.Equal(expected, result.PdfPath);
            Assert.Equal(3, result.TotalCaptured);
        }
    }
}

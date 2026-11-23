using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using Moq;
using Serilog;

namespace AutoCapture.Tests.ViewModels
{
    /// <summary>완료를 한눈에 알 수 있게 하는 결과 요약과 메인 화면 배너</summary>
    [Collection(MessengerCollection.Name)]
    public class CompletionResultTests
    {
        private readonly Mock<IMacroEngine> _engine = new();
        private readonly Mock<ICaptureSettingsViewModel> _settings = new();
        private readonly Mock<IAreaSelectionViewModel> _area = new();
        private readonly List<string> _opened = new();
        private readonly MacroControlViewModel _control;

        public CompletionResultTests()
        {
            _settings.SetupGet(s => s.SaveFolderPath).Returns(@"C:\settings-folder");
            _control = new MacroControlViewModel(Mock.Of<ILogger>(), _settings.Object, _area.Object, _engine.Object,
                (_, _) => { }, path => _opened.Add(path));
        }

        private static MacroCompletedEventArgs Done(string? pdf = @"C:\out\book.pdf") => new()
        {
            TotalCaptured = 10,
            ElapsedTime = TimeSpan.FromSeconds(25),
            PdfPath = pdf,
            CapturedFiles = new List<string> { @"C:\out\p1.png", @"C:\out\p10.png" }
        };

        [Fact]
        public void Summary_UsesSavedFolderAndReadableDetail()
        {
            var summary = CompletionSummary.From(Done(), @"C:\settings-folder");

            Assert.Equal("10장 저장 완료", summary.Title);
            Assert.Equal("PNG 10 · PDF 1 · 25초", summary.Detail);
            Assert.Equal(@"C:\out", summary.Folder);
            Assert.True(summary.HasPdf);
        }

        [Fact]
        public void Summary_WithoutFilesFallsBackToSettingsFolder()
        {
            var summary = CompletionSummary.From(new MacroCompletedEventArgs { TotalCaptured = 0 }, @"C:\settings-folder");

            Assert.Equal(@"C:\settings-folder", summary.Folder);
            Assert.False(summary.HasPdf);
            Assert.Equal("PNG 0 · 0초", summary.Detail);
        }

        [Fact]
        public void Completed_ShowsBannerThatOpensResults()
        {
            _engine.Raise(e => e.Completed += null, Done());

            Assert.True(_control.HasResult);
            Assert.Equal("10장 저장 완료", _control.LastResult!.Title);

            _control.OpenResultFolderCommand.Execute(null);
            _control.OpenResultPdfCommand.Execute(null);
            Assert.Equal(new[] { @"C:\out", @"C:\out\book.pdf" }, _opened);

            _control.DismissResultCommand.Execute(null);
            Assert.False(_control.HasResult);
        }

        [Fact]
        public void CancelledRun_ShowsNoBanner()
        {
            _engine.Raise(e => e.Completed += null, new MacroCompletedEventArgs { WasCancelled = true, StopLocation = "3장 저장" });

            Assert.False(_control.HasResult);
        }

        [Fact]
        public void NextStart_ClearsPreviousBanner()
        {
            _engine.Raise(e => e.Completed += null, Done());
            _area.SetupGet(a => a.HasSelection).Returns(true);
            _area.SetupGet(a => a.Width).Returns(10);
            _area.SetupGet(a => a.Height).Returns(10);
            _settings.SetupGet(s => s.AutoInputKey).Returns("");
            _settings.Setup(s => s.Validate()).Returns(true);
            _engine.Setup(e => e.StartAsync(It.IsAny<CaptureSettings>(), It.IsAny<CaptureArea>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _control.MacroState.Status = MacroStatus.Completed;

            _control.StartMacroCommand.Execute(null);

            Assert.False(_control.HasResult);
        }
    }
}

using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using Moq;
using Serilog;
using System.Windows;

namespace AutoCapture.Tests.ViewModels
{
    [Collection(MessengerCollection.Name)]
    public class MacroControlViewModelTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IMacroEngine> _mockMacroEngine;
        private readonly Mock<IAreaSelectionViewModel> _mockAreaSelection;
        private readonly Mock<ICaptureSettingsViewModel> _mockCaptureSettings;
        private readonly MacroControlViewModel _viewModel;

        private readonly List<(string Message, bool Error)> _notifications = new();

        public MacroControlViewModelTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockMacroEngine = new Mock<IMacroEngine>();
            _mockAreaSelection = new Mock<IAreaSelectionViewModel>();
            _mockAreaSelection.SetupGet(x => x.Target).Returns(Mock.Of<ICaptureTarget>(t => t.IsOpen));
            _mockCaptureSettings = new Mock<ICaptureSettingsViewModel>();
            
            // 기본 설정값
            _mockCaptureSettings.Setup(x => x.CaptureCount).Returns(5);
            _mockCaptureSettings.Setup(x => x.IntervalSeconds).Returns(5);
            _mockCaptureSettings.Setup(x => x.SaveFolderPath).Returns(@"C:\temp");
            _mockCaptureSettings.Setup(x => x.AutoInputKey).Returns("Right");
            _mockCaptureSettings.Setup(x => x.SaveAsPdf).Returns(false);
            _mockCaptureSettings.Setup(x => x.FileNamePrefix).Returns("capture");
            _mockCaptureSettings.Setup(x => x.Validate()).Returns(true);
            
            _viewModel = new MacroControlViewModel(
                _mockLogger.Object,
                _mockCaptureSettings.Object,
                _mockAreaSelection.Object,
                _mockMacroEngine.Object, (message, error) => _notifications.Add((message, error)));
        }

        [Fact]
        public void StartMacroCommand_WhenAreaNotSelected_CannotExecute()
        {
            // Arrange
            _mockAreaSelection.Setup(x => x.HasSelection).Returns(false);

            // Act
            var canExecute = _viewModel.StartMacroCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void StartMacroCommand_WhenAreaSelected_CanExecute()
        {
            // Arrange
            _mockAreaSelection.Setup(x => x.HasSelection).Returns(true);
            _mockMacroEngine.Setup(x => x.IsRunning).Returns(false);

            // Act
            var canExecute = _viewModel.StartMacroCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void StartMacroCommand_WhenRunning_CannotExecute()
        {
            // Arrange
            _mockAreaSelection.Setup(x => x.HasSelection).Returns(true);
            _mockCaptureSettings.Setup(x => x.Validate()).Returns(true);
            
            // MacroState를 Running 상태로 설정
            _viewModel.MacroState.Status = AutoCapture.Wpf.Models.MacroStatus.Running;

            // Act
            var canExecute = _viewModel.StartMacroCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void StartMacroCommand_Execute_CallsMacroEngine()
        {
            // Arrange
            _mockAreaSelection.Setup(x => x.HasSelection).Returns(true);
            _mockAreaSelection.Setup(x => x.X).Returns(0);
            _mockAreaSelection.Setup(x => x.Y).Returns(0);
            _mockAreaSelection.Setup(x => x.Width).Returns(100);
            _mockAreaSelection.Setup(x => x.Height).Returns(100);
            _mockMacroEngine.Setup(x => x.IsRunning).Returns(false);

            // Act
            _viewModel.StartMacroCommand.Execute(null);

            // Assert
            _mockMacroEngine.Verify(x => x.StartAsync(
                It.IsAny<CaptureSettings>(),
                It.IsAny<CaptureArea>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void StopMacroCommand_WhenNotRunning_CannotExecute()
        {
            // Arrange
            _mockMacroEngine.Setup(x => x.IsRunning).Returns(false);

            // Act
            var canExecute = _viewModel.StopMacroCommand.CanExecute(null);

            // Assert
            Assert.False(canExecute);
        }

        [Fact]
        public void StopMacroCommand_WhenRunning_CanExecute()
        {
            // Arrange
            _viewModel.MacroState.Status = MacroStatus.Running;

            // Act
            var canExecute = _viewModel.StopMacroCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void StopMacroCommand_Execute_CallsStopAsync()
        {
            // Arrange
            _mockMacroEngine.Setup(x => x.IsRunning).Returns(true);

            // Act
            _viewModel.StopMacroCommand.Execute(null);

            // Assert
            _mockMacroEngine.Verify(x => x.StopAsync(), Times.Once);
        }

        [Fact]
        public void StatusMessage_DefaultValue()
        {
            // Arrange & Act & Assert
            Assert.Equal("준비", _viewModel.StatusMessage);
        }

        [Fact]
        public void CurrentCaptureCount_ReflectsMacroState()
        {
            // Arrange & Act
            _viewModel.MacroState.CurrentCaptureCount = 3;

            // Assert
            Assert.Equal(3, _viewModel.CurrentCaptureCount);
        }

        [Fact]
        public void MacroEngine_ProgressChanged_UpdatesViewModel()
        {
            // Arrange
            var progressEventArgs = new MacroProgressEventArgs
            {
                CurrentCount = 5,
                TotalCount = 10,
                Message = "진행 중..."
            };

            // Act
            _mockMacroEngine.Raise(x => x.ProgressChanged += null, progressEventArgs);

            // Assert
            Assert.Equal(5, _viewModel.CurrentCaptureCount);
            Assert.Equal(10, _viewModel.MacroState.TotalCaptureCount);
        }

        [Fact]
        public void MacroEngine_Completed_LeavesResultToHud()
        {
            // Arrange
            var completedEventArgs = new MacroCompletedEventArgs
            {
                TotalAttempted = 10,
                Requested = 10,
                TotalCaptured = 10,
                ElapsedTime = TimeSpan.FromSeconds(30),
                CapturedFiles = new List<string> { "file1.png", "file2.png" }
            };

            // 성공 결과는 HUD 완료 카드가 보여주므로 알림 창을 띄우지 않는다.

            // Act
            _mockMacroEngine.Raise(x => x.Completed += null, completedEventArgs);
            Assert.Empty(_notifications);
            Assert.Equal(MacroStatus.Completed, _viewModel.MacroState.Status);

            // Assert
            Assert.False(_viewModel.MacroState.IsRunning);
            Assert.Equal(0, _viewModel.CurrentCaptureCount);
        }

        [Fact]
        public void MacroEngine_Error_UpdatesState()
        {
            // Arrange
            var errorArgs = new MacroErrorEventArgs(new Exception("Test error"));

            // Act
            _mockMacroEngine.Raise(x => x.Error += null, errorArgs);
            Assert.True(Assert.Single(_notifications).Error);
            Assert.Equal(MacroStatus.Error, _viewModel.MacroState.Status);

            // Assert
            Assert.False(_viewModel.MacroState.IsRunning);
            Assert.Equal(0, _viewModel.CurrentCaptureCount);
        }

        [Fact]
        public void IsRunning_ReflectsMacroState()
        {
            // Arrange & Act & Assert
            _viewModel.MacroState.Status = MacroStatus.Idle;
            Assert.False(_viewModel.IsRunning);
            
            _viewModel.MacroState.Status = MacroStatus.Running;
            Assert.True(_viewModel.IsRunning);
            
            _viewModel.MacroState.Status = MacroStatus.Completed;
            Assert.False(_viewModel.IsRunning);
        }
    }
}
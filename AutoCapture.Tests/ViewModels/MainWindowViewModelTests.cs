using System.Windows.Shell;
using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Models.Messages;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using Xunit;

namespace AutoCapture.Tests.ViewModels
{
    public class MainWindowViewModelTests : IDisposable
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly Mock<IAreaSelectionViewModel> _areaSelectionMock;
        private readonly Mock<ICaptureSettingsViewModel> _captureSettingsMock;
        private readonly Mock<IMacroControlViewModel> _macroControlMock;

        public MainWindowViewModelTests()
        {
            _areaSelectionMock = new Mock<IAreaSelectionViewModel>();
            _captureSettingsMock = new Mock<ICaptureSettingsViewModel>();
            _macroControlMock = new Mock<IMacroControlViewModel>();

            _viewModel = new MainWindowViewModel(
                _areaSelectionMock.Object,
                _captureSettingsMock.Object,
                _macroControlMock.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            Assert.Equal(_areaSelectionMock.Object, _viewModel.AreaSelection);
            Assert.Equal(_captureSettingsMock.Object, _viewModel.CaptureSettings);
            Assert.Equal(_macroControlMock.Object, _viewModel.MacroControl);
            Assert.Equal(TaskbarItemProgressState.None, _viewModel.TaskbarProgressState);
            Assert.Equal(0.0, _viewModel.TaskbarProgressValue);
        }

        [Fact]
        public void ProgressChangedMessage_ShouldUpdateTaskbarProgress()
        {
            var progressValue = 50.0;
            var message = new ProgressChangedMessage(progressValue, TaskbarItemProgressState.Normal);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(0.5, _viewModel.TaskbarProgressValue); // 50% = 0.5
            Assert.Equal(TaskbarItemProgressState.Normal, _viewModel.TaskbarProgressState);
        }

        [Fact]
        public void MacroStatusChangedMessage_Running_ShouldSetNormalState()
        {
            var message = new MacroStatusChangedMessage(MacroStatus.Running);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(TaskbarItemProgressState.Normal, _viewModel.TaskbarProgressState);
        }

        [Fact]
        public void MacroStatusChangedMessage_Error_ShouldSetErrorState()
        {
            var message = new MacroStatusChangedMessage(MacroStatus.Error);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(TaskbarItemProgressState.Error, _viewModel.TaskbarProgressState);
        }

        [Fact]
        public void MacroStatusChangedMessage_Paused_ShouldSetPausedState()
        {
            var message = new MacroStatusChangedMessage(MacroStatus.Paused);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(TaskbarItemProgressState.Paused, _viewModel.TaskbarProgressState);
        }

        [Fact]
        public void MacroStatusChangedMessage_Completed_ShouldResetProgress()
        {
            _viewModel.TaskbarProgressValue = 0.75;
            _viewModel.TaskbarProgressState = TaskbarItemProgressState.Normal;

            var message = new MacroStatusChangedMessage(MacroStatus.Completed);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(TaskbarItemProgressState.None, _viewModel.TaskbarProgressState);
            Assert.Equal(0.0, _viewModel.TaskbarProgressValue);
        }

        [Fact]
        public void MacroStatusChangedMessage_Idle_ShouldResetProgress()
        {
            _viewModel.TaskbarProgressValue = 0.5;
            _viewModel.TaskbarProgressState = TaskbarItemProgressState.Normal;

            var message = new MacroStatusChangedMessage(MacroStatus.Idle);

            WeakReferenceMessenger.Default.Send(message);

            Assert.Equal(TaskbarItemProgressState.None, _viewModel.TaskbarProgressState);
            Assert.Equal(0.0, _viewModel.TaskbarProgressValue);
        }

        [Fact]
        public void GetTaskbarState_ShouldReturnCorrectStateForEachStatus()
        {
            var testCases = new[]
            {
                (MacroStatus.Running, TaskbarItemProgressState.Normal),
                (MacroStatus.Error, TaskbarItemProgressState.Error),
                (MacroStatus.Paused, TaskbarItemProgressState.Paused),
                (MacroStatus.Stopping, TaskbarItemProgressState.Paused),
                (MacroStatus.Idle, TaskbarItemProgressState.None),
                (MacroStatus.Completed, TaskbarItemProgressState.None)
            };

            foreach (var (status, expectedState) in testCases)
            {
                var message = new MacroStatusChangedMessage(status);
                WeakReferenceMessenger.Default.Send(message);
                
                Assert.Equal(expectedState, _viewModel.TaskbarProgressState);
            }
        }

        [Fact]
        public void Dispose_ShouldUnregisterAllMessages()
        {
            _viewModel.Dispose();

            // 메시지를 보내도 아무 일도 일어나지 않아야 함
            var initialState = _viewModel.TaskbarProgressState;
            var initialValue = _viewModel.TaskbarProgressValue;

            WeakReferenceMessenger.Default.Send(new ProgressChangedMessage(100, TaskbarItemProgressState.Error));
            WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Running));

            Assert.Equal(initialState, _viewModel.TaskbarProgressState);
            Assert.Equal(initialValue, _viewModel.TaskbarProgressValue);
        }

        public void Dispose()
        {
            _viewModel?.Dispose();
        }
    }
}
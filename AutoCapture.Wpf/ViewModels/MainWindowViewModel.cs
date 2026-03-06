using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Models.Messages;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Shell;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IMainViewModel
    {
        [ObservableProperty]
        private IAreaSelectionViewModel _areaSelection;

        [ObservableProperty]
        private ICaptureSettingsViewModel _captureSettings;

        [ObservableProperty]
        private IMacroControlViewModel _macroControl;

        [ObservableProperty]
        private TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;

        [ObservableProperty]
        private double _taskbarProgressValue = 0.0;

        public MainWindowViewModel(
            IAreaSelectionViewModel areaSelection,
            ICaptureSettingsViewModel captureSettings,
            IMacroControlViewModel macroControl)
        {
            AreaSelection = areaSelection;
            CaptureSettings = captureSettings;
            MacroControl = macroControl;

            // 메시지 수신 등록
            WeakReferenceMessenger.Default.Register<ProgressChangedMessage>(this,
                (r, m) =>
                {
                    TaskbarProgressValue = m.Value / 100.0; // 0-1 범위로 변환
                    TaskbarProgressState = m.State;
                });

            WeakReferenceMessenger.Default.Register<MacroStatusChangedMessage>(this,
                (r, m) =>
                {
                    TaskbarProgressState = GetTaskbarState(m.Status);
                    if (m.Status == MacroStatus.Idle || m.Status == MacroStatus.Completed)
                    {
                        TaskbarProgressValue = 0.0;
                    }
                });
        }

        private TaskbarItemProgressState GetTaskbarState(MacroStatus status)
        {
            return status switch
            {
                MacroStatus.Running => TaskbarItemProgressState.Normal,
                MacroStatus.Error => TaskbarItemProgressState.Error,
                MacroStatus.Paused => TaskbarItemProgressState.Paused,
                MacroStatus.Stopping => TaskbarItemProgressState.Paused,
                _ => TaskbarItemProgressState.None
            };
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}
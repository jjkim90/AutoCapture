using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class MacroControlViewModel : ViewModelBase, IMacroControlViewModel
    {
        private readonly ILogger _logger;
        private readonly ICaptureSettingsViewModel _captureSettings;
        private readonly IAreaSelectionViewModel _areaSelection;
        
        [ObservableProperty]
        private MacroState _macroState = new();

        [ObservableProperty]
        private string _statusMessage = "준비";

        public bool IsRunning => MacroState.IsRunning;
        public int CurrentCaptureCount => MacroState.CurrentCaptureCount;

        public ICommand StartMacroCommand { get; }
        public ICommand StopMacroCommand { get; }

        public MacroControlViewModel(
            ILogger logger,
            ICaptureSettingsViewModel captureSettings,
            IAreaSelectionViewModel areaSelection)
        {
            _logger = logger;
            _captureSettings = captureSettings;
            _areaSelection = areaSelection;
            
            StartMacroCommand = new AsyncRelayCommand(StartMacroAsync, CanStartMacro);
            StopMacroCommand = new AsyncRelayCommand(StopMacroAsync, CanStopMacro);
        }

        private bool CanStartMacro()
        {
            return MacroState.CanStart 
                && _areaSelection.HasSelection 
                && _captureSettings.Validate();
        }

        private bool CanStopMacro()
        {
            return MacroState.CanStop;
        }

        private async Task StartMacroAsync()
        {
            try
            {
                _logger.Information("매크로 시작");
                MacroState.Reset();
                MacroState.Status = MacroStatus.Running;
                MacroState.StartTime = DateTime.Now;
                MacroState.TotalCaptureCount = _captureSettings.CaptureCount;
                
                StatusMessage = "매크로 실행 중...";
                OnPropertyChanged(nameof(IsRunning));
                
                // TODO: 실제 매크로 엔진 구현
                await Task.Delay(1000);
                
                StatusMessage = "매크로 완료";
                MacroState.Status = MacroStatus.Completed;
                MacroState.EndTime = DateTime.Now;
                
                _logger.Information("매크로 완료. 캡처 수: {Count}, 소요 시간: {Time}", 
                    MacroState.CurrentCaptureCount, MacroState.ElapsedTime);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "매크로 실행 중 오류 발생");
                MacroState.Status = MacroStatus.Error;
                MacroState.LastError = ex.Message;
                StatusMessage = $"오류: {ex.Message}";
            }
            finally
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(CurrentCaptureCount));
                ((IRelayCommand)StartMacroCommand).NotifyCanExecuteChanged();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            }
        }

        private async Task StopMacroAsync()
        {
            try
            {
                _logger.Information("매크로 중지 요청");
                MacroState.Status = MacroStatus.Stopping;
                StatusMessage = "매크로 중지 중...";
                
                // TODO: 실제 중지 로직 구현
                await Task.Delay(500);
                
                MacroState.Status = MacroStatus.Idle;
                MacroState.EndTime = DateTime.Now;
                StatusMessage = "매크로 중지됨";
                
                _logger.Information("매크로 중지됨");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "매크로 중지 중 오류 발생");
                StatusMessage = $"중지 오류: {ex.Message}";
            }
            finally
            {
                OnPropertyChanged(nameof(IsRunning));
                ((IRelayCommand)StartMacroCommand).NotifyCanExecuteChanged();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            }
        }

        public void UpdateProgress(int currentCount)
        {
            MacroState.CurrentCaptureCount = currentCount;
            OnPropertyChanged(nameof(CurrentCaptureCount));
            StatusMessage = $"캡처 진행 중: {currentCount}/{MacroState.TotalCaptureCount}";
        }
    }
}
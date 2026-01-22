using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Services.Interfaces;
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
        private readonly IMacroEngine _macroEngine;
        private CancellationTokenSource? _cancellationTokenSource;
        
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
            IAreaSelectionViewModel areaSelection,
            IMacroEngine macroEngine)
        {
            _logger = logger;
            _captureSettings = captureSettings;
            _areaSelection = areaSelection;
            _macroEngine = macroEngine;
            
            StartMacroCommand = new AsyncRelayCommand(StartMacroAsync, CanStartMacro);
            StopMacroCommand = new AsyncRelayCommand(StopMacroAsync, CanStopMacro);
            
            // MacroEngine 이벤트 구독
            _macroEngine.ProgressChanged += OnMacroProgressChanged;
            _macroEngine.Completed += OnMacroCompleted;
            _macroEngine.Error += OnMacroError;
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
                
                // 메인 윈도우 최소화
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow;
                    if (mainWindow != null && mainWindow.WindowState != System.Windows.WindowState.Minimized)
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Minimized;
                        _logger.Information("매크로 시작 - 메인 윈도우 최소화");
                    }
                });
                
                // 캡처 설정 가져오기
                var settings = new Models.CaptureSettings
                {
                    CaptureCount = _captureSettings.CaptureCount,
                    IntervalSeconds = _captureSettings.IntervalSeconds,
                    SaveFolderPath = _captureSettings.SaveFolderPath,
                    AutoInputKey = _captureSettings.AutoInputKey,
                    SaveAsPdf = _captureSettings.SaveAsPdf
                };
                
                // 캡처 영역 가져오기
                var captureArea = new Models.CaptureArea(
                    _areaSelection.X,
                    _areaSelection.Y,
                    _areaSelection.Width,
                    _areaSelection.Height
                );
                
                // CancellationToken 생성
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 매크로 엔진 실행
                await _macroEngine.StartAsync(
                    settings, 
                    captureArea, 
                    _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("매크로가 취소되었습니다.");
                StatusMessage = "매크로 취소됨";
                MacroState.Status = MacroStatus.Idle;
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
                MacroState.EndTime = DateTime.Now;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
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
                
                // 매크로 엔진 중지
                _cancellationTokenSource?.Cancel();
                await _macroEngine.StopAsync();
                
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
        
        private void OnMacroProgressChanged(object? sender, MacroProgressEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateProgress(e.CurrentCount);
            });
        }
        
        private void OnMacroCompleted(object? sender, MacroCompletedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MacroState.Status = MacroStatus.Completed;
                MacroState.EndTime = DateTime.Now;
                StatusMessage = $"매크로 완료 - 캡처: {e.TotalCaptured}개, 소요시간: {e.ElapsedTime:mm\\:ss}";
                
                // 메인 윈도우 복원 (선택사항)
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null && mainWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                    mainWindow.Activate();
                    _logger.Information("매크로 완료 - 메인 윈도우 복원");
                }
                
                _logger.Information("매크로 완료. 캡처 수: {Count}, 소요 시간: {Time}", 
                    e.TotalCaptured, e.ElapsedTime);
            });
        }
        
        private void OnMacroError(object? sender, MacroErrorEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MacroState.Status = MacroStatus.Error;
                MacroState.LastError = e.Exception.Message;
                StatusMessage = $"오류: {e.Message}";
                
                _logger.Error(e.Exception, "매크로 오류: {Message}", e.Message);
            });
        }
    }
}
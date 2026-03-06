using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Models.Messages;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

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
            
            // AreaSelection과 CaptureSettings의 변경 사항 구독
            _areaSelection.PropertyChanged += OnDependencyPropertyChanged;
            _captureSettings.PropertyChanged += OnDependencyPropertyChanged;
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
                
                // 작업 표시줄 상태 변경 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Running));
                
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
                    SaveAsPdf = _captureSettings.SaveAsPdf,
                    FileNamePrefix = _captureSettings.FileNamePrefix
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
                
                // 작업 표시줄 중지 상태 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Stopping));
                
                // 매크로 엔진 중지
                _cancellationTokenSource?.Cancel();
                await _macroEngine.StopAsync();
                
                MacroState.Status = MacroStatus.Idle;
                MacroState.EndTime = DateTime.Now;
                StatusMessage = "매크로 중지됨";
                
                // 작업 표시줄 상태 초기화 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Idle));
                
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
                
                // 작업 표시줄 진행률 메시지 전송
                var progressPercent = (double)e.CurrentCount / e.TotalCount * 100;
                WeakReferenceMessenger.Default.Send(
                    new ProgressChangedMessage(progressPercent, TaskbarItemProgressState.Normal));
            });
        }
        
        private void OnMacroCompleted(object? sender, MacroCompletedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MacroState.Status = MacroStatus.Completed;
                MacroState.EndTime = DateTime.Now;
                
                // 작업 표시줄 상태 완료 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Completed));
                
                // 성공/실패 상태에 따른 메시지 구성
                var failedCount = e.TotalAttempted - e.TotalCaptured;
                if (failedCount > 0)
                {
                    StatusMessage = $"매크로 완료 - 성공: {e.TotalCaptured}개, 실패: {failedCount}개, 소요시간: {e.ElapsedTime:mm\\:ss}";
                }
                else
                {
                    StatusMessage = $"매크로 완료 - 캡처: {e.TotalCaptured}개, 소요시간: {e.ElapsedTime:mm\\:ss}";
                }
                
                // 메인 윈도우 복원 (선택사항)
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null && mainWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                    mainWindow.Activate();
                    _logger.Information("매크로 완료 - 메인 윈도우 복원");
                }
                
                // 알림음 재생
                System.Media.SystemSounds.Exclamation.Play();
                
                // Windows 알림 표시
                ShowCompletionNotification(e.TotalCaptured, e.TotalAttempted, e.ElapsedTime, e.Errors);
                
                _logger.Information("매크로 완료. 성공: {Success}/{Total}, 소요 시간: {Time}", 
                    e.TotalCaptured, e.TotalAttempted, e.ElapsedTime);
            });
        }
        
        private void OnMacroError(object? sender, MacroErrorEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MacroState.Status = MacroStatus.Error;
                MacroState.LastError = e.Exception.Message;
                
                // 작업 표시줄 오류 상태 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Error));
                WeakReferenceMessenger.Default.Send(
                    new ProgressChangedMessage(100, TaskbarItemProgressState.Error));
                
                // 사용자 친화적 에러 메시지
                var userMessage = ErrorMessages.GetUserFriendlyMessage(e.Exception);
                StatusMessage = $"오류: {userMessage}";
                
                // 에러 팔업 표시
                ShowErrorNotification(e.Exception);
                
                _logger.Error(e.Exception, "매크로 오류: {Message}", e.Message);
            });
        }
        
        private void OnDependencyPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 영역 선택 또는 설정이 변경되면 커맨드 상태 업데이트
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ((IRelayCommand)StartMacroCommand).NotifyCanExecuteChanged();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            });
        }
        
        private void ShowCompletionNotification(int totalCaptured, int totalAttempted, TimeSpan elapsedTime, List<MacroErrorInfo>? errors)
        {
            try
            {
                string message;
                if (errors != null && errors.Count > 0)
                {
                    var failedCount = totalAttempted - totalCaptured;
                    message = $"캡처 작업 완료\n\n성공: {totalCaptured}개\n실패: {failedCount}개\n소요시간: {elapsedTime:mm\\:ss}\n\n실패한 캡처가 있습니다.";
                }
                else
                {
                    message = $"총 {totalCaptured}개 캡처 완료\n소요시간: {elapsedTime:mm\\:ss}";
                }
                
                // 사용자 정의 알림 창 표시
                var notificationWindow = new Views.NotificationWindow(message);
                notificationWindow.Show();
                    
                _logger.Information("작업 완료 알림 표시됨");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "알림 표시 중 오류 발생");
            }
        }
        
        private void ShowErrorNotification(Exception exception)
        {
            try
            {
                var userMessage = ErrorMessages.GetUserFriendlyMessage(exception);
                var advice = ErrorMessages.GetRecoveryAdvice(exception);
                
                var fullMessage = $"{userMessage}\n\n해결 방법:\n{advice}";
                
                // 에러 알림 창 표시
                var notificationWindow = new Views.NotificationWindow(fullMessage, isError: true);
                notificationWindow.Show();
                
                _logger.Information("에러 알림 표시됨");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "에러 알림 표시 중 오류 발생");
            }
        }
    }
}
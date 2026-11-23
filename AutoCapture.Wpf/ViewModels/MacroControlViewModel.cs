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
        private readonly Action<string, bool> _notify;
        private readonly ICaptureSettingsViewModel _captureSettings;
        private readonly IAreaSelectionViewModel _areaSelection;
        private readonly IMacroEngine _macroEngine;
        private CancellationTokenSource? _cancellationTokenSource;
        
        [ObservableProperty]
        private MacroState _macroState = new();

        [ObservableProperty]
        private string _statusMessage = "준비";

        public bool IsRunning => MacroState.IsRunning;
        public bool IsActive => MacroState.Status is MacroStatus.Running or MacroStatus.Paused or MacroStatus.Stopping;
        public int CurrentCaptureCount => MacroState.CurrentCaptureCount;
        public int TotalCaptureCount => MacroState.TotalCaptureCount;

        private void NotifyRunState()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(CurrentCaptureCount));
            OnPropertyChanged(nameof(TotalCaptureCount));
        }

        public ICommand StartMacroCommand { get; }
        public ICommand StopMacroCommand { get; }

        public MacroControlViewModel(
            ILogger logger,
            ICaptureSettingsViewModel captureSettings,
            IAreaSelectionViewModel areaSelection,
            IMacroEngine macroEngine)
            : this(logger, captureSettings, areaSelection, macroEngine,
                (message, error) => new Views.NotificationWindow(message, error).Show()) { }

        internal MacroControlViewModel(ILogger logger, ICaptureSettingsViewModel captureSettings,
            IAreaSelectionViewModel areaSelection, IMacroEngine macroEngine, Action<string, bool> notify)
        {
            _notify = notify;
            _logger = logger;
            _captureSettings = captureSettings;
            _areaSelection = areaSelection;
            _macroEngine = macroEngine;
            
            StartMacroCommand = new AsyncRelayCommand(StartMacroAsync, CanStartMacro);
            StopMacroCommand = new AsyncRelayCommand(StopMacroAsync, CanStopMacro);
            
            // MacroEngine 이벤트 구독
            _macroEngine.ProgressChanged += OnMacroProgressChanged;
            _macroEngine.PauseChanged += (_, paused) => OnUi(() =>
            {
                MacroState.Status = paused ? MacroStatus.Paused : MacroStatus.Running;
                StatusMessage = paused
                    ? $"일시정지 · {CurrentCaptureCount}장 저장 · 입력 대상 창으로 돌아가 F7로 재개"
                    : $"재개 · {CurrentCaptureCount}장 저장";
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroState.Status));
                NotifyRunState();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            });
            _macroEngine.Completed += OnMacroCompleted;
            _macroEngine.Error += OnMacroError;
            
            // AreaSelection과 CaptureSettings의 변경 사항 구독
            _areaSelection.PropertyChanged += OnDependencyPropertyChanged;
            _captureSettings.PropertyChanged += OnDependencyPropertyChanged;
            _areaSelection.StartRequested += OnStartRequested;
        }

        /// <summary>오버레이의 바로 시작. 입력 대상 창을 앞으로 가져온 뒤 시작한다.</summary>
        private void OnStartRequested(object? sender, StartRequestedEventArgs e)
        {
            if (!StartMacroCommand.CanExecute(null))
            {
                StatusMessage = StartBlockReason();
                _logger.Information("바로 시작 불가: {Reason}", StatusMessage);
                return;
            }

            var target = _areaSelection.Target;
            if (!string.IsNullOrEmpty(_captureSettings.AutoInputKey) && target is { IsOpen: true })
                target.Activate();
            StartMacroCommand.Execute(null);
            e.Started = true;
        }

        private string StartBlockReason()
        {
            if (!_captureSettings.Validate()) return "저장 폴더를 선택한 뒤 시작하세요";
            if (!string.IsNullOrEmpty(_captureSettings.AutoInputKey) && _areaSelection.Target is not { IsOpen: true })
                return "영역 중앙에서 입력 대상 창을 찾지 못했습니다. 영역을 다시 선택하세요";
            return "지금은 시작할 수 없습니다";
        }

        private static void OnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        private bool CanStartMacro()
        {
            return (string.IsNullOrEmpty(_captureSettings.AutoInputKey) || _areaSelection.Target is { IsOpen: true })
                && MacroState.CanStart
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
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
                NotifyRunState();
                
                // 작업 표시줄 상태 변경 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Running));
                
                // 메인 윈도우 최소화
                OnUi(() =>
                {
                    var mainWindow = System.Windows.Application.Current?.MainWindow;
                    if (mainWindow != null && mainWindow.WindowState != System.Windows.WindowState.Minimized)
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Minimized;
                        _logger.Information("매크로 시작 - 메인 윈도우 최소화");
                    }
                });
                
                // 캡처 설정 가져오기
                var settings = new Models.CaptureSettings
                {
                    Target = _areaSelection.Target,
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
                
                NotifyRunState();
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
                NotifyRunState();
                StatusMessage = "매크로 중지 중...";
                
                // 작업 표시줄 중지 상태 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Stopping));
                
                // 매크로 엔진 중지
                _cancellationTokenSource?.Cancel();
                await _macroEngine.StopAsync();
                
                MacroState.Status = MacroStatus.Idle;
                MacroState.EndTime = DateTime.Now;
                // 완료 이벤트가 저장한 중지 요약을 유지한다.
                
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
                NotifyRunState();
                ((IRelayCommand)StartMacroCommand).NotifyCanExecuteChanged();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            }
        }

        public void UpdateProgress(int currentCount)
        {
            MacroState.CurrentCaptureCount = currentCount;
            OnPropertyChanged(nameof(CurrentCaptureCount));
            OnPropertyChanged(nameof(TotalCaptureCount));
            StatusMessage = $"캡처 진행 중: {currentCount}/{MacroState.TotalCaptureCount}";
        }
        
        private void OnMacroProgressChanged(object? sender, MacroProgressEventArgs e)
        {
            OnUi(() =>
            {
                MacroState.TotalCaptureCount = e.TotalCount;
                UpdateProgress(e.CurrentCount);
                
                // 작업 표시줄 진행률 메시지 전송
                var progressPercent = (double)e.CurrentCount / e.TotalCount * 100;
                WeakReferenceMessenger.Default.Send(
                    new ProgressChangedMessage(progressPercent, TaskbarItemProgressState.Normal));
            });
        }
        
        private void OnMacroCompleted(object? sender, MacroCompletedEventArgs e)
        {
            OnUi(() =>
            {
                MacroState.Status = e.WasCancelled ? MacroStatus.Idle : MacroStatus.Completed;
                MacroState.EndTime = DateTime.Now;
                NotifyRunState();
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroState.Status));
                var outcome = e.WasCancelled ? $"{e.StopLocation} 중 사용자 중지" : "완료";
                StatusMessage = $"{outcome} · 요청 {e.Requested}장, 시도 {e.TotalAttempted}장, 저장 {e.TotalCaptured}장";
                if (e.WasCancelled) return;

                // 메인 윈도우 복원 (선택사항)
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                    mainWindow.Activate();
                    _logger.Information("매크로 완료 - 메인 윈도우 복원");
                }
                
                // 알림음 재생
                if (mainWindow != null) System.Media.SystemSounds.Exclamation.Play();
                
                // 완료 결과는 HUD 완료 카드가 보여준다.
                _logger.Information("매크로 완료. 성공: {Success}/{Total}, 소요 시간: {Time}", 
                    e.TotalCaptured, e.TotalAttempted, e.ElapsedTime);
            });
        }
        
        private void OnMacroError(object? sender, MacroErrorEventArgs e)
        {
            OnUi(() =>
            {
                MacroState.Status = MacroStatus.Error;
                MacroState.LastError = e.Exception.Message;
                NotifyRunState();
                
                // 작업 표시줄 오류 상태 메시지 전송
                WeakReferenceMessenger.Default.Send(new MacroStatusChangedMessage(MacroStatus.Error));
                WeakReferenceMessenger.Default.Send(
                    new ProgressChangedMessage(100.0 * CurrentCaptureCount / Math.Max(1, MacroState.TotalCaptureCount), TaskbarItemProgressState.Error));
                
                // 사용자 친화적 에러 메시지
                var userMessage = ErrorMessages.GetUserFriendlyMessage(e.Exception);
                StatusMessage = $"{e.Message} · {userMessage}";
                
                // 에러 팔업 표시
                ShowErrorNotification(e.Exception, e.Message);
                
                _logger.Error(e.Exception, "매크로 오류: {Message}", e.Message);
            });
        }
        
        private void OnDependencyPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 영역 선택 또는 설정이 변경되면 커맨드 상태 업데이트
            OnUi(() =>
            {
                ((IRelayCommand)StartMacroCommand).NotifyCanExecuteChanged();
                ((IRelayCommand)StopMacroCommand).NotifyCanExecuteChanged();
            });
        }
        
        private void ShowErrorNotification(Exception exception, string? summary)
        {
            try
            {
                var userMessage = ErrorMessages.GetUserFriendlyMessage(exception);
                var advice = ErrorMessages.GetRecoveryAdvice(exception);
                
                var fullMessage = $"{summary}\n{userMessage}\n\n해결 방법:\n{advice}";
                
                // 에러 알림 창 표시
                _notify(fullMessage, true);
                
                _logger.Information("에러 알림 표시됨");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "에러 알림 표시 중 오류 발생");
            }
        }
    }
}

using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using Serilog;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class MacroEngine : IMacroEngine
    {
        private readonly ILogger _logger;
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly IInputService _inputService;
        
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _macroTask;
        private readonly Random _random = new();
        private bool _isPaused;
        private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);

        public event EventHandler<MacroProgressEventArgs>? ProgressChanged;
        public event EventHandler<MacroCompletedEventArgs>? Completed;
        public event EventHandler<MacroErrorEventArgs>? Error;

        public bool IsRunning => _macroTask != null && !_macroTask.IsCompleted;
        public bool IsPaused => _isPaused;

        public MacroEngine(ILogger logger, IScreenCaptureService screenCaptureService, IInputService inputService)
        {
            _logger = logger;
            _screenCaptureService = screenCaptureService;
            _inputService = inputService;
        }

        public async Task StartAsync(CaptureSettings settings, CaptureArea area, CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("매크로가 이미 실행 중입니다.");
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _macroTask = RunMacroAsync(settings, area, _cancellationTokenSource.Token);
            
            await _macroTask;
        }

        private async Task RunMacroAsync(CaptureSettings settings, CaptureArea area, CancellationToken cancellationToken)
        {
            var capturedFiles = new List<string>();
            var startTime = DateTime.Now;

            try
            {
                _logger.Information("매크로 실행 시작: {Settings}", settings);
                
                // 첫 캡처 전 초기 대기 시간 (창 최소화 후 안정화)
                await Task.Delay(2000, cancellationToken);
                _logger.Information("초기 대기 완료, 캡처 시작");
                
                for (int i = 1; i <= settings.CaptureCount && !cancellationToken.IsCancellationRequested; i++)
                {
                    await _pauseSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (_isPaused)
                        {
                            _logger.Information("매크로 일시 정지됨");
                            await Task.Delay(Timeout.Infinite, cancellationToken);
                        }
                    }
                    finally
                    {
                        _pauseSemaphore.Release();
                    }

                    // 진행 상황 알림
                    ProgressChanged?.Invoke(this, new MacroProgressEventArgs
                    {
                        CurrentCount = i,
                        TotalCount = settings.CaptureCount,
                        Message = $"캡처 {i}/{settings.CaptureCount} 진행 중..."
                    });

                    // 화면 캡처 (키 입력보다 먼저)
                    var bitmap = await _screenCaptureService.CaptureAreaAsync(area);
                    if (bitmap != null)
                    {
                        var filePath = await _screenCaptureService.SaveCaptureAsync(bitmap, settings.SaveFolderPath, settings.FileNamePrefix);
                        capturedFiles.Add(filePath);
                        bitmap.Dispose();
                    }

                    // 키 입력 (캡처 후에 실행)
                    if (!string.IsNullOrEmpty(settings.AutoInputKey))
                    {
                        await Task.Delay(100, cancellationToken); // 캡처 후 짧은 대기
                        await _inputService.SendKeyAsync(settings.AutoInputKey);
                        await Task.Delay(100, cancellationToken); // 키 입력 후 짧은 대기
                    }

                    // 다음 캡처까지 대기 (마지막 캡처가 아닌 경우)
                    if (i < settings.CaptureCount)
                    {
                        var delayMs = settings.IntervalSeconds * 1000;
                        var randomDelay = _random.Next(-1000, 1001); // ±1초 랜덤
                        var totalDelay = Math.Max(100, delayMs + randomDelay); // 최소 100ms
                        
                        await Task.Delay(totalDelay, cancellationToken);
                    }
                }

                // PDF 저장 (설정된 경우)
                if (settings.SaveAsPdf && capturedFiles.Count > 0)
                {
                    await _screenCaptureService.SaveAsPdfAsync(capturedFiles, settings.SaveFolderPath, settings.FileNamePrefix);
                }

                // 완료 이벤트
                var elapsedTime = DateTime.Now - startTime;
                Completed?.Invoke(this, new MacroCompletedEventArgs
                {
                    TotalCaptured = capturedFiles.Count,
                    ElapsedTime = elapsedTime,
                    CapturedFiles = capturedFiles
                });

                _logger.Information("매크로 실행 완료. 캡처 수: {Count}, 소요 시간: {Time}", 
                    capturedFiles.Count, elapsedTime);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("매크로 실행이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "매크로 실행 중 오류 발생");
                Error?.Invoke(this, new MacroErrorEventArgs(ex, "매크로 실행 중 오류가 발생했습니다."));
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _isPaused = false;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            _logger.Information("매크로 중지 요청");
            _cancellationTokenSource?.Cancel();
            
            if (_macroTask != null)
            {
                try
                {
                    await _macroTask;
                }
                catch (OperationCanceledException)
                {
                    // 정상적인 취소
                }
            }
        }

        public async Task PauseAsync()
        {
            if (!IsRunning || _isPaused) return;

            await _pauseSemaphore.WaitAsync();
            try
            {
                _isPaused = true;
                _logger.Information("매크로 일시 정지");
            }
            finally
            {
                _pauseSemaphore.Release();
            }
        }

        public async Task ResumeAsync()
        {
            if (!IsRunning || !_isPaused) return;

            await _pauseSemaphore.WaitAsync();
            try
            {
                _isPaused = false;
                _logger.Information("매크로 재개");
            }
            finally
            {
                _pauseSemaphore.Release();
            }
        }
    }
}
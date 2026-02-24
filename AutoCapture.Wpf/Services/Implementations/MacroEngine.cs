using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.Utilities;
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
            var errors = new List<MacroErrorInfo>();
            var startTime = DateTime.Now;

            try
            {
                _logger.Information("매크로 실행 시작: {Settings}", settings);
                
                // 첫 캡처 전 초기 대기 시간 (창 최소화 후 안정화)
                await Task.Delay(2000, cancellationToken);
                _logger.Information("초기 대기 완료, 캡처 시작");
                
                // 메모리 사용량 초기 로깅
                var initialMemory = GC.GetTotalMemory(false);
                _logger.Information("매크로 시작 메모리: {MemoryMB:F2}MB", initialMemory / 1024.0 / 1024.0);
                
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
                    try
                    {
                        var filePath = await _screenCaptureService.CaptureAndSaveAsync(
                            area, 
                            settings.SaveFolderPath, 
                            settings.FileNamePrefix);
                            
                        if (filePath != null)
                        {
                            capturedFiles.Add(filePath);
                        }
                        else
                        {
                            _logger.Warning("캡처 실패 (캡처 {Count}): 화면 캡처 반환값 null", i);
                            errors.Add(new MacroErrorInfo
                            {
                                CaptureIndex = i,
                                ErrorType = "CaptureFailure",
                                ErrorMessage = "화면 캡처에 실패했습니다.",
                                OccurredAt = DateTime.Now,
                                IsRecoverable = true
                            });
                        }
                        
                        // 메모리 모니터링 및 정리
                        if (i % 50 == 0 || MemoryHelper.IsMemoryPressureHigh())
                        {
                            var currentUsage = MemoryHelper.GetCurrentMemoryUsageMB();
                            _logger.Information("현재 메모리 사용량 (캡처 {Count}): {MemoryMB:F2}MB", i, currentUsage);
                            
                            if (MemoryHelper.IsMemoryPressureHigh())
                            {
                                _logger.Warning("메모리 사용량이 높습니다. GC 실행...");
                                var (beforeMB, afterMB) = MemoryHelper.PerformGarbageCollection();
                                _logger.Information("메모리 정리 완료: {BeforeMB:F2}MB -> {AfterMB:F2}MB", beforeMB, afterMB);
                                
                                // 메모리 압박이 여전히 높으면 잠시 대기
                                if (MemoryHelper.IsMemoryPressureHigh(1500))
                                {
                                    _logger.Warning("메모리 압박이 계속됩니다. 5초 대기...");
                                    await Task.Delay(5000, cancellationToken);
                                }
                            }
                        }
                    }
                    catch (OutOfMemoryException oomEx)
                    {
                        _logger.Error(oomEx, "메모리 부족 (캡처 {Count})", i);
                        errors.Add(new MacroErrorInfo
                        {
                            CaptureIndex = i,
                            ErrorType = "OutOfMemoryException",
                            ErrorMessage = "메모리가 부족합니다. 다른 프로그램을 종료하거나 캡처 범위를 줄여주세요.",
                            OccurredAt = DateTime.Now,
                            IsRecoverable = false
                        });
                        
                        // 메모리 부족 시 안전한 중단
                        throw;
                    }
                    catch (Exception captureEx)
                    {
                        _logger.Error(captureEx, "캡처 실패 (캡처 {Count})", i);
                        errors.Add(new MacroErrorInfo
                        {
                            CaptureIndex = i,
                            ErrorType = captureEx.GetType().Name,
                            ErrorMessage = captureEx.Message,
                            OccurredAt = DateTime.Now,
                            IsRecoverable = true
                        });
                        
                        // 캡처 실패 시 Error 이벤트 발생
                        Error?.Invoke(this, new MacroErrorEventArgs(captureEx, $"캡처 {i} 실패"));
                    }

                    // 키 입력 (캡처 후에 실행 - 랜덤 타이밍)
                    if (!string.IsNullOrEmpty(settings.AutoInputKey))
                    {
                        await Task.Delay(_random.Next(100, 301), cancellationToken); // 캡처 후 랜덤 대기
                        await _inputService.SendKeyWithRandomTimingAsync(settings.AutoInputKey);
                    }

                    // 다음 캡처까지 대기 (마지막 캡처가 아닌 경우)
                    if (i < settings.CaptureCount)
                    {
                        var delayMs = settings.IntervalSeconds * 1000;
                        // 더 넓은 랜덤 범위: -50% ~ +100%
                        var minRandom = -(int)(delayMs * 0.5);
                        var maxRandom = (int)(delayMs * 1.0);
                        var randomDelay = _random.Next(minRandom, maxRandom + 1);
                        var totalDelay = Math.Max(1000, delayMs + randomDelay); // 최소 1초
                        
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
                    TotalAttempted = settings.CaptureCount,
                    ElapsedTime = elapsedTime,
                    CapturedFiles = capturedFiles,
                    Errors = errors.Count > 0 ? errors : null
                });

                _logger.Information("매크로 실행 완료. 성공: {Success}/{Total}, 실패: {Failed}, 소요 시간: {Time}", 
                    capturedFiles.Count, settings.CaptureCount, errors.Count, elapsedTime);
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
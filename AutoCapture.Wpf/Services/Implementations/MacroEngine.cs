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

        private readonly object _runLock = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _macroTask;
        private readonly Func<int, CancellationToken, Task> _delay;
        private readonly Random _random = new();
        private readonly object _pauseLock = new();
        private TaskCompletionSource? _resume;
        public event EventHandler<bool>? PauseChanged;

        public event EventHandler<MacroProgressEventArgs>? ProgressChanged;
        public event EventHandler<MacroCompletedEventArgs>? Completed;
        public event EventHandler<MacroErrorEventArgs>? Error;

        public bool IsRunning { get { lock (_runLock) return _macroTask != null; } }
        public bool IsPaused { get { lock (_pauseLock) return _resume != null; } }

        public MacroEngine(ILogger logger, IScreenCaptureService screenCaptureService, IInputService inputService)
            : this(logger, screenCaptureService, inputService, Task.Delay) { }

        internal MacroEngine(ILogger logger, IScreenCaptureService screenCaptureService,
            IInputService inputService, Func<int, CancellationToken, Task> delay)
        {
            _delay = delay;
            _logger = logger;
            _screenCaptureService = screenCaptureService;
            _inputService = inputService;
        }

        public async Task StartAsync(CaptureSettings settings, CaptureArea area, CancellationToken cancellationToken = default)
        {
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_runLock)
            {
                if (_macroTask != null) throw new InvalidOperationException("매크로가 이미 실행 중입니다.");
                _cancellationTokenSource = source;
                _macroTask = finished.Task;
            }

            try { await RunMacroAsync(settings, area, source.Token); }
            finally
            {
                lock (_runLock)
                {
                    lock (_pauseLock) _resume = null;
                    _cancellationTokenSource = null;
                    _macroTask = null;
                    finished.TrySetResult();
                }
            }
        }

        private async Task RunMacroAsync(CaptureSettings settings, CaptureArea area, CancellationToken cancellationToken)
        {
            var capturedFiles = new List<string>();
            int attempted = 0;
            string operation = "시작 대기";
            var startTime = DateTime.Now;

            var target = string.IsNullOrEmpty(settings.AutoInputKey) ? null : settings.Target;
            Task BeforeAction() => WaitForTargetAsync(target, cancellationToken);

            void Finish(bool cancelled)
            {
                Completed?.Invoke(this, new MacroCompletedEventArgs
                {
                    Requested = settings.CaptureCount,
                    TotalAttempted = attempted,
                    TotalCaptured = capturedFiles.Count,
                    CapturedFiles = capturedFiles,
                    ElapsedTime = DateTime.Now - startTime,
                    StopLocation = cancelled ? operation : null,
                    WasCancelled = cancelled
                });
            }

            try
            {
                _logger.Information("매크로 실행 시작: {Settings}", settings);

                if (!string.IsNullOrEmpty(settings.AutoInputKey) && target == null)
                    throw new InvalidOperationException("영역을 다시 선택해 입력 대상 창을 지정하세요.");

                // 첫 캡처 전 초기 대기 시간 (창 최소화 후 안정화)
                await _delay(2000, cancellationToken);
                _logger.Information("초기 대기 완료, 캡처 시작");

                // 메모리 사용량 초기 로깅
                var initialMemory = GC.GetTotalMemory(false);
                _logger.Information("매크로 시작 메모리: {MemoryMB:F2}MB", initialMemory / 1024.0 / 1024.0);

                for (int i = 1; i <= settings.CaptureCount; i++)
                {
                    operation = $"{i}장 저장";
                    await BeforeAction();
                    attempted = i;
                    var filePath = await _screenCaptureService.CaptureAndSaveAsync(
                        area, settings.SaveFolderPath, settings.FileNamePrefix, BeforeAction);
                    if (filePath == null)
                        throw new System.IO.IOException("재시도 후에도 화면을 저장할 수 없습니다.");

                    capturedFiles.Add(filePath);
                    ProgressChanged?.Invoke(this, new MacroProgressEventArgs
                    {
                        CurrentCount = capturedFiles.Count,
                        TotalCount = settings.CaptureCount,
                        Message = $"{i}/{settings.CaptureCount}장 저장 완료"
                    });
                    cancellationToken.ThrowIfCancellationRequested();

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
                                await _delay(5000, cancellationToken);
                            }
                        }
                    }

                    // 키 입력 (캡처 후에 실행 - 랜덤 타이밍)
                    if (i < settings.CaptureCount && !string.IsNullOrEmpty(settings.AutoInputKey))
                    {
                        operation = $"{i}장 저장 후 페이지 전환";
                        await _delay(_random.Next(100, 301), cancellationToken); // 캡처 후 랜덤 대기
                        await BeforeAction();
                        await _inputService.SendKeyWithRandomTimingAsync(settings.AutoInputKey, BeforeAction);
                    }

                    // 다음 캡처까지 대기 (마지막 캡처가 아닌 경우)
                    if (i < settings.CaptureCount)
                    {
                        operation = $"{i + 1}장 대기";
                        var delayMs = settings.IntervalSeconds * 1000;
                        // 더 넓은 랜덤 범위: -50% ~ +100%
                        var minRandom = -(int)(delayMs * 0.5);
                        var maxRandom = (int)(delayMs * 1.0);
                        var randomDelay = _random.Next(minRandom, maxRandom + 1);
                        var totalDelay = Math.Max(1000, delayMs + randomDelay); // 최소 1초

                        await _delay(totalDelay, cancellationToken);
                    }
                }

                // PDF 저장 (설정된 경우)
                if (settings.SaveAsPdf && capturedFiles.Count > 0)
                {
                    operation = "PDF 생성";
                    await _screenCaptureService.SaveAsPdfAsync(capturedFiles, settings.SaveFolderPath, settings.FileNamePrefix);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Finish(false);
                _logger.Information("매크로 완료: {Count}장", capturedFiles.Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Finish(true);
                _logger.Information("매크로 중지: {Count}장 저장", capturedFiles.Count);
            }
            catch (Exception ex)
            {
                var summary = $"{operation} 실패 · 요청 {settings.CaptureCount}장, 시도 {attempted}장, 저장 {capturedFiles.Count}장";
                _logger.Error(ex, "{Summary}", summary);
                Error?.Invoke(this, new MacroErrorEventArgs(ex, summary));
            }
        }

        public Task StopAsync()
        {
            lock (_runLock)
            {
                if (_cancellationTokenSource == null) return Task.CompletedTask;
                _logger.Information("매크로 중지 요청");
                _cancellationTokenSource.Cancel();
                return _macroTask ?? Task.CompletedTask;
            }
        }

        private async Task WaitForTargetAsync(ICaptureTarget? target, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (target is { IsOpen: false })
                    throw new InvalidOperationException("입력 대상 창이 닫혔습니다.");

                if (target is { IsForeground: false }) await PauseAsync();
                Task? wait;
                lock (_pauseLock) wait = _resume?.Task;
                if (wait == null)
                {
                    if (target == null || target.IsForeground) return;
                    continue;
                }

                // While paused, detect a closed target even if F7 is never pressed.
                await Task.WhenAny(wait, Task.Delay(200, token)).ConfigureAwait(false);
            }
        }

        public Task PauseAsync()
        {
            lock (_runLock)
            {
                if (_macroTask == null) return Task.CompletedTask;
                lock (_pauseLock)
                {
                    if (_resume != null) return Task.CompletedTask;
                    _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            PauseChanged?.Invoke(this, true);
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            TaskCompletionSource? resume;
            lock (_pauseLock)
            {
                resume = _resume;
                if (resume == null) return Task.CompletedTask;
                _resume = null;
            }
            PauseChanged?.Invoke(this, false);
            resume.TrySetResult();
            return Task.CompletedTask;
        }
    }
}

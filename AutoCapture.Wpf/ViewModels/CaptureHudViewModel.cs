using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Models.Messages;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.Utilities;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace AutoCapture.Wpf.ViewModels
{
    public enum HudState { Hidden, Running, Paused, Completed }

    /// <summary>
    /// 실행 중 화면 위에 떠 있는 상태 표시. 엔진 이벤트를 구독해 보여주기만 하고 실행 흐름은 바꾸지 않는다.
    /// </summary>
    public partial class CaptureHudViewModel : ViewModelBase, IDisposable
    {
        private readonly IMacroEngine _engine;
        private readonly ICaptureSettingsViewModel _settings;
        private readonly IAreaSelectionViewModel _area;
        private readonly IMacroControlViewModel _control;
        private readonly ILogger _logger;
        private readonly Func<string, Task<ImageSource?>> _loadThumbnail;
        private readonly Action<string> _open;

        private int _intervalSeconds;
        private string _folder = string.Empty;
        private string? _pdfPath;

        [ObservableProperty]
        private HudState _state = HudState.Hidden;

        [ObservableProperty]
        private int _current;

        [ObservableProperty]
        private int _total;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _remainingText = string.Empty;

        [ObservableProperty]
        private ImageSource? _lastThumbnail;

        [ObservableProperty]
        private string _completedTitle = string.Empty;

        [ObservableProperty]
        private string _completedDetail = string.Empty;

        [ObservableProperty]
        private bool _canOpenPdf;

        public CaptureArea Area => new(_area.X, _area.Y, _area.Width, _area.Height);

        public IAsyncRelayCommand PauseCommand { get; }
        public IAsyncRelayCommand ResumeCommand { get; }
        public IRelayCommand StopCommand { get; }
        public IRelayCommand OpenFolderCommand { get; }
        public IRelayCommand OpenPdfCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public CaptureHudViewModel(IMacroEngine engine, ICaptureSettingsViewModel settings,
            IAreaSelectionViewModel area, IMacroControlViewModel control, ILogger logger)
            : this(engine, settings, area, control, logger,
                path => Task.Run<ImageSource?>(() => ScreenSnapshot.LoadThumbnail(path)),
                path => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
        { }

        internal CaptureHudViewModel(IMacroEngine engine, ICaptureSettingsViewModel settings,
            IAreaSelectionViewModel area, IMacroControlViewModel control, ILogger logger,
            Func<string, Task<ImageSource?>> loadThumbnail, Action<string> open)
        {
            _engine = engine;
            _settings = settings;
            _area = area;
            _control = control;
            _logger = logger;
            _loadThumbnail = loadThumbnail;
            _open = open;

            PauseCommand = new AsyncRelayCommand(() => _engine.PauseAsync(), () => State == HudState.Running);
            ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => State == HudState.Paused);
            StopCommand = new RelayCommand(Stop);
            OpenFolderCommand = new RelayCommand(() => Open(_folder));
            OpenPdfCommand = new RelayCommand(() => { if (_pdfPath != null) Open(_pdfPath); });
            CloseCommand = new RelayCommand(() => State = HudState.Hidden);

            _engine.ProgressChanged += OnProgressChanged;
            _engine.PauseChanged += OnPauseChanged;
            _engine.Completed += OnCompleted;
            _engine.Error += OnError;
            WeakReferenceMessenger.Default.Register<MacroStatusChangedMessage>(this,
                (_, message) => OnUi(() => OnStatusChanged(message.Status)));
        }

        partial void OnStateChanged(HudState value)
        {
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
        }

        private static void OnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        private bool IsLive => State is HudState.Running or HudState.Paused;

        private void Begin()
        {
            _intervalSeconds = _settings.IntervalSeconds;
            _folder = _settings.SaveFolderPath;
            _pdfPath = null;
            CanOpenPdf = false;
            Total = _settings.CaptureCount;
            Current = 0;
            Progress = 0;
            LastThumbnail = null;
            UpdateRemaining();
            State = HudState.Running;
        }

        private void OnStatusChanged(MacroStatus status)
        {
            switch (status)
            {
                // 재개할 때도 Running이 오므로 새 실행일 때만 초기화한다.
                case MacroStatus.Running when !IsLive:
                    Begin();
                    break;
                case MacroStatus.Idle or MacroStatus.Error when IsLive:
                    State = HudState.Hidden;
                    break;
            }
        }

        private void OnProgressChanged(object? sender, MacroProgressEventArgs e) => OnUi(() =>
        {
            if (!IsLive) Begin();
            Total = e.TotalCount;
            Current = e.CurrentCount;
            Progress = Total > 0 ? (double)Current / Total : 0;
            UpdateRemaining();
            if (e.FilePath != null) _ = ShowThumbnailAsync(e.FilePath);
        });

        private void OnPauseChanged(object? sender, bool paused) => OnUi(() =>
        {
            if (IsLive) State = paused ? HudState.Paused : HudState.Running;
        });

        private void OnCompleted(object? sender, MacroCompletedEventArgs e) => OnUi(() =>
        {
            if (e.WasCancelled)
            {
                State = HudState.Hidden;
                return;
            }

            _pdfPath = e.PdfPath;
            CanOpenPdf = !string.IsNullOrEmpty(e.PdfPath);
            Current = e.TotalCaptured;
            Progress = 1;
            CompletedTitle = $"{e.TotalCaptured}장 저장 완료";
            var parts = new List<string> { $"PNG {e.TotalCaptured}" };
            if (CanOpenPdf) parts.Add("PDF 1");
            parts.Add(CaptureSettingsViewModel.FormatDuration(e.ElapsedTime));
            CompletedDetail = string.Join(" · ", parts);
            if (LastThumbnail == null && e.CapturedFiles is { Count: > 0 } files)
                _ = ShowThumbnailAsync(files[^1]);
            State = HudState.Completed;
        });

        private void OnError(object? sender, MacroErrorEventArgs e) => OnUi(() => State = HudState.Hidden);

        private void UpdateRemaining()
        {
            var remaining = Math.Max(0, Total - Current);
            if (remaining == 0)
            {
                RemainingText = "마무리 중";
                return;
            }
            var step = Math.Max(1.0, _intervalSeconds * 1.25);
            RemainingText = $"약 {CaptureSettingsViewModel.FormatDuration(TimeSpan.FromSeconds(Math.Ceiling(remaining * step)))} 남음";
        }

        private async Task ShowThumbnailAsync(string path)
        {
            var image = await _loadThumbnail(path);
            OnUi(() => { if (image != null && State != HudState.Hidden) LastThumbnail = image; });
        }

        private async Task ResumeAsync()
        {
            // 일시정지는 대상 창이 포커스를 잃어서 생긴다. 먼저 대상 창을 앞으로 가져온다.
            var target = _area.Target;
            if (!string.IsNullOrEmpty(_settings.AutoInputKey) && target is { IsOpen: true, IsForeground: false })
                target.Activate();
            await _engine.ResumeAsync();
        }

        private void Stop()
        {
            if (_control.StopMacroCommand.CanExecute(null))
                _control.StopMacroCommand.Execute(null);
        }

        private void Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                _open(path);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException or InvalidOperationException)
            {
                _logger.Warning(ex, "결과 열기 실패: {Path}", path);
            }
        }

        public void Dispose()
        {
            _engine.ProgressChanged -= OnProgressChanged;
            _engine.PauseChanged -= OnPauseChanged;
            _engine.Completed -= OnCompleted;
            _engine.Error -= OnError;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
    }
}

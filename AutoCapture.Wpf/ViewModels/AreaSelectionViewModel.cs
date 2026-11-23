using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Utilities;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class AreaSelectionViewModel : ViewModelBase, IAreaSelectionViewModel
    {
        private readonly ILogger _logger;
        private readonly Func<CaptureArea, Task<ImageSource?>> _snapshot;
        private readonly Func<string>? _summary;

        [ObservableProperty]
        private CaptureArea _captureArea = new();

        [ObservableProperty]
        private ICaptureTarget? _target;

        [ObservableProperty]
        private ImageSource? _preview;

        public int X => CaptureArea.X;
        public int Y => CaptureArea.Y;
        public int Width => CaptureArea.Width;
        public int Height => CaptureArea.Height;
        public bool HasSelection => CaptureArea.IsValid;

        public ICommand SelectAreaCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public event EventHandler<StartRequestedEventArgs>? StartRequested;

        public AreaSelectionViewModel(ILogger logger, ICaptureSettingsViewModel settings)
            : this(logger, async area =>
            {
                // 선택 오버레이가 화면에서 사라진 뒤에 찍는다.
                await Task.Delay(150);
                return ScreenSnapshot.Capture(area);
            }, () => Summarize(settings))
        { }

        internal AreaSelectionViewModel(ILogger logger, Func<CaptureArea, Task<ImageSource?>> snapshot,
            Func<string>? summary = null)
        {
            _logger = logger;
            _snapshot = snapshot;
            _summary = summary;
            SelectAreaCommand = new AsyncRelayCommand(SelectAreaAsync);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
        }

        private async Task SelectAreaAsync()
        {
            try
            {
                _logger.Information("화면 영역 선택 시작");

                // 메인 윈도우 최소화
                var mainWindow = Application.Current?.MainWindow;
                var wasMinimized = false;
                if (mainWindow != null && mainWindow.WindowState != WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Minimized;
                    wasMinimized = true;
                    _logger.Information("영역 선택을 위해 메인 윈도우 최소화");

                    // 최소화 애니메이션이 완료되도록 잠시 대기
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(delegate { }));
                }

                var selectionWindow = new Views.AreaSelectionWindow(_summary?.Invoke());
                selectionWindow.ShowDialog();

                if (selectionWindow.IsSelectionConfirmed)
                {
                    var rect = selectionWindow.SelectedArea;
                    await ApplySelectionAsync(new CaptureArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height),
                        Models.CaptureTarget.FromArea);

                    // 바로 시작했다면 메인 창은 최소화된 채로 둔다.
                    if (selectionWindow.StartRequested && RequestStart())
                        return;
                }
                else
                {
                    _logger.Information("화면 영역 선택 취소됨");
                }

                // 메인 윈도우 복원
                if (wasMinimized && mainWindow != null)
                {
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    _logger.Information("영역 선택 완료 - 메인 윈도우 복원");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "화면 영역 선택 중 오류 발생");
            }
        }

        internal async Task ApplySelectionAsync(CaptureArea area, Func<CaptureArea, ICaptureTarget?> findTarget)
        {
            CaptureArea = area;
            Target = findTarget(area);
            Preview = null;
            _logger.Information("화면 영역 선택 완료: {Area}", CaptureArea);
            NotifyArea();

            try
            {
                Preview = await _snapshot(area);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
                                           or System.Runtime.InteropServices.ExternalException
                                           or ArgumentException)
            {
                // 미리보기는 보조 정보라 실패해도 선택은 유지한다.
                _logger.Warning(ex, "영역 미리보기 생성 실패");
            }
        }

        /// <summary>바로 시작을 요청한다. 시작 조건이 맞지 않으면 false.</summary>
        internal bool RequestStart()
        {
            var args = new StartRequestedEventArgs();
            StartRequested?.Invoke(this, args);
            _logger.Information("오버레이에서 바로 시작 요청: {Started}", args.Started);
            return args.Started;
        }

        /// <summary>오버레이 툴바에 보여줄 현재 설정 요약</summary>
        internal static string Summarize(ICaptureSettingsViewModel settings)
        {
            var key = settings.AutoInputKey switch
            {
                "" or null => "넘김 없음",
                "Right" => "→ 키",
                var other => $"{other} 키"
            };
            return $"{settings.CaptureCount}장 · {settings.IntervalSeconds}초 · {key}";
        }

        private void ClearSelection()
        {
            Target = null;
            Preview = null;
            CaptureArea = new CaptureArea();
            _logger.Information("화면 영역 선택 초기화");
            NotifyArea();
        }

        private void NotifyArea()
        {
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}

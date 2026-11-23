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

        public AreaSelectionViewModel(ILogger logger)
            : this(logger, async area =>
            {
                // 선택 오버레이가 화면에서 사라진 뒤에 찍는다.
                await Task.Delay(150);
                return ScreenSnapshot.Capture(area);
            })
        { }

        internal AreaSelectionViewModel(ILogger logger, Func<CaptureArea, Task<ImageSource?>> snapshot)
        {
            _logger = logger;
            _snapshot = snapshot;
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

                var selectionWindow = new Views.AreaSelectionWindow();
                selectionWindow.ShowDialog();

                if (selectionWindow.IsSelectionConfirmed)
                {
                    var rect = selectionWindow.SelectedArea;
                    await ApplySelectionAsync(new CaptureArea((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height),
                        Models.CaptureTarget.FromArea);
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

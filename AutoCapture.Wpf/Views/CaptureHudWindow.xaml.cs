using AutoCapture.Wpf.ViewModels;
using Serilog;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace AutoCapture.Wpf.Views
{
    /// <summary>
    /// 실행 중 상태를 보여주는 떠 있는 창. 이 앱은 입력 대상 창이 포커스를 잃으면 일시정지하므로
    /// HUD는 클릭해도 활성화되지 않아야 하고, 캡처 영역과 겹쳐도 저장 이미지에 찍히면 안 된다.
    /// </summary>
    public partial class CaptureHudWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_NOACTIVATE = 0x08000000;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;
        private const uint SWP_NOSIZE = 0x0001, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

        private readonly CaptureHudViewModel _viewModel;
        private readonly ILogger _logger;
        private readonly DispatcherTimer _dismiss = new() { Interval = TimeSpan.FromSeconds(12) };
        private nint _handle;

        /// <summary>캡처 제외 설정이 적용됐는지. 실패해도 HUD는 동작한다.</summary>
        public bool IsExcludedFromCapture { get; private set; }

        public CaptureHudWindow(CaptureHudViewModel viewModel, ILogger logger)
        {
            InitializeComponent();
            DataContext = _viewModel = viewModel;
            _logger = logger;

            viewModel.PropertyChanged += OnViewModelChanged;
            SizeChanged += (_, _) => Reposition();
            DpiChanged += (_, _) => Reposition();
            _dismiss.Tick += (_, _) =>
            {
                // 마우스를 올려 두면 닫지 않는다.
                if (IsMouseOver) return;
                _dismiss.Stop();
                _viewModel.CloseCommand.Execute(null);
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _handle = new WindowInteropHelper(this).Handle;

            var style = GetWindowLongPtr(_handle, GWL_EXSTYLE);
            SetWindowLongPtr(_handle, GWL_EXSTYLE, (nint)((long)style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));

            IsExcludedFromCapture = SetWindowDisplayAffinity(_handle, WDA_EXCLUDEFROMCAPTURE);
            if (!IsExcludedFromCapture)
                _logger.Warning("HUD 캡처 제외 설정 실패: {Error}", Marshal.GetLastWin32Error());

            HwndSource.FromHwnd(_handle)?.AddHook(Hook);
        }

        private static nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            // 클릭해도 대상 창의 포커스를 빼앗지 않는다.
            if (msg != WM_MOUSEACTIVATE) return 0;
            handled = true;
            return MA_NOACTIVATE;
        }

        private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CaptureHudViewModel.State)) return;

            if (_viewModel.State == HudState.Hidden)
            {
                _dismiss.Stop();
                Hide();
                return;
            }

            if (!IsVisible) Show();
            Reposition();
            if (_viewModel.State == HudState.Completed) _dismiss.Start();
            else _dismiss.Stop();
        }

        private void Reposition()
        {
            if (!IsVisible || _handle == 0) return;

            var area = _viewModel.Area;
            var capture = new Rect(area.X, area.Y, Math.Max(1, area.Width), Math.Max(1, area.Height));
            var screen = Screen.FromRectangle(new System.Drawing.Rectangle(area.X, area.Y,
                Math.Max(1, area.Width), Math.Max(1, area.Height)));
            var work = screen.WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            var size = new Size(ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY);
            // 그림자 여백(16)을 빼고 화면 가장자리에서 8만큼 띄운다.
            var position = HudPlacement.Compute(new Rect(work.X, work.Y, work.Width, work.Height), capture,
                size, -8 * dpi.DpiScaleY);
            SetWindowPos(_handle, 0, (int)position.X, (int)position.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern nint GetWindowLongPtr(nint window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern nint SetWindowLongPtr(nint window, int index, nint value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(nint window, uint affinity);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    }
}

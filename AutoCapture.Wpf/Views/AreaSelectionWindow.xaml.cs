using AutoCapture.Wpf.Utilities;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Screen = System.Windows.Forms.Screen;

namespace AutoCapture.Wpf.Views
{
    /// <summary>
    /// 화면 위에 덮는 영역 선택 오버레이. 창 위에서 클릭하면 그 창에 맞추고, 드래그로 직접 그린다.
    /// 선택한 뒤에는 손잡이로 크기를 고치거나 안쪽을 끌어 옮기고, Enter나 툴바로 확정한다.
    /// </summary>
    public partial class AreaSelectionWindow : Window
    {
        private enum Mode { Idle, Pressed, Drawing, Adjusting, Moving, Resizing }

        private const double DragThreshold = 4;
        private const double MinimumSize = 8;
        private const double Gap = 10;

        private readonly uint _processId = (uint)Environment.ProcessId;
        private Mode _mode = Mode.Idle;
        private Point _pressPoint;
        private Point _lastSnapPoint = new(double.NaN, double.NaN);
        private Rect _origin;
        private Rect _selection = Rect.Empty;
        private Rect _hover = Rect.Empty;
        private SelectionHandle _handle;
        private Rect? _initialArea;

        public Rect SelectedArea { get; private set; }
        public bool IsSelectionConfirmed { get; private set; }
        /// <summary>툴바의 바로 시작으로 확정했는지</summary>
        public bool StartRequested { get; private set; }

        public AreaSelectionWindow() : this(null) { }

        /// <param name="settingsSummary">툴바에 보여줄 현재 설정 요약</param>
        /// <param name="initialArea">이미 고른 영역(물리 픽셀). 있으면 그 영역을 고른 상태로 연다.</param>
        public AreaSelectionWindow(string? settingsSummary, Rect? initialArea = null)
        {
            InitializeComponent();
            _initialArea = initialArea is { IsEmpty: false, Width: > 0, Height: > 0 } ? initialArea : null;
            SummaryText.Text = settingsSummary ?? string.Empty;
            SummaryText.Visibility = string.IsNullOrEmpty(settingsSummary) ? Visibility.Collapsed : Visibility.Visible;
            SelectionCanvas.SizeChanged += (_, _) => Reset();
            SelectionCanvas.MouseRightButtonDown += (_, e) =>
            {
                // 우클릭: 지금 영역을 버리고 새로 그린다.
                ClearSelection(e.GetPosition(SelectionCanvas));
                e.Handled = true;
            };
            // 툴바 여백을 눌러도 새 선택이 시작되지 않게 한다.
            Toolbar.MouseLeftButtonDown += (_, e) => e.Handled = true;
            SourceInitialized += (_, _) =>
            {
                var monitors = Screen.AllScreens;
                MonitorPicker.ItemsSource = monitors;
                MonitorPicker.Visibility = monitors.Length > 1 ? Visibility.Visible : Visibility.Collapsed;
                // 이전 영역이 있으면 그 영역이 있는 모니터에서 연다.
                var current = _initialArea is { } area
                    ? Screen.FromRectangle(new System.Drawing.Rectangle((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height))
                    : Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                MonitorPicker.SelectedItem = monitors.First(m => m.DeviceName == current.DeviceName);
            };
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            // Esc와 Enter가 바로 먹도록 키보드 포커스를 가져온다.
            Activate();
            Focus();
            Keyboard.Focus(this);

            if (_initialArea is not { } area) return;
            _initialArea = null;
            var restored = SelectionMath.Clamp(new Rect(SelectionCanvas.PointFromScreen(area.TopLeft),
                                                        SelectionCanvas.PointFromScreen(area.BottomRight)), Bounds);
            if (restored.IsEmpty || restored.Width < MinimumSize || restored.Height < MinimumSize) return;
            _selection = restored;
            _hover = Rect.Empty;
            _mode = Mode.Adjusting;
            Render();
        }

        /// <summary>
        /// 커서 모양: 크기 조절 중은 손잡이 방향, 이동 중과 선택 안쪽은 이동 커서, 나머지는 창의 십자 커서(null).
        /// </summary>
        internal static Cursor? CursorFor(bool adjusting, bool moving, SelectionHandle? resizing, bool insideSelection, bool drawNew)
        {
            if (resizing is { } handle) return HandleCursor(handle);
            if (moving) return Cursors.SizeAll;
            return adjusting && insideSelection && !drawNew ? Cursors.SizeAll : null;
        }

        internal static Cursor HandleCursor(SelectionHandle handle) => handle switch
        {
            SelectionHandle.TopLeft or SelectionHandle.BottomRight => Cursors.SizeNWSE,
            SelectionHandle.TopRight or SelectionHandle.BottomLeft => Cursors.SizeNESW,
            SelectionHandle.Top or SelectionHandle.Bottom => Cursors.SizeNS,
            _ => Cursors.SizeWE
        };

        private static bool DrawNewModifier => (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        private void UpdateCursor(Point point)
        {
            SelectionCanvas.Cursor = CursorFor(_mode == Mode.Adjusting, _mode == Mode.Moving,
                _mode == Mode.Resizing ? _handle : null, _selection.Contains(point), DrawNewModifier);
        }

        private void ClearSelection(Point point)
        {
            if (_mode is Mode.Drawing or Mode.Moving or Mode.Resizing) SelectionCanvas.ReleaseMouseCapture();
            _selection = Rect.Empty;
            _mode = Mode.Idle;
            _lastSnapPoint = new Point(double.NaN, double.NaN);
            UpdateHover(point, force: true);
            UpdateCursor(point);
            Render();
        }

        private void Redraw_Click(object sender, RoutedEventArgs e) => ClearSelection(Mouse.GetPosition(SelectionCanvas));

        private Rect Bounds => new(0, 0, SelectionCanvas.ActualWidth, SelectionCanvas.ActualHeight);

        private IEnumerable<Rectangle> Handles => SelectionCanvas.Children.OfType<Rectangle>()
            .Where(r => r.Tag is SelectionHandle);

        private void MonitorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MonitorPicker.SelectedItem is not Screen monitor) return;
            var bounds = monitor.Bounds;
            var window = new WindowInteropHelper(this).Handle;
            if (window != 0)
                SetWindowPos(window, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014);
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

        internal static Rect ScreenBounds(Point first, Point second, Func<Point, Point> toScreen)
        {
            var a = toScreen(first);
            var b = toScreen(second);
            return new Rect(new Point(Math.Round(a.X), Math.Round(a.Y)),
                            new Point(Math.Round(b.X), Math.Round(b.Y)));
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsSelectionConfirmed = false;
                Close();
            }
            else if (e.Key == Key.Enter && _mode == Mode.Adjusting)
            {
                Confirm(start: false);
                e.Handled = true;
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(SelectionCanvas);

            if (e.OriginalSource is Rectangle { Tag: SelectionHandle handle })
            {
                _mode = Mode.Resizing;
                _handle = handle;
                _origin = _selection;
            }
            else if (_mode == Mode.Adjusting && _selection.Contains(point) && !DrawNewModifier)
            {
                if (e.ClickCount == 2)
                {
                    Confirm(start: false);
                    return;
                }
                _mode = Mode.Moving;
                _origin = _selection;
            }
            else
            {
                _mode = Mode.Pressed;
            }

            _pressPoint = point;
            Toolbar.Visibility = Visibility.Collapsed;
            SelectionCanvas.CaptureMouse();
            UpdateCursor(point);
            e.Handled = true;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var point = e.GetPosition(SelectionCanvas);
            switch (_mode)
            {
                case Mode.Idle:
                    UpdateHover(point);
                    return;
                case Mode.Adjusting:
                    UpdateCursor(point);
                    return;
                case Mode.Pressed when (point - _pressPoint).Length > DragThreshold:
                    _mode = Mode.Drawing;
                    _hover = Rect.Empty;
                    goto case Mode.Drawing;
                case Mode.Drawing:
                    _selection = SelectionMath.FromPoints(_pressPoint, point, Bounds);
                    break;
                case Mode.Moving:
                    _selection = SelectionMath.Move(_origin, point - _pressPoint, Bounds);
                    break;
                case Mode.Resizing:
                    _selection = SelectionMath.Resize(_origin, _handle, point - _pressPoint, MinimumSize, Bounds);
                    break;
                default:
                    return;
            }
            Render();
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SelectionCanvas.ReleaseMouseCapture();
            switch (_mode)
            {
                case Mode.Pressed:
                    // 드래그 없이 클릭: 커서 아래 창에 맞춘다.
                    UpdateHover(e.GetPosition(SelectionCanvas), force: true);
                    _selection = _hover;
                    _hover = Rect.Empty;
                    _mode = _selection.IsEmpty ? Mode.Idle : Mode.Adjusting;
                    break;
                case Mode.Drawing:
                    _mode = _selection.Width >= MinimumSize && _selection.Height >= MinimumSize ? Mode.Adjusting : Mode.Idle;
                    if (_mode == Mode.Idle) _selection = Rect.Empty;
                    break;
                case Mode.Moving:
                case Mode.Resizing:
                    _mode = Mode.Adjusting;
                    break;
            }
            UpdateCursor(e.GetPosition(SelectionCanvas));
            Render();
        }

        private void UpdateHover(Point point, bool force = false)
        {
            if (!force && (point - _lastSnapPoint).Length < 3) return;
            _lastSnapPoint = point;

            var screen = SelectionCanvas.PointToScreen(point);
            var found = WindowSnap.Find((int)Math.Round(screen.X), (int)Math.Round(screen.Y), _processId);
            _hover = found is { } rect
                ? SelectionMath.Clamp(new Rect(SelectionCanvas.PointFromScreen(rect.TopLeft),
                                               SelectionCanvas.PointFromScreen(rect.BottomRight)), Bounds)
                : Rect.Empty;
            if (!force) Render();
        }

        private void Reset()
        {
            _mode = Mode.Idle;
            _selection = Rect.Empty;
            _hover = Rect.Empty;
            Render();
        }

        private void Render()
        {
            var bounds = Bounds;
            var focus = !_selection.IsEmpty ? _selection : _hover;

            var scrim = new GeometryGroup { FillRule = FillRule.EvenOdd };
            scrim.Children.Add(new RectangleGeometry(bounds));
            if (!focus.IsEmpty) scrim.Children.Add(new RectangleGeometry(focus));
            Scrim.Data = scrim;

            Place(HoverRectangle, _selection.IsEmpty ? _hover : Rect.Empty);
            Place(SelectionRectangle, _selection);

            var adjusting = _mode == Mode.Adjusting;
            foreach (var handle in Handles)
            {
                handle.Visibility = adjusting ? Visibility.Visible : Visibility.Collapsed;
                if (!adjusting) continue;
                var at = HandlePoint(_selection, (SelectionHandle)handle.Tag);
                Canvas.SetLeft(handle, at.X - handle.Width / 2);
                Canvas.SetTop(handle, at.Y - handle.Height / 2);
            }

            if (focus.IsEmpty)
            {
                SizeBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                var physical = ScreenBounds(focus.TopLeft, focus.BottomRight, SelectionCanvas.PointToScreen);
                // 점선 미리보기는 아직 고른 게 아니라는 것을 글로도 알려준다.
                SizeText.Text = _selection.IsEmpty
                    ? $"{physical.Width:0} × {physical.Height:0} · 클릭하면 선택"
                    : $"{physical.Width:0} × {physical.Height:0}";
                SizeBadge.Visibility = Visibility.Visible;
                Canvas.SetLeft(SizeBadge, focus.Left);
                Canvas.SetTop(SizeBadge, Math.Max(0, focus.Top - 28));
            }

            Toolbar.Visibility = adjusting ? Visibility.Visible : Visibility.Collapsed;
            if (adjusting)
            {
                Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var at = SelectionMath.ToolbarPosition(_selection, Toolbar.DesiredSize, bounds, Gap);
                Canvas.SetLeft(Toolbar, at.X);
                Canvas.SetTop(Toolbar, at.Y);
            }

            HintText.Text = adjusting
                ? "안쪽 끌기: 이동 · 우클릭 또는 Ctrl+드래그: 새로 그리기 · Enter: 확정 · Esc: 취소"
                : "창 위에서 클릭: 창에 맞춤 · 드래그: 직접 선택 · Esc: 취소";
        }

        private static void Place(FrameworkElement element, Rect rect)
        {
            if (rect.IsEmpty)
            {
                element.Visibility = Visibility.Collapsed;
                return;
            }
            Canvas.SetLeft(element, rect.Left);
            Canvas.SetTop(element, rect.Top);
            element.Width = rect.Width;
            element.Height = rect.Height;
            element.Visibility = Visibility.Visible;
        }

        private static Point HandlePoint(Rect rect, SelectionHandle handle)
        {
            var x = handle switch
            {
                SelectionHandle.TopLeft or SelectionHandle.Left or SelectionHandle.BottomLeft => rect.Left,
                SelectionHandle.TopRight or SelectionHandle.Right or SelectionHandle.BottomRight => rect.Right,
                _ => rect.Left + rect.Width / 2
            };
            var y = handle switch
            {
                SelectionHandle.TopLeft or SelectionHandle.Top or SelectionHandle.TopRight => rect.Top,
                SelectionHandle.BottomLeft or SelectionHandle.Bottom or SelectionHandle.BottomRight => rect.Bottom,
                _ => rect.Top + rect.Height / 2
            };
            return new Point(x, y);
        }

        private void Confirm_Click(object sender, RoutedEventArgs e) => Confirm(start: false);

        private void Start_Click(object sender, RoutedEventArgs e) => Confirm(start: true);

        private void Confirm(bool start)
        {
            if (_selection.IsEmpty || _selection.Width <= 0 || _selection.Height <= 0) return;

            SelectedArea = ScreenBounds(_selection.TopLeft, _selection.BottomRight, SelectionCanvas.PointToScreen);
            IsSelectionConfirmed = true;
            StartRequested = start;
            Close();
        }
    }
}

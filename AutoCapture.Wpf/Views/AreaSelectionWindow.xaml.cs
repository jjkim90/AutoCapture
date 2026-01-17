using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AutoCapture.Wpf.Views
{
    public partial class AreaSelectionWindow : Window
    {
        private Point _startPoint;
        private bool _isSelecting;
        private Rectangle? _currentSelection;

        public Rect SelectedArea { get; private set; }
        public bool IsSelectionConfirmed { get; private set; }

        public AreaSelectionWindow()
        {
            InitializeComponent();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                IsSelectionConfirmed = false;
                Close();
            }
            else if (e.Key == Key.Enter && _currentSelection != null)
            {
                ConfirmSelection();
            }
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _startPoint = e.GetPosition(SelectionCanvas);
                _isSelecting = true;

                // 기존 선택 영역 초기화
                if (_currentSelection != null)
                {
                    SelectionCanvas.Children.Remove(_currentSelection);
                }

                // 새 선택 영역 생성
                _currentSelection = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(51, 33, 150, 243)),
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                };

                Canvas.SetLeft(_currentSelection, _startPoint.X);
                Canvas.SetTop(_currentSelection, _startPoint.Y);
                SelectionCanvas.Children.Add(_currentSelection);

                // 마우스 캡처
                Mouse.Capture(SelectionCanvas);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isSelecting && _currentSelection != null)
            {
                var currentPoint = e.GetPosition(SelectionCanvas);

                var x = Math.Min(currentPoint.X, _startPoint.X);
                var y = Math.Min(currentPoint.Y, _startPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                Canvas.SetLeft(_currentSelection, x);
                Canvas.SetTop(_currentSelection, y);
                _currentSelection.Width = width;
                _currentSelection.Height = height;

                // 크기 정보 업데이트
                if (width > 0 && height > 0)
                {
                    InfoText.Text = $"{width:0} × {height:0}";
                    InfoBorder.Visibility = Visibility.Visible;
                }
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isSelecting && _currentSelection != null)
            {
                _isSelecting = false;
                Mouse.Capture(null);

                // 선택 영역이 있으면 바로 확정
                if (_currentSelection.Width > 5 && _currentSelection.Height > 5)
                {
                    ConfirmSelection();
                }
            }
        }

        private void ConfirmSelection()
        {
            if (_currentSelection != null && _currentSelection.Width > 0 && _currentSelection.Height > 0)
            {
                var x = Canvas.GetLeft(_currentSelection);
                var y = Canvas.GetTop(_currentSelection);
                
                // 실제 화면 좌표로 변환 (DPI 고려)
                var source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    var dpiX = source.CompositionTarget.TransformToDevice.M11;
                    var dpiY = source.CompositionTarget.TransformToDevice.M22;
                    
                    SelectedArea = new Rect(
                        x * dpiX,
                        y * dpiY,
                        _currentSelection.Width * dpiX,
                        _currentSelection.Height * dpiY
                    );
                }
                else
                {
                    SelectedArea = new Rect(x, y, _currentSelection.Width, _currentSelection.Height);
                }

                IsSelectionConfirmed = true;
                Close();
            }
        }
    }
}
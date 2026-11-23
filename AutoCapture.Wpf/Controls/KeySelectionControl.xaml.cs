using System.Windows;
using System.Windows.Controls;

namespace AutoCapture.Wpf.Controls
{
    /// <summary>
    /// 넘김 키를 고르는 세그먼트. 값은 "Space", "Right", ""(없음) 또는 사용자 지정 키 이름이다.
    /// </summary>
    public partial class KeySelectionControl : UserControl
    {
        public static readonly DependencyProperty SelectedKeyProperty =
            DependencyProperty.Register(nameof(SelectedKey), typeof(string), typeof(KeySelectionControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (d, _) => ((KeySelectionControl)d).Refresh()));

        public string SelectedKey
        {
            get => (string)GetValue(SelectedKeyProperty);
            set => SetValue(SelectedKeyProperty, value);
        }

        internal enum Segment { Space, Right, None, Custom }

        public KeySelectionControl()
        {
            InitializeComponent();
            Refresh();
        }

        internal static Segment SegmentOf(string? key) => key switch
        {
            "Space" => Segment.Space,
            "Right" => Segment.Right,
            null or "" => Segment.None,
            _ => Segment.Custom
        };

        private void Refresh()
        {
            if (SpaceOption == null) return;
            var segment = SegmentOf(SelectedKey);
            SpaceOption.IsChecked = segment == Segment.Space;
            RightOption.IsChecked = segment == Segment.Right;
            NoneOption.IsChecked = segment == Segment.None;
            CustomOption.IsChecked = segment == Segment.Custom;
            CustomText.Text = segment == Segment.Custom ? SelectedKey : "직접";
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            SelectedKey = (string)((FrameworkElement)sender).Tag;
            Refresh();
        }

        private void Custom_Click(object sender, RoutedEventArgs e)
        {
            var window = new KeyCaptureWindow { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true && !string.IsNullOrEmpty(window.CapturedKey))
                SelectedKey = window.CapturedKey;
            // 취소하면 이전 선택으로 되돌린다.
            Refresh();
        }
    }
}

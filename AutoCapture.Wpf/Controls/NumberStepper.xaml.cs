using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace AutoCapture.Wpf.Controls
{
    /// <summary>
    /// 정수 입력 상자와 증감 버튼. 범위를 벗어난 값은 경계로 맞추고,
    /// 지우기(X) 버튼이나 빈 칸은 기본값으로 되돌린다.
    /// </summary>
    public partial class NumberStepper : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumberStepper),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    (d, _) => ((NumberStepper)d).Refresh()));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(NumberStepper),
                new PropertyMetadata(0, (d, _) => ((NumberStepper)d).Refresh()));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(NumberStepper),
                new PropertyMetadata(100, (d, _) => ((NumberStepper)d).Refresh()));

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(int), typeof(NumberStepper),
                new PropertyMetadata(0));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberStepper),
                new PropertyMetadata(string.Empty));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public int DefaultValue
        {
            get => (int)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public NumberStepper()
        {
            InitializeComponent();
            // Fluent 테마의 지우기(X) 버튼은 공개되지 않은 EditingCommands.Clear를 보낸다.
            // 글자만 지우는 대신 기본값으로 되돌린다. 이 명령이 바뀌어도 빈 칸은 Commit에서 기본값이 된다.
            CommandManager.AddPreviewExecutedHandler(Input, OnPreviewExecuted);
            Refresh();
        }

        /// <summary>
        /// 입력 문자열을 범위 안의 정수로 바꾼다. 빈 칸이면 기본값, 숫자가 아니면 현재 값을 쓴다.
        /// </summary>
        internal static int Parse(string? text, int current, int fallback, int minimum, int maximum)
        {
            int value;
            if (string.IsNullOrWhiteSpace(text)) value = fallback;
            else if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value)) value = current;
            return Math.Clamp(value, minimum, Math.Max(minimum, maximum));
        }

        private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (!IsClearCommand(e.Command)) return;
            e.Handled = true;
            ResetToDefault();
        }

        internal static bool IsClearCommand(ICommand command) =>
            command is RoutedUICommand { Name: "Clear" } routed && routed.OwnerType == typeof(EditingCommands);

        internal void ResetToDefault()
        {
            Value = Math.Clamp(DefaultValue, Minimum, Math.Max(Minimum, Maximum));
            Refresh();
            Input.SelectAll();
        }

        private void Refresh()
        {
            if (Input == null) return;
            Input.Text = Value.ToString(CultureInfo.CurrentCulture);
            DecreaseButton.IsEnabled = Value > Minimum;
            IncreaseButton.IsEnabled = Value < Maximum;
        }

        private void Commit()
        {
            var value = Parse(Input.Text, Value, DefaultValue, Minimum, Maximum);
            if (value != Value) Value = value;
            else Refresh();
        }

        private void Step(int delta)
        {
            Commit();
            Value = Math.Clamp(Value + delta, Minimum, Math.Max(Minimum, Maximum));
        }

        private void Decrease_Click(object sender, RoutedEventArgs e) => Step(-1);

        private void Increase_Click(object sender, RoutedEventArgs e) => Step(1);

        private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

        private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Commit();
                    Input.SelectAll();
                    e.Handled = true;
                    break;
                case Key.Up:
                    Step(1);
                    e.Handled = true;
                    break;
                case Key.Down:
                    Step(-1);
                    e.Handled = true;
                    break;
            }
        }
    }
}

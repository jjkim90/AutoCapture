using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoCapture.Wpf.Controls
{
    public partial class KeyCaptureWindow : Window
    {
        public string CapturedKey { get; private set; } = string.Empty;

        public KeyCaptureWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 윈도우에 포커스 설정
            Focus();
            Activate();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                // ESC로 취소
                DialogResult = false;
                Close();
            }
            else
            {
                // 키 캡처
                var keyName = GetKeyDisplayName(e.Key, 
                    Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                    Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt),
                    Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
                
                CapturedKey = keyName;
                KeyDisplayText.Text = keyName;
                KeyDisplayText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                
                // 잠시 대기 후 창 닫기
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    DialogResult = true;
                    Close();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private string GetKeyDisplayName(Key key, bool ctrl, bool alt, bool shift)
        {
            var modifiers = string.Empty;
            
            if (ctrl) modifiers += "Ctrl+";
            if (alt) modifiers += "Alt+";
            if (shift) modifiers += "Shift+";

            // 특수 키 이름 변환
            var keyName = key switch
            {
                Key.Space => "Space",
                Key.Enter => "Enter",
                Key.Tab => "Tab",
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Insert => "Insert",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Escape => "Escape",
                Key.CapsLock => "CapsLock",
                Key.NumLock => "NumLock",
                Key.Scroll => "ScrollLock",
                Key.PrintScreen => "PrintScreen",
                Key.Pause => "Pause",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemMinus => "-",
                Key.OemPlus => "+",
                Key.OemQuestion => "/",
                Key.OemSemicolon => ";",
                Key.OemQuotes => "'",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemBackslash => "\\",
                Key.OemTilde => "`",
                _ when key >= Key.F1 && key <= Key.F24 => key.ToString(),
                _ when key >= Key.D0 && key <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
                _ when key >= Key.A && key <= Key.Z => key.ToString(),
                _ when key >= Key.NumPad0 && key <= Key.NumPad9 => $"NumPad{key - Key.NumPad0}",
                _ => key.ToString()
            };

            return modifiers + keyName;
        }
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoCapture.Wpf.Controls
{
    public class KeyCaptureTextBox : TextBox
    {
        private bool _isCapturing = false;
        private string _originalValue = string.Empty;
        private Brush _originalForeground;

        public KeyCaptureTextBox()
        {
            // 기본 스타일 설정
            Cursor = Cursors.Hand;
            _originalForeground = Foreground;
            
            // 텍스트 선택 방지
            IsReadOnly = true;
            IsReadOnlyCaretVisible = false;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            StartCapture();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            StopCapture();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // 항상 캡처 모드로 전환
            if (!_isCapturing)
            {
                Focus();
            }
            else
            {
                // 이미 포커스가 있는 상태에서 클릭하면 다시 캡처 모드 시작
                StartCapture();
            }
            e.Handled = true;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (_isCapturing)
            {
                e.Handled = true;

                if (e.Key == Key.Escape)
                {
                    // ESC로 취소
                    Text = _originalValue;
                    StopCapture();
                    Keyboard.ClearFocus();
                }
                else
                {
                    // 키 캡처
                    var keyName = GetKeyDisplayName(e.Key, 
                        Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                        Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt),
                        Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));
                    
                    Text = keyName;
                    StopCapture();
                    
                    // 바인딩 업데이트 강제 실행
                    var binding = GetBindingExpression(TextProperty);
                    binding?.UpdateSource();
                    
                    // 포커스 유지하지 않음
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        Keyboard.ClearFocus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
        }

        private void StartCapture()
        {
            _isCapturing = true;
            _originalValue = Text;
            Text = "키를 누르세요... (ESC로 취소)";
            Foreground = Brushes.Gray;
            SelectAll();
        }

        private void StopCapture()
        {
            _isCapturing = false;
            Foreground = _originalForeground;
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
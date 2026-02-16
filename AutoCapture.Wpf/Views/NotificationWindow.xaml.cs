using System;
using System.Windows;
using System.Windows.Threading;

namespace AutoCapture.Wpf.Views
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _autoCloseTimer;

        public NotificationWindow(string message, bool isError = false)
        {
            InitializeComponent();
            MessageText.Text = message;
            
            // 에러 메시지인 경우 스타일 변경
            if (isError)
            {
                NotificationBorder.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFE91E63"));
                TitleText.Text = "오류 발생";
            }
            
            // 화면 우측 하단에 위치
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 20;
            Top = workingArea.Bottom - Height - 20;
            
            // 에러 메시지는 10초, 일반 메시지는 5초 후 자동 닫기
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(isError ? 10 : 5)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                Close();
            };
            _autoCloseTimer.Start();
            
            // 페이드 인 애니메이션
            Opacity = 0;
            var fadeInTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(10)
            };
            fadeInTimer.Tick += (s, e) =>
            {
                if (Opacity < 1)
                {
                    Opacity += 0.05;
                }
                else
                {
                    fadeInTimer.Stop();
                }
            };
            fadeInTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            Close();
        }
    }
}
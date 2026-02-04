using System;
using System.Windows;
using System.Windows.Threading;

namespace AutoCapture.Wpf.Views
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _autoCloseTimer;

        public NotificationWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
            
            // 화면 우측 하단에 위치
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 20;
            Top = workingArea.Bottom - Height - 20;
            
            // 5초 후 자동 닫기
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
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
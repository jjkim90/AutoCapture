using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using AutoCapture.Wpf.Models;

namespace AutoCapture.Wpf.Views
{
    public partial class ErrorDialog : Window
    {
        private readonly Exception _exception;
        private readonly bool _canContinue;
        
        public bool CanRestart { get; }
        public string CloseButtonText { get; }

        public ErrorDialog(Exception exception, bool canContinue, bool canRestart)
        {
            InitializeComponent();
            
            _exception = exception;
            _canContinue = canContinue;
            CanRestart = canRestart;
            CloseButtonText = canContinue ? "계속" : "종료";
            
            DataContext = this;
            
            // RestartButton의 Visibility 설정
            RestartButton.Visibility = canRestart ? Visibility.Visible : Visibility.Collapsed;
            
            PopulateErrorInfo();
        }

        private void PopulateErrorInfo()
        {
            // 사용자 친화적 메시지
            UserFriendlyMessageText.Text = ErrorMessages.GetUserFriendlyMessage(_exception);
            
            // 복구 조언
            RecoveryAdviceText.Text = ErrorMessages.GetRecoveryAdvice(_exception);
            
            // 상세 오류 정보
            DetailedErrorText.Text = GenerateDetailedErrorReport();
        }

        private string GenerateDetailedErrorReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== 오류 보고서 ===");
            sb.AppendLine($"발생 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"오류 타입: {_exception.GetType().FullName}");
            sb.AppendLine($"메시지: {_exception.Message}");
            sb.AppendLine();
            
            sb.AppendLine("=== 시스템 정보 ===");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($".NET: {Environment.Version}");
            sb.AppendLine($"앱 버전: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine($"프로세스 ID: {Process.GetCurrentProcess().Id}");
            sb.AppendLine($"메모리 사용량: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
            sb.AppendLine();
            
            sb.AppendLine("=== 스택 트레이스 ===");
            sb.AppendLine(_exception.StackTrace);
            
            // 내부 예외가 있는 경우
            if (_exception.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== 내부 예외 ===");
                sb.AppendLine($"타입: {_exception.InnerException.GetType().FullName}");
                sb.AppendLine($"메시지: {_exception.InnerException.Message}");
                sb.AppendLine($"스택 트레이스: {_exception.InnerException.StackTrace}");
            }
            
            return sb.ToString();
        }

        private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(GenerateDetailedErrorReport());
                
                // 복사 완료 피드백
                CopyToClipboardButton.Content = "복사됨!";
                CopyToClipboardButton.IsEnabled = false;
                
                // 2초 후 원래대로 복구
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                timer.Tick += (s, args) =>
                {
                    CopyToClipboardButton.Content = "오류 정보 복사";
                    CopyToClipboardButton.IsEnabled = true;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"클립보드 복사 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 새 프로세스 시작
                var currentProcess = Process.GetCurrentProcess();
                var startInfo = new ProcessStartInfo
                {
                    FileName = currentProcess.MainModule?.FileName ?? string.Empty,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
                
                // 현재 애플리케이션 종료
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_canContinue)
            {
                DialogResult = true;
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.Views;
using Serilog;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class ExceptionHandlerService : IExceptionHandlerService
    {
        private readonly ILogger _logger;
        private readonly Application? _application;

        public bool IsTestMode { get; set; }

        public ExceptionHandlerService(ILogger logger)
        {
            _logger = logger;
            _application = Application.Current;
        }

        public void RegisterGlobalHandlers()
        {
            // Application이 null인 경우 (테스트 환경) 처리
            if (_application != null)
            {
                // UI 스레드 예외 처리
                _application.DispatcherUnhandledException += HandleDispatcherUnhandledException;
            }
            
            // 모든 스레드 예외 처리
            AppDomain.CurrentDomain.UnhandledException += HandleAppDomainUnhandledException;
            
            // 비동기 작업 예외 처리
            TaskScheduler.UnobservedTaskException += HandleTaskSchedulerUnobservedException;
            
            _logger.Information("전역 예외 처리기 등록 완료");
        }

        public void UnregisterGlobalHandlers()
        {
            if (_application != null)
            {
                _application.DispatcherUnhandledException -= HandleDispatcherUnhandledException;
            }
            
            AppDomain.CurrentDomain.UnhandledException -= HandleAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= HandleTaskSchedulerUnobservedException;
            
            _logger.Information("전역 예외 처리기 해제 완료");
        }

        public void HandleException(Exception exception, bool canContinue = true, bool canRestart = true)
        {
            if (exception == null)
                return;

            // 테스트 모드에서는 다이얼로그를 표시하지 않음
            if (IsTestMode)
            {
                _logger.Information("Test mode: Skipping error dialog for exception {ExceptionType}", exception.GetType().Name);
                return;
            }

            // UI 스레드에서 실행되도록 보장
            if (!_application?.Dispatcher.CheckAccess() ?? false)
            {
                _application.Dispatcher.Invoke(() => HandleException(exception, canContinue, canRestart));
                return;
            }

            try
            {
                var errorDialog = new ErrorDialog(exception, canContinue, canRestart);
                errorDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                // 에러 다이얼로그 자체에서 오류가 발생한 경우
                _logger.Error(ex, "에러 다이얼로그 표시 중 오류 발생");
                ShowFallbackErrorMessage(exception, ex);
            }
        }

        private void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // 디버그 모드에서는 Visual Studio가 예외를 처리하도록 함
            if (Debugger.IsAttached)
                return;

            _logger.Error(e.Exception, "UI 스레드에서 처리되지 않은 예외 발생");

            // 예외를 처리했음을 표시 (앱이 종료되지 않도록)
            e.Handled = true;

            // 에러 다이얼로그 표시
            HandleException(e.Exception, canContinue: true, canRestart: true);
        }

        private void HandleAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            _logger.Fatal(exception, "AppDomain에서 치명적인 예외 발생. 종료 여부: {IsTerminating}", e.IsTerminating);

            // 크래시 로그 파일 생성
            WriteCrashLog(exception);

            if (e.IsTerminating)
            {
                // 종료 중에는 복잡한 UI 다이얼로그 대신 간단한 메시지 박스 사용
                ShowTerminatingErrorMessage(exception);
            }
        }

        private void HandleTaskSchedulerUnobservedException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.Error(e.Exception, "비동기 작업에서 관찰되지 않은 예외 발생");

            // 예외를 관찰했음을 표시
            e.SetObserved();

            // 에러 다이얼로그 표시
            HandleException(e.Exception, canContinue: true, canRestart: false);
        }

        private void ShowFallbackErrorMessage(Exception originalException, Exception dialogException)
        {
            if (IsTestMode)
            {
                _logger.Error("Test mode: Skipping fallback error message");
                return;
            }

            var detailedMessage = $"오류 표시 중 문제가 발생했습니다.\n\n" +
                                $"원본 오류: {originalException.Message}\n\n" +
                                $"다이얼로그 오류: {dialogException.GetType().Name}: {dialogException.Message}\n\n" +
                                $"스택 트레이스:\n{dialogException.StackTrace}";
            
            MessageBox.Show(detailedMessage, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowTerminatingErrorMessage(Exception? exception)
        {
            if (IsTestMode)
            {
                _logger.Fatal("Test mode: Skipping terminating error message for exception {ExceptionType}", 
                    exception?.GetType().Name ?? "Unknown");
                return;
            }

            var message = exception != null 
                ? $"치명적인 오류가 발생했습니다.\n\n{ErrorMessages.GetUserFriendlyMessage(exception)}\n\n프로그램을 종료합니다.\n\n자세한 정보는 logs 폴더의 크래시 로그를 확인하세요."
                : "치명적인 오류가 발생했습니다. 프로그램을 종료합니다.";
            
            MessageBox.Show(message, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void WriteCrashLog(Exception? exception)
        {
            try
            {
                var crashLogPath = Path.Combine("logs", $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                Directory.CreateDirectory("logs");

                var sb = new StringBuilder();
                sb.AppendLine("=== AutoCapture 크래시 로그 ===");
                sb.AppendLine($"발생 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"OS: {Environment.OSVersion}");
                sb.AppendLine($".NET: {Environment.Version}");
                sb.AppendLine($"앱 버전: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                sb.AppendLine();
                
                if (exception != null)
                {
                    sb.AppendLine($"예외 타입: {exception.GetType().FullName}");
                    sb.AppendLine($"메시지: {exception.Message}");
                    sb.AppendLine();
                    sb.AppendLine("스택 트레이스:");
                    sb.AppendLine(exception.StackTrace);
                    
                    if (exception.InnerException != null)
                    {
                        sb.AppendLine();
                        sb.AppendLine("내부 예외:");
                        sb.AppendLine($"타입: {exception.InnerException.GetType().FullName}");
                        sb.AppendLine($"메시지: {exception.InnerException.Message}");
                        sb.AppendLine(exception.InnerException.StackTrace);
                    }
                }

                File.WriteAllText(crashLogPath, sb.ToString());
                _logger.Information("크래시 로그 생성 완료: {Path}", crashLogPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "크래시 로그 작성 실패");
            }
        }
    }
}
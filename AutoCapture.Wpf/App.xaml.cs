using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Windows;
using System.Windows.Input;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.Services.Implementations;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using AutoCapture.Wpf.Views;
using System.Text;
using System.IO;
using AutoCapture.Wpf.Models;

namespace AutoCapture.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 전역 예외 처리기 등록
            RegisterGlobalExceptionHandlers();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            
            // 전역 단축키 등록
            RegisterGlobalHotkeys();

            base.OnStartup(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 로깅 설정
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/autocapture-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            services.AddSingleton<ILogger>(Log.Logger);

            // Views 등록
            services.AddSingleton<MainWindow>();

            // ViewModels 등록
            services.AddSingleton<IAreaSelectionViewModel, AreaSelectionViewModel>();
            services.AddSingleton<ICaptureSettingsViewModel, CaptureSettingsViewModel>();
            services.AddSingleton<IMacroControlViewModel, MacroControlViewModel>();
            services.AddSingleton<IMainViewModel, MainWindowViewModel>();
            services.AddSingleton<MainWindowViewModel>();

            // Services 등록
            services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
            services.AddSingleton<IInputService, InputService>();
            services.AddSingleton<IHotkeyService, HotkeyService>();
            services.AddSingleton<IMacroEngine, MacroEngine>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 리소스 정리
            CleanupResources();
            
            base.OnExit(e);
        }
        
        private void RegisterGlobalHotkeys()
        {
            var hotkeyService = _serviceProvider?.GetRequiredService<IHotkeyService>();
            var macroControl = _serviceProvider?.GetRequiredService<IMacroControlViewModel>();
            var logger = _serviceProvider?.GetRequiredService<ILogger>();
            
            if (hotkeyService == null || macroControl == null || logger == null)
            {
                logger?.Warning("단축키 등록 실패: 필요한 서비스를 찾을 수 없습니다.");
                return;
            }
            
            // F7: 매크로 시작
            if (hotkeyService.RegisterHotkey(Key.F7, ModifierKeys.None, () =>
            {
                logger.Information("F7 단축키 눌림");
                if (macroControl.StartMacroCommand.CanExecute(null))
                {
                    macroControl.StartMacroCommand.Execute(null);
                }
            }))
            {
                logger.Information("F7 단축키 등록 성공");
            }
            
            // F8: 매크로 중지
            if (hotkeyService.RegisterHotkey(Key.F8, ModifierKeys.None, () =>
            {
                logger.Information("F8 단축키 눌림");
                if (macroControl.StopMacroCommand.CanExecute(null))
                {
                    macroControl.StopMacroCommand.Execute(null);
                }
            }))
            {
                logger.Information("F8 단축키 등록 성공");
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            // UI 스레드 예외 처리
            Application.Current.DispatcherUnhandledException += HandleDispatcherUnhandledException;
            
            // 모든 스레드 예외 처리
            AppDomain.CurrentDomain.UnhandledException += HandleAppDomainUnhandledException;
            
            // 비동기 작업 예외 처리
            TaskScheduler.UnobservedTaskException += HandleTaskSchedulerUnobservedException;
        }

        private void HandleDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // 디버그 모드에서는 Visual Studio가 예외를 처리하도록 함
            if (Debugger.IsAttached)
                return;

            var logger = _serviceProvider?.GetService<ILogger>();
            logger?.Error(e.Exception, "UI 스레드에서 처리되지 않은 예외 발생");

            // 예외를 처리했음을 표시 (앱이 종료되지 않도록)
            e.Handled = true;

            // 에러 다이얼로그 표시
            ShowErrorDialog(e.Exception, canContinue: true, canRestart: true);
        }

        private void HandleAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var logger = _serviceProvider?.GetService<ILogger>();
            logger?.Fatal(exception, "AppDomain에서 치명적인 예외 발생. 종료 여부: {IsTerminating}", e.IsTerminating);

            // 크래시 로그 파일 생성
            WriteCrashLog(exception);

            if (e.IsTerminating)
            {
                // 리소스 정리
                CleanupResources();
                
                // 종료 중에는 복잡한 UI 다이얼로그 대신 간단한 메시지 박스 사용
                var message = exception != null 
                    ? $"치명적인 오류가 발생했습니다.\n\n{ErrorMessages.GetUserFriendlyMessage(exception)}\n\n프로그램을 종료합니다.\n\n자세한 정보는 logs 폴더의 크래시 로그를 확인하세요."
                    : "치명적인 오류가 발생했습니다. 프로그램을 종료합니다.";
                
                MessageBox.Show(message, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleTaskSchedulerUnobservedException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var logger = _serviceProvider?.GetService<ILogger>();
            logger?.Error(e.Exception, "비동기 작업에서 관찰되지 않은 예외 발생");

            // 예외를 관찰했음을 표시
            e.SetObserved();

            // 에러 다이얼로그 표시
            ShowErrorDialog(e.Exception, canContinue: true, canRestart: false);
        }

        private void ShowErrorDialog(Exception? exception, bool canContinue, bool canRestart)
        {
            if (exception == null)
                return;

            // UI 스레드에서 실행되도록 보장
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowErrorDialog(exception, canContinue, canRestart));
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
                var detailedMessage = $"오류 표시 중 문제가 발생했습니다.\n\n" +
                                    $"원본 오류: {exception.Message}\n\n" +
                                    $"다이얼로그 오류: {ex.GetType().Name}: {ex.Message}\n\n" +
                                    $"스택 트레이스:\n{ex.StackTrace}";
                
                MessageBox.Show(detailedMessage, "치명적 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            }
            catch
            {
                // 크래시 로그 작성 실패 시 무시
            }
        }

        private void CleanupResources()
        {
            try
            {
                var logger = _serviceProvider?.GetService<ILogger>();
                logger?.Information("리소스 정리 시작");

                // 전역 단축키 해제
                var hotkeyService = _serviceProvider?.GetService<IHotkeyService>();
                hotkeyService?.UnregisterAll();
                
                // 진행 중인 매크로 중지
                var macroEngine = _serviceProvider?.GetService<IMacroEngine>();
                if (macroEngine?.IsRunning == true)
                {
                    macroEngine.StopAsync().Wait(TimeSpan.FromSeconds(5));
                }
                
                // 로깅 종료
                Log.CloseAndFlush();
                
                // DI 컨테이너 정리
                _serviceProvider?.Dispose();
                
                logger?.Information("리소스 정리 완료");
            }
            catch (Exception ex)
            {
                // 정리 중 오류는 무시
                Debug.WriteLine($"리소스 정리 중 오류: {ex}");
            }
        }
    }
}

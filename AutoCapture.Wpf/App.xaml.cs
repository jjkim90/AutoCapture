using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Windows;
using System.Windows.Input;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;
using AutoCapture.Wpf.Services.Interfaces;
using AutoCapture.Wpf.Services.Implementations;

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
            // 전역 단축키 해제
            var hotkeyService = _serviceProvider?.GetService<IHotkeyService>();
            hotkeyService?.UnregisterAll();
            
            Log.CloseAndFlush();
            _serviceProvider?.Dispose();
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
    }
}

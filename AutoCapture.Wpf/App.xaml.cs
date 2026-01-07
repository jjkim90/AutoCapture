using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Windows;
using AutoCapture.Wpf.ViewModels;
using AutoCapture.Wpf.ViewModels.Interfaces;

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

            // Services 등록 (추후 추가)
            // services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
            // services.AddSingleton<IInputService, InputService>();
            // services.AddSingleton<IHotkeyService, HotkeyService>();
            // services.AddSingleton<IMacroEngine, MacroEngine>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}

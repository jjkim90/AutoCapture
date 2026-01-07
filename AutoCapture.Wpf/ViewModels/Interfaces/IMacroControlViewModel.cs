using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface IMacroControlViewModel
    {
        bool IsRunning { get; }
        int CurrentCaptureCount { get; }
        string StatusMessage { get; }
        
        ICommand StartMacroCommand { get; }
        ICommand StopMacroCommand { get; }
    }
}
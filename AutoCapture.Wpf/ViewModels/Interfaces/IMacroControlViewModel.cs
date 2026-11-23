using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface IMacroControlViewModel
    {
        bool IsRunning { get; }
        /// <summary>실행, 일시정지, 중지 중처럼 작업이 끝나지 않은 상태</summary>
        bool IsActive { get; }
        int CurrentCaptureCount { get; }
        int TotalCaptureCount { get; }
        string StatusMessage { get; }
        
        ICommand StartMacroCommand { get; }
        ICommand StopMacroCommand { get; }
    }
}
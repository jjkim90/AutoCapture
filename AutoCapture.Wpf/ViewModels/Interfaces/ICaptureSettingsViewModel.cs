using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface ICaptureSettingsViewModel
    {
        int CaptureCount { get; set; }
        int IntervalSeconds { get; set; }
        string SaveFolderPath { get; set; }
        string AutoInputKey { get; set; }
        bool SaveAsPdf { get; set; }
        
        ICommand SelectFolderCommand { get; }
        bool Validate();
    }
}
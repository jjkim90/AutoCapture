using System.ComponentModel;
using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface ICaptureSettingsViewModel : INotifyPropertyChanged
    {
        int CaptureCount { get; set; }
        int IntervalSeconds { get; set; }
        string SaveFolderPath { get; set; }
        string AutoInputKey { get; set; }
        bool SaveAsPdf { get; set; }
        string FileNamePrefix { get; set; }
        
        ICommand SelectFolderCommand { get; }
        bool Validate();
    }
}
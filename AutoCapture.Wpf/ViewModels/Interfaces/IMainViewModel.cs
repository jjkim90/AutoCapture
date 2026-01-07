namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface IMainViewModel
    {
        IAreaSelectionViewModel AreaSelection { get; }
        ICaptureSettingsViewModel CaptureSettings { get; }
        IMacroControlViewModel MacroControl { get; }
    }
}
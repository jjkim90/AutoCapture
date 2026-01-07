using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase, IMainViewModel
    {
        [ObservableProperty]
        private IAreaSelectionViewModel _areaSelection;

        [ObservableProperty]
        private ICaptureSettingsViewModel _captureSettings;

        [ObservableProperty]
        private IMacroControlViewModel _macroControl;

        public MainWindowViewModel(
            IAreaSelectionViewModel areaSelection,
            ICaptureSettingsViewModel captureSettings,
            IMacroControlViewModel macroControl)
        {
            AreaSelection = areaSelection;
            CaptureSettings = captureSettings;
            MacroControl = macroControl;
        }
    }
}
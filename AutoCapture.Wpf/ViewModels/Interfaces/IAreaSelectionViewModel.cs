using System.ComponentModel;
using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels.Interfaces
{
    public interface IAreaSelectionViewModel : INotifyPropertyChanged
    {
        Models.ICaptureTarget? Target { get; }
        int X { get; }
        int Y { get; }
        int Width { get; }
        int Height { get; }
        bool HasSelection { get; }
        /// <summary>선택 직후 찍은 영역 미리보기</summary>
        System.Windows.Media.ImageSource? Preview { get; }
        
        ICommand SelectAreaCommand { get; }
        ICommand ClearSelectionCommand { get; }
    }
}
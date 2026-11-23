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

        /// <summary>오버레이 툴바에서 바로 시작을 눌렀을 때. 시작했다면 Started를 true로 둔다.</summary>
        event EventHandler<StartRequestedEventArgs>? StartRequested;
    }

    public class StartRequestedEventArgs : EventArgs
    {
        public bool Started { get; set; }
    }
}
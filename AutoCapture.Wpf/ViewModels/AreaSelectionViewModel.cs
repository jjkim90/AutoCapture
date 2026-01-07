using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class AreaSelectionViewModel : ViewModelBase, IAreaSelectionViewModel
    {
        private readonly ILogger _logger;
        
        [ObservableProperty]
        private CaptureArea _captureArea = new();

        public int X => CaptureArea.X;
        public int Y => CaptureArea.Y;
        public int Width => CaptureArea.Width;
        public int Height => CaptureArea.Height;
        public bool HasSelection => CaptureArea.IsValid;

        public ICommand SelectAreaCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public AreaSelectionViewModel(ILogger logger)
        {
            _logger = logger;
            SelectAreaCommand = new AsyncRelayCommand(SelectAreaAsync);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
        }

        private async Task SelectAreaAsync()
        {
            try
            {
                _logger.Information("화면 영역 선택 시작");
                
                // TODO: 실제 화면 선택 UI 구현
                await Task.Delay(100);
                
                // 임시로 하드코딩된 값 설정
                CaptureArea = new CaptureArea(100, 100, 800, 600);
                
                _logger.Information("화면 영역 선택 완료: {Area}", CaptureArea);
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(HasSelection));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "화면 영역 선택 중 오류 발생");
            }
        }

        private void ClearSelection()
        {
            CaptureArea = new CaptureArea();
            _logger.Information("화면 영역 선택 초기화");
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}
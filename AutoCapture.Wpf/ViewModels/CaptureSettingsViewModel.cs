using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.ViewModels.Base;
using AutoCapture.Wpf.ViewModels.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Windows.Input;

namespace AutoCapture.Wpf.ViewModels
{
    public partial class CaptureSettingsViewModel : ViewModelBase, ICaptureSettingsViewModel
    {
        private readonly ILogger _logger;
        private readonly Properties.Settings _preferences;
        
        [ObservableProperty]
        private CaptureSettings _settings = new();

        public int CaptureCount
        {
            get => Settings.CaptureCount;
            set
            {
                if (Settings.CaptureCount != value)
                {
                    Settings.CaptureCount = value;
                    OnPropertyChanged(nameof(EstimatedDurationText));
                    OnPropertyChanged();
                }
            }
        }

        public int IntervalSeconds
        {
            get => Settings.IntervalSeconds;
            set
            {
                if (Settings.IntervalSeconds != value)
                {
                    Settings.IntervalSeconds = value;
                    OnPropertyChanged(nameof(EstimatedDurationText));
                    OnPropertyChanged();
                }
            }
        }

        public string EstimatedDurationText =>
            $"{CaptureCount}장 · 예상 약 {FormatDuration(EstimateDuration(CaptureCount, IntervalSeconds))}";

        /// <summary>
        /// 엔진의 실행 흐름에 맞춘 예상 소요 시간. 시작 전 2초를 기다리고,
        /// 마지막 장을 뺀 각 장 사이에 간격의 -50%~+100% 무작위 대기(평균 125%)를 둔다.
        /// </summary>
        internal static TimeSpan EstimateDuration(int count, int intervalSeconds)
        {
            if (count <= 0 || intervalSeconds <= 0) return TimeSpan.Zero;
            var seconds = 2 + (count - 1) * Math.Max(1.0, intervalSeconds * 1.25);
            return TimeSpan.FromSeconds(Math.Ceiling(seconds));
        }

        internal static string FormatDuration(TimeSpan duration)
        {
            var total = (int)Math.Ceiling(duration.TotalSeconds);
            if (total < 60) return $"{total}초";
            if (total < 3600)
                return total % 60 == 0 ? $"{total / 60}분" : $"{total / 60}분 {total % 60}초";
            var minutes = total / 60 % 60;
            return minutes == 0 ? $"{total / 3600}시간" : $"{total / 3600}시간 {minutes}분";
        }

        public string SaveFolderPath
        {
            get => Settings.SaveFolderPath;
            set
            {
                if (Settings.SaveFolderPath != value)
                {
                    Settings.SaveFolderPath = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AutoInputKey
        {
            get => Settings.AutoInputKey;
            set
            {
                if (Settings.AutoInputKey != value)
                {
                    Settings.AutoInputKey = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool SaveAsPdf
        {
            get => Settings.SaveAsPdf;
            set
            {
                if (Settings.SaveAsPdf != value)
                {
                    Settings.SaveAsPdf = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileNamePrefix
        {
            get => Settings.FileNamePrefix;
            set
            {
                if (Settings.FileNamePrefix != value)
                {
                    Settings.FileNamePrefix = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SelectFolderCommand { get; }

        public CaptureSettingsViewModel(ILogger logger) : this(logger, Properties.Settings.Default) { }

        internal CaptureSettingsViewModel(ILogger logger, Properties.Settings preferences)
        {
            _logger = logger;
            _preferences = preferences;
            SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
            
            // 설정 복원
            LoadSettings();
        }

        private void LoadSettings()
        {
            var path = _preferences.LastSaveFolderPath;
            Settings.SaveFolderPath = Directory.Exists(path) ? path : string.Empty;
            Settings.AutoInputKey = _preferences.LastAutoInputKey ?? "Space";
        }

        private void SaveSettings()
        {
            _preferences.LastSaveFolderPath = SaveFolderPath;
            _preferences.LastAutoInputKey = AutoInputKey;
            _preferences.Save();
            _logger.Information("설정 저장됨 - 폴더: {FolderPath}, 키: {Key}", SaveFolderPath, AutoInputKey);
        }

        private async Task SelectFolderAsync()
        {
            try
            {
                // Windows 10 FolderPicker 사용
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                folderPicker.FileTypeFilter.Add("*"); // 필수 - 최소 하나의 파일 타입 필터 필요
                
                // WPF Window의 핸들 가져오기
                var window = System.Windows.Application.Current.MainWindow;
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                
                // FolderPicker를 현재 창과 연결
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, handle);
                
                // 폴더 선택
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    SaveFolderPath = folder.Path;
                    _logger.Information("저장 폴더 변경 (FolderPicker): {FolderPath}", SaveFolderPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "FolderPicker 사용 실패, 기존 방식으로 대체");
                
                // 폴백: 기존 OpenFileDialog 방식 사용
                await Task.Run(() => SelectFolderFallback());
            }
        }
        
        private void SelectFolderFallback()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 기존 OpenFileDialog 방식
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "캡처 이미지를 저장할 폴더를 선택하세요",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "폴더 선택",
                    Filter = "폴더|*.folder",
                    InitialDirectory = !string.IsNullOrEmpty(SaveFolderPath) && Directory.Exists(SaveFolderPath) 
                        ? SaveFolderPath 
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };

                if (dialog.ShowDialog() == true)
                {
                    var selectedPath = Path.GetDirectoryName(dialog.FileName);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        SaveFolderPath = selectedPath;
                        _logger.Information("저장 폴더 변경 (Fallback): {FolderPath}", SaveFolderPath);
                    }
                }
            });
        }

        public bool Validate()
        {
            return Settings.IsValid();
        }
    }
}
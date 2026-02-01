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
                    OnPropertyChanged();
                }
            }
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
                    
                    // 변경된 경로를 Settings에 저장
                    Properties.Settings.Default.LastSaveFolderPath = value;
                    Properties.Settings.Default.Save();
                    _logger.Information("저장 폴더 경로 저장됨: {FolderPath}", value);
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

        public ICommand SelectFolderCommand { get; }

        public CaptureSettingsViewModel(ILogger logger)
        {
            _logger = logger;
            SelectFolderCommand = new RelayCommand(SelectFolder);
            
            // 마지막 사용한 저장 폴더 복원
            var lastPath = Properties.Settings.Default.LastSaveFolderPath;
            if (!string.IsNullOrWhiteSpace(lastPath) && Directory.Exists(lastPath))
            {
                SaveFolderPath = lastPath;
                _logger.Information("마지막 저장 폴더 복원: {FolderPath}", SaveFolderPath);
            }
            else
            {
                SaveFolderPath = string.Empty;
            }
        }

        private void SelectFolder()
        {
            // WPF에서는 기본 폴더 선택 다이얼로그가 없으므로 OpenFileDialog를 활용
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
                    _logger.Information("저장 폴더 변경: {FolderPath}", SaveFolderPath);
                }
            }
        }

        public bool Validate()
        {
            return Settings.IsValid();
        }
    }
}
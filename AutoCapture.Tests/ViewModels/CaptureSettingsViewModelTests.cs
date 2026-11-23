using AutoCapture.Wpf.ViewModels;
using Moq;
using Serilog;
using System.ComponentModel;

namespace AutoCapture.Tests.ViewModels
{
    public class CaptureSettingsViewModelTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly CaptureSettingsViewModel _viewModel;

        private readonly string _key = Guid.NewGuid().ToString("N");

        private CaptureSettingsViewModel Open() => new(_mockLogger.Object, new Wpf.Properties.Settings { SettingsKey = _key });

        public CaptureSettingsViewModelTests()
        {
            _mockLogger = new Mock<ILogger>();
            _viewModel = Open();
        }

        [Theory]
        [InlineData("Right")]
        [InlineData("")]
        public void Reopen_RestoresFolderAndKey(string key)
        {
            var folder = Directory.CreateTempSubdirectory();
            try
            {
                _viewModel.SaveFolderPath = folder.FullName;
                _viewModel.AutoInputKey = key;
                var reopened = Open();
                Assert.Equal(folder.FullName, reopened.SaveFolderPath);
                Assert.Equal(key, reopened.AutoInputKey);
            }
            finally
            {
                folder.Delete();
            }
            var missingFolder = Open();
            Assert.Empty(missingFolder.SaveFolderPath);
            Assert.Equal(key, missingFolder.AutoInputKey);
        }

        [Fact]
        public void NoSavedFolder_CreatesAndUsesDefaultFolder()
        {
            var root = Directory.CreateTempSubdirectory();
            try
            {
                var fallback = Path.Combine(root.FullName, "AutoCapture");
                var viewModel = new CaptureSettingsViewModel(_mockLogger.Object,
                    new Wpf.Properties.Settings { SettingsKey = Guid.NewGuid().ToString("N") }, fallback);

                Assert.Equal(fallback, viewModel.SaveFolderPath);
                Assert.True(Directory.Exists(fallback));
                Assert.True(viewModel.Validate());
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }

        [Fact]
        public void SavedFolder_WinsOverDefaultFolder()
        {
            var root = Directory.CreateTempSubdirectory();
            try
            {
                var chosen = Directory.CreateDirectory(Path.Combine(root.FullName, "chosen")).FullName;
                var fallback = Path.Combine(root.FullName, "AutoCapture");
                _viewModel.SaveFolderPath = chosen;

                var reopened = new CaptureSettingsViewModel(_mockLogger.Object,
                    new Wpf.Properties.Settings { SettingsKey = _key }, fallback);

                Assert.Equal(chosen, reopened.SaveFolderPath);
                Assert.False(Directory.Exists(fallback));
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }

        [Fact]
        public void DefaultFolder_IsAutoCaptureUnderDocuments()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Assert.Equal(Path.Combine(documents, "AutoCapture"), CaptureSettingsViewModel.DefaultFolder);
        }

        // 설정 파일(user.config)을 함께 쓰므로 이 클래스 안에서 순서대로 실행한다.
        [Fact]
        public void EstimatedDurationText_ChangesWithCountAndInterval()
        {
            var changed = new List<string?>();
            _viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            _viewModel.CaptureCount = 50;
            _viewModel.IntervalSeconds = 2;

            Assert.Equal("50장 · 예상 약 2분 5초", _viewModel.EstimatedDurationText);
            Assert.Equal(2, changed.Count(name => name == nameof(CaptureSettingsViewModel.EstimatedDurationText)));
        }

        [Fact]
        public void CaptureCount_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var currentValue = _viewModel.CaptureCount;
            var propertyChangedRaised = false;
            var propertyName = string.Empty;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                propertyChangedRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            _viewModel.CaptureCount = currentValue + 5; // 다른 값으로 변경

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(nameof(CaptureSettingsViewModel.CaptureCount), propertyName);
            Assert.Equal(currentValue + 5, _viewModel.CaptureCount);
        }

        [Fact]
        public void IntervalSeconds_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CaptureSettingsViewModel.IntervalSeconds))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.IntervalSeconds = 30;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(30, _viewModel.IntervalSeconds);
        }

        [Fact]
        public void SaveFolderPath_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CaptureSettingsViewModel.SaveFolderPath))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.SaveFolderPath = @"C:\TestFolder";

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal(@"C:\TestFolder", _viewModel.SaveFolderPath);
        }

        [Fact]
        public void AutoInputKey_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CaptureSettingsViewModel.AutoInputKey))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.AutoInputKey = "Right";

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal("Right", _viewModel.AutoInputKey);
        }

        [Fact]
        public void SaveAsPdf_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CaptureSettingsViewModel.SaveAsPdf))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.SaveAsPdf = true;

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.True(_viewModel.SaveAsPdf);
        }

        [Fact]
        public void FileNamePrefix_SetValue_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(CaptureSettingsViewModel.FileNamePrefix))
                    propertyChangedRaised = true;
            };

            // Act
            _viewModel.FileNamePrefix = "custom_prefix";

            // Assert
            Assert.True(propertyChangedRaised);
            Assert.Equal("custom_prefix", _viewModel.FileNamePrefix);
        }

        [Fact]
        public void SelectFolderCommand_CanExecute_AlwaysTrue()
        {
            // Act
            var canExecute = _viewModel.SelectFolderCommand.CanExecute(null);

            // Assert
            Assert.True(canExecute);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var newViewModel = Open();

            // Assert
            Assert.Equal(10, newViewModel.CaptureCount);
            Assert.Equal(5, newViewModel.IntervalSeconds);
            Assert.True(string.IsNullOrEmpty(newViewModel.SaveFolderPath) || Directory.Exists(newViewModel.SaveFolderPath));
            Assert.Equal("Space", newViewModel.AutoInputKey);
            Assert.False(newViewModel.SaveAsPdf);
            Assert.Equal("capture", newViewModel.FileNamePrefix);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        public void CaptureCount_ValidValues_SetsCorrectly(int count)
        {
            // Act
            _viewModel.CaptureCount = count;

            // Assert
            Assert.Equal(count, _viewModel.CaptureCount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(60)]
        public void IntervalSeconds_ValidValues_SetsCorrectly(int seconds)
        {
            // Act
            _viewModel.IntervalSeconds = seconds;

            // Assert
            Assert.Equal(seconds, _viewModel.IntervalSeconds);
        }

        [Fact]
        public void MultipleProperties_SetValues_RaisesCorrectEvents()
        {
            // Arrange
            var propertyChangedEvents = new List<string>();
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName != null)
                    propertyChangedEvents.Add(e.PropertyName);
            };

            // Act
            _viewModel.CaptureCount = 20;
            _viewModel.IntervalSeconds = 10;
            _viewModel.SaveAsPdf = true;

            // Assert
            Assert.Contains(nameof(CaptureSettingsViewModel.CaptureCount), propertyChangedEvents);
            Assert.Contains(nameof(CaptureSettingsViewModel.IntervalSeconds), propertyChangedEvents);
            Assert.Contains(nameof(CaptureSettingsViewModel.SaveAsPdf), propertyChangedEvents);
        }
    }
}
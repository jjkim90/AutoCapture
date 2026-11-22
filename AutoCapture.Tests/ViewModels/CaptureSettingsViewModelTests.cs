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
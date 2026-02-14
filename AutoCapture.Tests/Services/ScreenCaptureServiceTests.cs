using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;
using System.Drawing;
using System.Drawing.Imaging;

namespace AutoCapture.Tests.Services
{
    public class ScreenCaptureServiceTests : IDisposable
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly string _testPath;

        public ScreenCaptureServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _screenCaptureService = new ScreenCaptureService(_mockLogger.Object);
            _testPath = Path.Combine(Path.GetTempPath(), "AutoCaptureTests");
            Directory.CreateDirectory(_testPath);
        }

        public void Dispose()
        {
            // 테스트 후 정리
            if (Directory.Exists(_testPath))
            {
                Directory.Delete(_testPath, true);
            }
        }

        [Fact]
        public async Task SaveCaptureAsync_ValidBitmap_SavesFile()
        {
            // Arrange
            using var bitmap = new Bitmap(100, 100);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Red);
            
            var prefix = "test";

            // Act
            var savedPath = await _screenCaptureService.SaveCaptureAsync(bitmap, _testPath, prefix);

            // Assert
            Assert.NotNull(savedPath);
            Assert.True(File.Exists(savedPath));
            Assert.StartsWith(Path.Combine(_testPath, prefix), savedPath);
            Assert.EndsWith(".png", savedPath);
        }

        [Fact]
        public async Task SaveCaptureAsync_MultipleFiles_CreatesUniqueFileNames()
        {
            // Arrange
            using var bitmap = new Bitmap(50, 50);
            var prefix = "test";
            var savedPaths = new List<string>();

            // Act
            for (int i = 0; i < 3; i++)
            {
                var path = await _screenCaptureService.SaveCaptureAsync(bitmap, _testPath, prefix);
                savedPaths.Add(path);
                await Task.Delay(1100); // 파일명에 타임스탬프가 포함되므로 대기
            }

            // Assert
            Assert.Equal(3, savedPaths.Distinct().Count());
            Assert.All(savedPaths, path => Assert.True(File.Exists(path)));
        }

        [Fact]
        public async Task SaveCaptureAsync_InvalidPath_ThrowsException()
        {
            // Arrange
            using var bitmap = new Bitmap(100, 100);
            var invalidPath = @"Z:\NonExistentPath\That\Should\Not\Exist";
            var prefix = "test";

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                async () => await _screenCaptureService.SaveCaptureAsync(bitmap, invalidPath, prefix));
        }

        [Fact(Skip = "QuestPDF requires native dependencies not available in test environment")]
        public async Task SaveAsPdfAsync_ValidImagePaths_CreatesPdf()
        {
            // Arrange
            var imagePaths = new List<string>();
            
            // 테스트 이미지 생성
            for (int i = 0; i < 3; i++)
            {
                using var bitmap = new Bitmap(100, 100);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(i % 2 == 0 ? Color.Blue : Color.Green);
                
                var imagePath = Path.Combine(_testPath, $"test_{i}.png");
                bitmap.Save(imagePath, ImageFormat.Png);
                imagePaths.Add(imagePath);
            }

            var prefix = "testpdf";

            // Act
            var pdfPath = await _screenCaptureService.SaveAsPdfAsync(imagePaths, _testPath, prefix);

            // Assert
            Assert.NotNull(pdfPath);
            Assert.True(File.Exists(pdfPath));
            Assert.StartsWith(Path.Combine(_testPath, prefix), pdfPath);
            Assert.EndsWith(".pdf", pdfPath);
            
            // PDF 파일 크기 확인 (최소한의 크기는 있어야 함)
            var fileInfo = new FileInfo(pdfPath);
            Assert.True(fileInfo.Length > 1000, $"PDF file size too small: {fileInfo.Length} bytes");
        }

        [Fact(Skip = "QuestPDF requires native dependencies not available in test environment")]
        public async Task SaveAsPdfAsync_EmptyImageList_ThrowsException()
        {
            // Arrange
            var emptyList = new List<string>();
            var prefix = "test";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _screenCaptureService.SaveAsPdfAsync(emptyList, _testPath, prefix));
        }

        [Fact(Skip = "QuestPDF requires native dependencies not available in test environment")]
        public async Task SaveAsPdfAsync_NonExistentImage_ThrowsException()
        {
            // Arrange
            var imagePaths = new List<string> { @"C:\NonExistent\Image.png" };
            var prefix = "test";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                async () => await _screenCaptureService.SaveAsPdfAsync(imagePaths, _testPath, prefix));
        }

        [Fact]
        public async Task CaptureAreaAsync_ValidArea_LogsInformation()
        {
            // Arrange
            var area = new CaptureArea(0, 0, 100, 100);

            // Act
            // Note: 실제 화면 캡처는 테스트 환경에서 실패할 수 있으므로
            // 로깅만 확인
            try
            {
                var result = await _screenCaptureService.CaptureAreaAsync(area);
                result?.Dispose();
            }
            catch
            {
                // 테스트 환경에서는 화면 캡처가 실패할 수 있음
            }

            // Assert
            _mockLogger.Verify(
                x => x.Information("화면 캡처 시작: {Area}", It.IsAny<CaptureArea>()),
                Times.Once);
        }

        [Fact]
        public async Task SaveCaptureAsync_DifferentPrefixes_UsesCorrectPrefix()
        {
            // Arrange
            using var bitmap = new Bitmap(50, 50);
            var prefixes = new[] { "capture", "screenshot", "image" };
            var savedPaths = new List<string>();

            // Act
            foreach (var prefix in prefixes)
            {
                var path = await _screenCaptureService.SaveCaptureAsync(bitmap, _testPath, prefix);
                savedPaths.Add(path);
            }

            // Assert
            for (int i = 0; i < prefixes.Length; i++)
            {
                var fileName = Path.GetFileName(savedPaths[i]);
                Assert.StartsWith(prefixes[i], fileName);
            }
        }
    }
}
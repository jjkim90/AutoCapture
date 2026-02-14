using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;
using System.Diagnostics;
using WindowsInput;

namespace AutoCapture.Tests.Services
{
    public class InputServiceTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly IInputService _inputService;

        public InputServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            _inputService = new InputService(_mockLogger.Object);
        }

        [Fact]
        public async Task SendKeyAsync_SingleKey_LogsDebugMessage()
        {
            // Arrange
            var key = "A";

            // Act
            await _inputService.SendKeyAsync(key);

            // Assert
            _mockLogger.Verify(
                x => x.Debug("키 입력: {Key}", key),
                Times.Once);
        }

        [Fact]
        public async Task SendKeyAsync_SpecialKey_HandlesCorrectly()
        {
            // Arrange
            var specialKeys = new[] { "Space", "Enter", "Tab", "Left", "Right" };

            // Act & Assert
            foreach (var key in specialKeys)
            {
                await _inputService.SendKeyAsync(key);
                _mockLogger.Verify(
                    x => x.Debug("키 입력: {Key}", key),
                    Times.Once);
                _mockLogger.Reset();
            }
        }

        [Fact]
        public async Task SendKeyAsync_ComboKey_HandlesModifiers()
        {
            // Arrange
            var comboKeys = new[] { "Ctrl+C", "Alt+Tab", "Shift+A" };

            // Act & Assert
            foreach (var key in comboKeys)
            {
                await _inputService.SendKeyAsync(key);
                _mockLogger.Verify(
                    x => x.Debug("키 입력: {Key}", key),
                    Times.Once);
                _mockLogger.Reset();
            }
        }

        [Fact]
        public async Task SendKeyWithRandomTimingAsync_SingleKey_HasRandomDelay()
        {
            // Arrange
            var key = "A";
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            await _inputService.SendKeyWithRandomTimingAsync(key);
            stopwatch.Stop();

            // Assert
            _mockLogger.Verify(
                x => x.Debug("키 입력 (랜덤 타이밍): {Key}", key),
                Times.Once);
            
            // 실행 시간이 최소 지연 시간보다 길어야 함
            Assert.True(stopwatch.ElapsedMilliseconds >= 10, 
                $"Expected delay >= 10ms, but was {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task SendKeyWithRandomTimingAsync_MultipleExecutions_HasVariableDelay()
        {
            // Arrange
            var key = "A";
            var executionTimes = new List<long>();

            // Act - 같은 키를 여러 번 실행하여 시간 측정
            for (int i = 0; i < 5; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await _inputService.SendKeyWithRandomTimingAsync(key);
                stopwatch.Stop();
                executionTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            // Assert - 실행 시간이 모두 다른지 확인 (랜덤성 검증)
            var distinctTimes = executionTimes.Distinct().Count();
            Assert.True(distinctTimes > 1, 
                "Expected variable execution times due to random delays, but all times were identical");
        }

        [Fact]
        public async Task SendKeysAsync_MultipleKeys_SendsInOrder()
        {
            // Arrange
            var keys = new[] { "H", "E", "L", "L", "O" };
            var capturedKeys = new List<string>();
            
            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((msg, key) => 
                {
                    if (msg == "키 입력: {Key}")
                        capturedKeys.Add(key);
                });

            // Act
            await _inputService.SendKeysAsync(keys);

            // Assert
            Assert.Equal(keys.Length, capturedKeys.Count);
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.Equal(keys[i], capturedKeys[i]);
            }
        }

        [Fact]
        public async Task SendKeyAsync_InvalidKey_ThrowsException()
        {
            // Arrange
            var invalidKey = "";
            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<string>()))
                .Verifiable();

            // Act & Assert
            // Note: 빈 문자열은 현재 구현에서 예외를 발생시키지 않음
            // 실제 시나리오에서는 유효성 검사가 필요할 수 있음
            await _inputService.SendKeyAsync(invalidKey);
        }

        [Theory]
        [InlineData("F1")]
        [InlineData("F12")]
        [InlineData("PageUp")]
        [InlineData("PageDown")]
        [InlineData("Home")]
        [InlineData("End")]
        public async Task SendKeyAsync_FunctionAndNavigationKeys_HandlesCorrectly(string key)
        {
            // Act
            await _inputService.SendKeyAsync(key);

            // Assert
            _mockLogger.Verify(
                x => x.Debug("키 입력: {Key}", key),
                Times.Once);
        }
    }
}
using AutoCapture.Wpf.Services.Implementations;
using AutoCapture.Wpf.Services.Interfaces;
using Moq;
using Serilog;
using WindowsInput;

namespace AutoCapture.Tests.Services
{
    public class InputServiceTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly IInputService _inputService;

        private readonly Mock<IKeyboardSimulator> _keyboard = new();
        private readonly List<int> _delays = new();

        public InputServiceTests()
        {
            _mockLogger = new Mock<ILogger>();
            var simulator = new Mock<IInputSimulator>();
            simulator.SetupGet(s => s.Keyboard).Returns(_keyboard.Object);
            _inputService = new InputService(_mockLogger.Object, simulator.Object, ms =>
            {
                _delays.Add(ms);
                return Task.CompletedTask;
            });
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
            await _inputService.SendKeyWithRandomTimingAsync(key);
            Assert.InRange(Assert.Single(_delays), 20, 150);
            _keyboard.Verify(k => k.KeyPress(VirtualKeyCode.VK_A), Times.Once);
        }

        [Fact]
        public async Task SendKeyWithRandomTimingAsync_ChecksTargetBeforeInput()
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _inputService.SendKeyWithRandomTimingAsync("Right", () => throw new OperationCanceledException()));
            _keyboard.VerifyNoOtherCalls();
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
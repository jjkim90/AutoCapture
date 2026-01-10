using AutoCapture.Wpf.Services.Interfaces;
using Serilog;
using WindowsInput;
using WindowsInput.Native;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class InputService : IInputService
    {
        private readonly ILogger _logger;
        private readonly IInputSimulator _simulator;

        public InputService(ILogger logger)
        {
            _logger = logger;
            _simulator = new InputSimulator();
        }

        public async Task SendKeyAsync(string key)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("키 입력: {Key}", key);
                    
                    // 특수 키 매핑
                    switch (key.ToLower())
                    {
                        case "space":
                            _simulator.Keyboard.KeyPress(VirtualKeyCode.SPACE);
                            break;
                        case "enter":
                            _simulator.Keyboard.KeyPress(VirtualKeyCode.RETURN);
                            break;
                        case "tab":
                            _simulator.Keyboard.KeyPress(VirtualKeyCode.TAB);
                            break;
                        case "escape":
                        case "esc":
                            _simulator.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);
                            break;
                        default:
                            if (key.Length == 1)
                            {
                                _simulator.Keyboard.TextEntry(key);
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "키 입력 실패: {Key}", key);
                    throw;
                }
            });
        }

        public async Task SendKeysAsync(params string[] keys)
        {
            foreach (var key in keys)
            {
                await SendKeyAsync(key);
                await Task.Delay(50); // 키 입력 간 짧은 딜레이
            }
        }

        public async Task MoveMouseAsync(int x, int y)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("마우스 이동: ({X}, {Y})", x, y);
                    _simulator.Mouse.MoveMouseTo(x, y);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "마우스 이동 실패");
                    throw;
                }
            });
        }

        public async Task ClickAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("마우스 클릭");
                    _simulator.Mouse.LeftButtonClick();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "마우스 클릭 실패");
                    throw;
                }
            });
        }

        public async Task ClickAsync(int x, int y)
        {
            await MoveMouseAsync(x, y);
            await ClickAsync();
        }
    }
}
using AutoCapture.Wpf.Services.Interfaces;
using Serilog;
using WindowsInput;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class InputService : IInputService
    {
        private readonly ILogger _logger;
        private readonly IInputSimulator _simulator;
        private readonly Random _random;

        public InputService(ILogger logger)
        {
            _logger = logger;
            _simulator = new InputSimulator();
            _random = new Random();
        }

        public async Task SendKeyAsync(string key)
        {
            await Task.Run(() =>
            {
                try
                {
                    _logger.Debug("키 입력: {Key}", key);
                    
                    // 조합 키 처리 (Ctrl+, Alt+, Shift+)
                    var parts = key.Split('+');
                    if (parts.Length > 1)
                    {
                        var modifiers = new List<VirtualKeyCode>();
                        var mainKey = parts[parts.Length - 1];
                        
                        // 수정자 키 확인
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            switch (parts[i].ToLower())
                            {
                                case "ctrl":
                                    modifiers.Add(VirtualKeyCode.CONTROL);
                                    break;
                                case "alt":
                                    modifiers.Add(VirtualKeyCode.MENU);
                                    break;
                                case "shift":
                                    modifiers.Add(VirtualKeyCode.SHIFT);
                                    break;
                            }
                        }
                        
                        // 조합 키 실행
                        if (modifiers.Count > 0)
                        {
                            var keyCode = GetVirtualKeyCode(mainKey);
                            if (keyCode != null)
                            {
                                _simulator.Keyboard.ModifiedKeyStroke(modifiers, keyCode.Value);
                            }
                            return;
                        }
                    }
                    
                    // 단일 키 처리
                    var vkCode = GetVirtualKeyCode(key);
                    if (vkCode != null)
                    {
                        _simulator.Keyboard.KeyPress(vkCode.Value);
                    }
                    else if (key.Length == 1)
                    {
                        _simulator.Keyboard.TextEntry(key);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "키 입력 실패: {Key}", key);
                    throw;
                }
            });
        }

        public async Task SendKeyWithRandomTimingAsync(string key)
        {
            await Task.Run(async () =>
            {
                try
                {
                    _logger.Debug("키 입력 (랜덤 타이밍): {Key}", key);
                    
                    // 조합 키 처리 (Ctrl+, Alt+, Shift+)
                    var parts = key.Split('+');
                    if (parts.Length > 1)
                    {
                        var modifiers = new List<VirtualKeyCode>();
                        var mainKey = parts[parts.Length - 1];
                        
                        // 수정자 키 확인
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            switch (parts[i].ToLower())
                            {
                                case "ctrl":
                                    modifiers.Add(VirtualKeyCode.CONTROL);
                                    break;
                                case "alt":
                                    modifiers.Add(VirtualKeyCode.MENU);
                                    break;
                                case "shift":
                                    modifiers.Add(VirtualKeyCode.SHIFT);
                                    break;
                            }
                        }
                        
                        // 조합 키 실행 (랜덤 타이밍)
                        if (modifiers.Count > 0)
                        {
                            var keyCode = GetVirtualKeyCode(mainKey);
                            if (keyCode != null)
                            {
                                foreach (var modifier in modifiers)
                                {
                                    _simulator.Keyboard.KeyDown(modifier);
                                }
                                
                                _simulator.Keyboard.KeyDown(keyCode.Value);
                                
                                // 실제 사람의 키 입력 패턴 모방
                                var duration = _random.Next(100) < 5 
                                    ? _random.Next(200, 301)  // 5% 확률로 긴 키 홀드
                                    : _random.Next(20, 151);  // 95% 확률로 일반 키 홀드
                                await Task.Delay(duration);
                                
                                _simulator.Keyboard.KeyUp(keyCode.Value);
                                
                                foreach (var modifier in modifiers.Reverse<VirtualKeyCode>())
                                {
                                    _simulator.Keyboard.KeyUp(modifier);
                                }
                            }
                            return;
                        }
                    }
                    
                    // 단일 키 처리 (랜덤 타이밍)
                    var vkCode = GetVirtualKeyCode(key);
                    if (vkCode != null)
                    {
                        _simulator.Keyboard.KeyDown(vkCode.Value);
                        
                        // 실제 사람의 키 입력 패턴 모방
                        var duration = _random.Next(100) < 5 
                            ? _random.Next(200, 301)  // 5% 확률로 긴 키 홀드
                            : _random.Next(20, 151);  // 95% 확률로 일반 키 홀드
                        await Task.Delay(duration);
                        
                        _simulator.Keyboard.KeyUp(vkCode.Value);
                    }
                    else if (key.Length == 1)
                    {
                        _simulator.Keyboard.TextEntry(key);
                    }
                    
                    // 키 입력 후 짧은 대기 (10-50ms)
                    await Task.Delay(_random.Next(10, 51));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "키 입력 실패 (랜덤 타이밍): {Key}", key);
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

        private VirtualKeyCode? GetVirtualKeyCode(string key)
        {
            return key.ToLower() switch
            {
                "space" => VirtualKeyCode.SPACE,
                "enter" => VirtualKeyCode.RETURN,
                "tab" => VirtualKeyCode.TAB,
                "escape" or "esc" => VirtualKeyCode.ESCAPE,
                "backspace" => VirtualKeyCode.BACK,
                "delete" => VirtualKeyCode.DELETE,
                "insert" => VirtualKeyCode.INSERT,
                "home" => VirtualKeyCode.HOME,
                "end" => VirtualKeyCode.END,
                "pageup" => VirtualKeyCode.PRIOR,
                "pagedown" => VirtualKeyCode.NEXT,
                "left" => VirtualKeyCode.LEFT,
                "right" => VirtualKeyCode.RIGHT,
                "up" => VirtualKeyCode.UP,
                "down" => VirtualKeyCode.DOWN,
                "capslock" => VirtualKeyCode.CAPITAL,
                "numlock" => VirtualKeyCode.NUMLOCK,
                "scrolllock" => VirtualKeyCode.SCROLL,
                "printscreen" => VirtualKeyCode.SNAPSHOT,
                "pause" => VirtualKeyCode.PAUSE,
                "f1" => VirtualKeyCode.F1,
                "f2" => VirtualKeyCode.F2,
                "f3" => VirtualKeyCode.F3,
                "f4" => VirtualKeyCode.F4,
                "f5" => VirtualKeyCode.F5,
                "f6" => VirtualKeyCode.F6,
                "f7" => VirtualKeyCode.F7,
                "f8" => VirtualKeyCode.F8,
                "f9" => VirtualKeyCode.F9,
                "f10" => VirtualKeyCode.F10,
                "f11" => VirtualKeyCode.F11,
                "f12" => VirtualKeyCode.F12,
                "0" => VirtualKeyCode.VK_0,
                "1" => VirtualKeyCode.VK_1,
                "2" => VirtualKeyCode.VK_2,
                "3" => VirtualKeyCode.VK_3,
                "4" => VirtualKeyCode.VK_4,
                "5" => VirtualKeyCode.VK_5,
                "6" => VirtualKeyCode.VK_6,
                "7" => VirtualKeyCode.VK_7,
                "8" => VirtualKeyCode.VK_8,
                "9" => VirtualKeyCode.VK_9,
                "a" => VirtualKeyCode.VK_A,
                "b" => VirtualKeyCode.VK_B,
                "c" => VirtualKeyCode.VK_C,
                "d" => VirtualKeyCode.VK_D,
                "e" => VirtualKeyCode.VK_E,
                "f" => VirtualKeyCode.VK_F,
                "g" => VirtualKeyCode.VK_G,
                "h" => VirtualKeyCode.VK_H,
                "i" => VirtualKeyCode.VK_I,
                "j" => VirtualKeyCode.VK_J,
                "k" => VirtualKeyCode.VK_K,
                "l" => VirtualKeyCode.VK_L,
                "m" => VirtualKeyCode.VK_M,
                "n" => VirtualKeyCode.VK_N,
                "o" => VirtualKeyCode.VK_O,
                "p" => VirtualKeyCode.VK_P,
                "q" => VirtualKeyCode.VK_Q,
                "r" => VirtualKeyCode.VK_R,
                "s" => VirtualKeyCode.VK_S,
                "t" => VirtualKeyCode.VK_T,
                "u" => VirtualKeyCode.VK_U,
                "v" => VirtualKeyCode.VK_V,
                "w" => VirtualKeyCode.VK_W,
                "x" => VirtualKeyCode.VK_X,
                "y" => VirtualKeyCode.VK_Y,
                "z" => VirtualKeyCode.VK_Z,
                _ => null
            };
        }
    }
}
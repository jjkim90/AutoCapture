using AutoCapture.Wpf.Services.Interfaces;
using Serilog;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class HotkeyService : IHotkeyService
    {
        private readonly ILogger _logger;
        private readonly Dictionary<int, HotkeyInfo> _hotkeys = new();
        private HwndSource? _source;
        private IntPtr _windowHandle;
        private int _currentId = 1;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        public HotkeyService(ILogger logger)
        {
            _logger = logger;
        }

        private void InitializeWindow()
        {
            if (_windowHandle != IntPtr.Zero) return;
            
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow == null) return;
            
            var helper = new WindowInteropHelper(mainWindow);
            if (helper.Handle == IntPtr.Zero)
            {
                mainWindow.SourceInitialized += (s, e) =>
                {
                    _windowHandle = helper.Handle;
                    _source = HwndSource.FromHwnd(_windowHandle);
                    _source?.AddHook(HwndHook);
                };
            }
            else
            {
                _windowHandle = helper.Handle;
                _source = HwndSource.FromHwnd(_windowHandle);
                _source?.AddHook(HwndHook);
            }
        }

        public bool RegisterHotkey(Key key, ModifierKeys modifiers, Action callback)
        {
            try
            {
                InitializeWindow();
                
                if (_windowHandle == IntPtr.Zero)
                {
                    _logger.Warning("윈도우 핸들이 초기화되지 않았습니다.");
                    return false;
                }
                
                var id = _currentId++;
                var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                var modifierFlags = GetModifierFlags(modifiers);

                if (RegisterHotKey(_windowHandle, id, modifierFlags, virtualKey))
                {
                    _hotkeys[id] = new HotkeyInfo
                    {
                        Key = key,
                        Modifiers = modifiers,
                        Callback = callback
                    };

                    _logger.Information("단축키 등록 성공: {Key}+{Modifiers}", key, modifiers);
                    return true;
                }

                _logger.Warning("단축키 등록 실패: {Key}+{Modifiers}", key, modifiers);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "단축키 등록 중 오류 발생");
                return false;
            }
        }

        public bool UnregisterHotkey(Key key, ModifierKeys modifiers)
        {
            try
            {
                var hotkeyToRemove = _hotkeys.FirstOrDefault(h => 
                    h.Value.Key == key && h.Value.Modifiers == modifiers);

                if (hotkeyToRemove.Value != null)
                {
                    if (UnregisterHotKey(_windowHandle, hotkeyToRemove.Key))
                    {
                        _hotkeys.Remove(hotkeyToRemove.Key);
                        _logger.Information("단축키 해제: {Key}+{Modifiers}", key, modifiers);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "단축키 해제 중 오류 발생");
                return false;
            }
        }

        public void UnregisterAll()
        {
            foreach (var id in _hotkeys.Keys.ToList())
            {
                UnregisterHotKey(_windowHandle, id);
            }
            _hotkeys.Clear();
            _logger.Information("모든 단축키 해제 완료");
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (_hotkeys.TryGetValue(id, out var hotkeyInfo))
                {
                    hotkeyInfo.Callback?.Invoke();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private uint GetModifierFlags(ModifierKeys modifiers)
        {
            uint flags = MOD_NONE;

            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                flags |= MOD_ALT;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                flags |= MOD_CONTROL;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                flags |= MOD_SHIFT;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                flags |= MOD_WIN;

            return flags;
        }

        public void Dispose()
        {
            UnregisterAll();
            _source?.RemoveHook(HwndHook);
            _source?.Dispose();
        }

        private class HotkeyInfo
        {
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
            public Action? Callback { get; set; }
        }
    }
}
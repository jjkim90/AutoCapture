using System.Windows.Input;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IHotkeyService : IDisposable
    {
        bool RegisterHotkey(Key key, ModifierKeys modifiers, Action callback);
        bool UnregisterHotkey(Key key, ModifierKeys modifiers);
        void UnregisterAll();
    }
}
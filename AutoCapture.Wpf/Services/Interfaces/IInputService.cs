namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IInputService
    {
        Task SendKeyAsync(string key);
        Task SendKeysAsync(params string[] keys);
        Task MoveMouseAsync(int x, int y);
        Task ClickAsync();
        Task ClickAsync(int x, int y);
    }
}
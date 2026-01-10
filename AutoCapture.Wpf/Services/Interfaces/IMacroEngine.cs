using AutoCapture.Wpf.Models;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IMacroEngine
    {
        event EventHandler<MacroProgressEventArgs>? ProgressChanged;
        event EventHandler<MacroCompletedEventArgs>? Completed;
        event EventHandler<MacroErrorEventArgs>? Error;

        bool IsRunning { get; }
        bool IsPaused { get; }

        Task StartAsync(CaptureSettings settings, CaptureArea area, CancellationToken cancellationToken = default);
        Task StopAsync();
        Task PauseAsync();
        Task ResumeAsync();
    }

    public class MacroProgressEventArgs : EventArgs
    {
        public int CurrentCount { get; set; }
        public int TotalCount { get; set; }
        public string? Message { get; set; }
    }

    public class MacroCompletedEventArgs : EventArgs
    {
        public int TotalCaptured { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public List<string>? CapturedFiles { get; set; }
    }

    public class MacroErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string? Message { get; set; }

        public MacroErrorEventArgs(Exception exception, string? message = null)
        {
            Exception = exception;
            Message = message;
        }
    }
}
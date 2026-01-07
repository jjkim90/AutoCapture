namespace AutoCapture.Wpf.Models
{
    public enum MacroStatus
    {
        Idle,
        Running,
        Paused,
        Stopping,
        Completed,
        Error
    }

    public class MacroState
    {
        public MacroStatus Status { get; set; } = MacroStatus.Idle;
        public int CurrentCaptureCount { get; set; } = 0;
        public int TotalCaptureCount { get; set; } = 0;
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? LastError { get; set; }

        public bool IsRunning => Status == MacroStatus.Running;
        public bool CanStart => Status == MacroStatus.Idle || Status == MacroStatus.Completed || Status == MacroStatus.Error;
        public bool CanStop => Status == MacroStatus.Running || Status == MacroStatus.Paused;

        public TimeSpan ElapsedTime
        {
            get
            {
                if (StartTime == null) return TimeSpan.Zero;
                var endTime = EndTime ?? DateTime.Now;
                return endTime - StartTime.Value;
            }
        }

        public void Reset()
        {
            Status = MacroStatus.Idle;
            CurrentCaptureCount = 0;
            TotalCaptureCount = 0;
            StartTime = null;
            EndTime = null;
            LastError = null;
        }
    }
}
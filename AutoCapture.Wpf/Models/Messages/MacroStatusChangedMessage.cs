namespace AutoCapture.Wpf.Models.Messages
{
    public class MacroStatusChangedMessage
    {
        public MacroStatus Status { get; }
        public string? Message { get; }

        public MacroStatusChangedMessage(MacroStatus status, string? message = null)
        {
            Status = status;
            Message = message;
        }
    }
}
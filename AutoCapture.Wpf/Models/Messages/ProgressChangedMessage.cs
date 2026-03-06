using System.Windows.Shell;

namespace AutoCapture.Wpf.Models.Messages
{
    public class ProgressChangedMessage
    {
        public double Value { get; }
        public TaskbarItemProgressState State { get; }

        public ProgressChangedMessage(double value, TaskbarItemProgressState state = TaskbarItemProgressState.Normal)
        {
            Value = value;
            State = state;
        }
    }
}
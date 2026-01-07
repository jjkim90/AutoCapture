namespace AutoCapture.Wpf.Models
{
    public class CaptureSettings
    {
        public int CaptureCount { get; set; } = 10;
        public int IntervalSeconds { get; set; } = 5;
        public string SaveFolderPath { get; set; } = string.Empty;
        public string AutoInputKey { get; set; } = "Space";
        public bool SaveAsPdf { get; set; } = false;

        public bool IsValid()
        {
            return CaptureCount > 0 
                && IntervalSeconds > 0 
                && !string.IsNullOrWhiteSpace(SaveFolderPath)
                && System.IO.Directory.Exists(SaveFolderPath);
        }
    }
}
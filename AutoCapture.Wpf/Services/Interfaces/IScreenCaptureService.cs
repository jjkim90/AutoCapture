using AutoCapture.Wpf.Models;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IScreenCaptureService
    {
        Task<string?> CaptureAndSaveAsync(CaptureArea area, string folderPath, string fileNamePrefix = "capture", Func<Task>? beforeCapture = null);
        Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath, string fileNamePrefix = "capture");
    }
}
using AutoCapture.Wpf.Models;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IScreenCaptureService
    {
        Task<string?> CaptureAndSaveAsync(CaptureArea area, string folderPath, string fileNamePrefix = "capture");
        Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath, string fileNamePrefix = "capture");
    }
}
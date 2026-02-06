using AutoCapture.Wpf.Models;
using System.Drawing;

namespace AutoCapture.Wpf.Services.Interfaces
{
    public interface IScreenCaptureService
    {
        Task<Bitmap?> CaptureAreaAsync(CaptureArea area);
        Task<string> SaveCaptureAsync(Bitmap bitmap, string folderPath, string fileNamePrefix = "capture");
        Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath, string fileNamePrefix = "capture");
    }
}
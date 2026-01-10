using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Serilog;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly ILogger _logger;

        public ScreenCaptureService(ILogger logger)
        {
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<Bitmap?> CaptureAreaAsync(CaptureArea area)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.Information("화면 캡처 시작: {Area}", area);

                    var bitmap = new Bitmap(area.Width, area.Height);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.CopyFromScreen(
                            area.X, area.Y,
                            0, 0,
                            new System.Drawing.Size(area.Width, area.Height),
                            CopyPixelOperation.SourceCopy);
                    }

                    _logger.Information("화면 캡처 완료");
                    return bitmap;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "화면 캡처 실패");
                    return null;
                }
            });
        }

        public async Task<string> SaveCaptureAsync(Bitmap bitmap, string folderPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine(folderPath, fileName);

                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                    _logger.Information("캡처 이미지 저장 완료: {FilePath}", filePath);

                    return filePath;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "캡처 이미지 저장 실패");
                    throw;
                }
            });
        }

        public async Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var pdfFileName = $"capture_collection_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    var pdfPath = Path.Combine(folderPath, pdfFileName);

                    Document.Create(container =>
                    {
                        foreach (var imagePath in imagePaths.Where(File.Exists))
                        {
                            container.Page(page =>
                            {
                                page.Size(QuestPDF.Helpers.PageSizes.A4);
                                page.Margin(1, Unit.Centimetre);
                                page.Content().Image(imagePath);
                            });
                        }
                    }).GeneratePdf(pdfPath);

                    _logger.Information("PDF 생성 완료: {PdfPath}", pdfPath);
                    return pdfPath;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "PDF 생성 실패");
                    throw;
                }
            });
        }
    }
}
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

        public async Task<string?> CaptureAndSaveAsync(CaptureArea area, string folderPath, string fileNamePrefix = "capture")
        {
            const int maxRetries = 3;
            const int retryDelayMs = 500;
            
            return await Task.Run(async () =>
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger.Information("화면 캡처 시도 {Attempt}/{MaxAttempts}: {Area}", attempt, maxRetries, area);

                        // 폴더가 없으면 생성
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        // 파일 경로 생성
                        var fileName = $"{fileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        var filePath = Path.Combine(folderPath, fileName);

                        // Bitmap을 using으로 감싸서 자동 Dispose 보장
                        using (var bitmap = new Bitmap(area.Width, area.Height))
                        {
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.CopyFromScreen(
                                    area.X, area.Y,
                                    0, 0,
                                    new System.Drawing.Size(area.Width, area.Height),
                                    CopyPixelOperation.SourceCopy);
                            }

                            // 메모리 스트림 없이 직접 파일로 저장
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                        }

                        _logger.Information("화면 캡처 및 저장 성공 (시도 {Attempt}): {FilePath}", attempt, filePath);
                        return filePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "화면 캡처 실패 (시도 {Attempt}/{MaxAttempts})", attempt, maxRetries);
                        
                        if (attempt < maxRetries)
                        {
                            _logger.Information("재시도 대기 중... {DelayMs}ms", retryDelayMs);
                            await Task.Delay(retryDelayMs);
                        }
                        else
                        {
                            _logger.Error(ex, "화면 캡처 최종 실패: 모든 재시도 소진");
                            return null;
                        }
                    }
                }
                
                return null;
            });
        }

        public async Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath, string fileNamePrefix = "capture")
        {
            return await Task.Run(() =>
            {
                try
                {
                    var pdfFileName = $"{fileNamePrefix}_collection_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    var pdfPath = Path.Combine(folderPath, pdfFileName);
                    
                    // 유효한 이미지 경로만 필터링
                    var validImagePaths = imagePaths.Where(File.Exists).ToList();
                    _logger.Information("PDF 생성 시작: {ValidCount}/{TotalCount} 이미지", 
                        validImagePaths.Count, imagePaths.Count);

                    // 메모리 효율을 위해 청크 단위로 처리
                    const int chunkSize = 50;
                    using (var pdfStream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write))
                    {
                        Document.Create(container =>
                        {
                            for (int i = 0; i < validImagePaths.Count; i++)
                            {
                                var imagePath = validImagePaths[i];
                                
                                container.Page(page =>
                                {
                                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                                    page.Margin(1, Unit.Centimetre);
                                    
                                    // 메모리 사용량 최적화를 위해 스트림으로 처리
                                    page.Content().Image(imagePath);
                                });
                                
                                // 주기적 메모리 정리
                                if ((i + 1) % chunkSize == 0)
                                {
                                    _logger.Information("PDF 생성 진행: {Processed}/{Total} 이미지 처리됨", 
                                        i + 1, validImagePaths.Count);
                                    GC.Collect();
                                }
                            }
                        }).GeneratePdf(pdfStream);
                    }

                    _logger.Information("PDF 생성 완료: {PdfPath}, 크기: {SizeMB:F2}MB", 
                        pdfPath, new FileInfo(pdfPath).Length / 1024.0 / 1024.0);
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
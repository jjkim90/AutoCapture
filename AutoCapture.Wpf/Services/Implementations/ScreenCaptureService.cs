using AutoCapture.Wpf.Models;
using AutoCapture.Wpf.Services.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Serilog;
using System.Drawing;
using ImageFormat = System.Drawing.Imaging.ImageFormat;
using System.IO;

namespace AutoCapture.Wpf.Services.Implementations
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        private readonly ILogger _logger;

        private readonly Action<Graphics, CaptureArea> _copy;

        public ScreenCaptureService(ILogger logger)
            : this(logger, (graphics, area) => graphics.CopyFromScreen(area.X, area.Y, 0, 0,
                new System.Drawing.Size(area.Width, area.Height), CopyPixelOperation.SourceCopy)) { }

        internal ScreenCaptureService(ILogger logger, Action<Graphics, CaptureArea> copy)
        {
            _copy = copy;
            _logger = logger;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<string?> CaptureAndSaveAsync(CaptureArea area, string folderPath, string fileNamePrefix = "capture", Func<Task>? beforeCapture = null)
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

                        using var bitmap = new Bitmap(area.Width, area.Height);
                        using (var graphics = Graphics.FromImage(bitmap))
                        {
                            if (beforeCapture != null) await beforeCapture();
                            _copy(graphics, area);
                        }
                        var filePath = WriteOutput(folderPath, fileNamePrefix, ".png",
                            stream => bitmap.Save(stream, ImageFormat.Png));

                        _logger.Information("화면 캡처 및 저장 성공 (시도 {Attempt}): {FilePath}", attempt, filePath);
                        return filePath;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
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

        private static string WriteOutput(string folder, string prefix, string extension, Action<Stream> write)
        {
            string stem = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var temporary = Path.Combine(folder, Path.GetRandomFileName());
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            try
            {
                using (stream)
                {
                    write(stream);
                }
                for (int index = 1; ; index++)
                {
                    var name = index == 1 ? stem : $"{stem}_{index:D3}";
                    var path = Path.Combine(folder, name + extension);
                    try
                    {
                        File.Move(temporary, path);
                        return path;
                    }
                    catch (IOException) when (File.Exists(path))
                    {
                        // A previous capture already owns this timestamp.
                    }
                }
            }
            finally { File.Delete(temporary); }
        }

        public async Task<string> SaveAsPdfAsync(List<string> imagePaths, string folderPath, string fileNamePrefix = "capture")
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (imagePaths.Count == 0)
                        throw new ArgumentException("PDF에 넣을 이미지가 없습니다.", nameof(imagePaths));
                    foreach (var path in imagePaths)
                    {
                        if (!File.Exists(path))
                            throw new FileNotFoundException("PDF 원본 이미지를 찾을 수 없습니다.", path);
                    }

                    // 메모리 효율을 위해 청크 단위로 처리
                    const int chunkSize = 50;
                    var pdfPath = WriteOutput(folderPath, fileNamePrefix + "_collection", ".pdf", pdfStream =>
                    {
                        Document.Create(container =>
                        {
                            for (int i = 0; i < imagePaths.Count; i++)
                            {
                                var imagePath = imagePaths[i];
                                
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
                                        i + 1, imagePaths.Count);
                                    GC.Collect();
                                }
                            }
                        }).GeneratePdf(pdfStream);
                    });

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
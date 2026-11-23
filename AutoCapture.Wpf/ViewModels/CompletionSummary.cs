using AutoCapture.Wpf.Services.Interfaces;
using System.IO;

namespace AutoCapture.Wpf.ViewModels
{
    /// <summary>완료 결과를 HUD 카드와 메인 화면 배너에 같은 문구로 보여주기 위한 요약</summary>
    public sealed record CompletionSummary(string Title, string Detail, string Folder, string? PdfPath)
    {
        public bool HasPdf => !string.IsNullOrEmpty(PdfPath);

        public static CompletionSummary From(MacroCompletedEventArgs e, string fallbackFolder)
        {
            var parts = new List<string> { $"PNG {e.TotalCaptured}" };
            if (!string.IsNullOrEmpty(e.PdfPath)) parts.Add("PDF 1");
            parts.Add(CaptureSettingsViewModel.FormatDuration(e.ElapsedTime));

            // 실제로 저장한 위치를 우선한다. 실행 중에 설정을 바꿔도 결과 폴더가 맞게 열린다.
            var folder = e.CapturedFiles is { Count: > 0 } files
                ? Path.GetDirectoryName(files[^1]) ?? fallbackFolder
                : fallbackFolder;
            return new CompletionSummary($"{e.TotalCaptured}장 저장 완료", string.Join(" · ", parts), folder, e.PdfPath);
        }
    }
}

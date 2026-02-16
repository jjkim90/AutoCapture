using System.IO;

namespace AutoCapture.Wpf.Models
{
    public class MacroErrorInfo
    {
        public int CaptureIndex { get; set; }
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public bool IsRecoverable { get; set; }
    }

    public static class ErrorMessages
    {
        public static string GetUserFriendlyMessage(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => "파일 또는 폴더에 접근할 수 없습니다.\n관리자 권한으로 실행하거나 폴더 권한을 확인해주세요.",
                DirectoryNotFoundException => "지정된 저장 폴더를 찾을 수 없습니다.\n저장 경로를 확인해주세요.",
                IOException ioEx when ioEx.Message.Contains("disk") => "디스크 공간이 부족합니다.\n저장 위치의 여유 공간을 확인해주세요.",
                IOException => "파일 저장 중 오류가 발생했습니다.\n다른 프로그램이 파일을 사용 중인지 확인해주세요.",
                OutOfMemoryException => "메모리가 부족합니다.\n다른 프로그램을 종료하고 다시 시도해주세요.",
                InvalidOperationException when ex.Message.Contains("GDI+") => "화면 캡처에 실패했습니다.\n화면 보호기나 전체화면 프로그램을 종료하고 다시 시도해주세요.",
                _ => $"예기치 않은 오류가 발생했습니다.\n오류: {ex.GetType().Name}\n{ex.Message}"
            };
        }

        public static string GetRecoveryAdvice(Exception ex)
        {
            return ex switch
            {
                UnauthorizedAccessException => "• 프로그램을 관리자 권한으로 실행\n• 저장 폴더의 쓰기 권한 확인",
                DirectoryNotFoundException => "• 저장 경로 재설정\n• 폴더 수동 생성",
                IOException ioEx when ioEx.Message.Contains("disk") => "• 디스크 정리 실행\n• 다른 드라이브 선택",
                IOException => "• 저장 폴더 변경\n• 파일 이름 변경",
                OutOfMemoryException => "• 캡처 크기 축소\n• 캡처 간격 늘리기",
                InvalidOperationException when ex.Message.Contains("GDI+") => "• 전체화면 프로그램 종료\n• 화면 해상도 확인",
                _ => "• 프로그램 재시작\n• 컴퓨터 재부팅"
            };
        }
    }
}
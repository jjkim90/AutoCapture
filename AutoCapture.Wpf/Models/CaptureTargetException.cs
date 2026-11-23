namespace AutoCapture.Wpf.Models
{
    /// <summary>
    /// 입력 대상 창이 없거나 닫혀서 계속할 수 없을 때. 사용자가 고칠 수 있는 상황이라
    /// "예기치 않은 오류" 대신 대상 창을 다시 열라는 안내로 이어진다.
    /// </summary>
    public sealed class CaptureTargetException(string message) : InvalidOperationException(message);
}

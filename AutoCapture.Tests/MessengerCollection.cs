namespace AutoCapture.Tests
{
    /// <summary>
    /// 전역 WeakReferenceMessenger로 메시지를 주고받는 테스트는 병렬로 돌면 서로의 상태를 바꾼다.
    /// 이 컬렉션에 넣어 순서대로 실행한다.
    /// </summary>
    [CollectionDefinition(Name)]
    public class MessengerCollection
    {
        public const string Name = "Messenger";
    }
}

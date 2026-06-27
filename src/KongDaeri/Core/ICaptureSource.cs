namespace KongDaeri.Core;

// 입력 소스: 출력과 생명주기만 통일 (트리거는 내부 자유)
public interface ICaptureSource
{
    string Name { get; }
    CaptureSourceType Type { get; }
    event EventHandler<CaptureItem> Captured;   // 모든 소스가 push로 통일
    void Start();
    void Stop();
}

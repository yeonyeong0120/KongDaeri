namespace KongDaeri.Core;

public enum CaptureSourceType { Clipboard, ScreenSnip, File }
public enum CaptureStatus { Collected, Processing, Processed, Exported, Failed }

public record CaptureItem
{
    public Guid Id { get; init; }
    public CaptureSourceType SourceType { get; init; }
    public DateTime CapturedAt { get; init; }

    // 원본
    public string? RawText { get; init; }       // 클립보드/파일 텍스트
    public string? ImagePath { get; init; }     // 스니핑 이미지 경로
    public string? SourceContext { get; init; } // 활성 창 제목, 파일 경로 등

    // AI 처리 결과
    public string? AiTitle { get; set; }
    public string? AiMarkdown { get; set; }
    public string[]? AiTags { get; set; }

    // 상태/내보내기
    public CaptureStatus Status { get; set; }
    public string? NotionPageId { get; set; }
}

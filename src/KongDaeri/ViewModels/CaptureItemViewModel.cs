using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KongDaeri.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace KongDaeri.ViewModels;

/// <summary>리스트/상세에 바인딩되는 단일 수집 항목 뷰모델(CaptureItem 래퍼).</summary>
public partial class CaptureItemViewModel : ObservableObject
{
    public CaptureItem Model { get; }

    public CaptureItemViewModel(CaptureItem model) => Model = model;

    public Guid Id => Model.Id;

    public string Title =>
        !string.IsNullOrWhiteSpace(Model.AiTitle) ? Model.AiTitle!
        : HasImage ? (Model.SourceContext ?? "화면 스니핑")   // 이미지 항목은 출처 문구로 폴백
        : Preview(Model.RawText, 30);

    public string CapturedAtText => Model.CapturedAt.ToString("MM-dd HH:mm");

    public string SourceText => Model.SourceType switch
    {
        CaptureSourceType.Clipboard => "클립보드",
        CaptureSourceType.ScreenSnip => "스니핑",
        CaptureSourceType.File => "파일",
        _ => "기타",
    };

    public string StatusText => Model.Status switch
    {
        CaptureStatus.Collected => "수집됨",
        CaptureStatus.Processing => "정리중",
        CaptureStatus.Processed => "정리완료",
        CaptureStatus.Exported => "노션전송",
        CaptureStatus.Failed => "실패",
        _ => "?",
    };

    public Brush StatusBrush => new SolidColorBrush(Model.Status switch
    {
        CaptureStatus.Collected => Color.FromRgb(0x9E, 0x9E, 0x9E),  // 회색
        CaptureStatus.Processing => Color.FromRgb(0xFB, 0x8C, 0x00), // 주황
        CaptureStatus.Processed => Color.FromRgb(0x1E, 0x88, 0xE5),  // 파랑
        CaptureStatus.Exported => Color.FromRgb(0x43, 0xA0, 0x47),   // 초록
        CaptureStatus.Failed => Color.FromRgb(0xE5, 0x39, 0x35),     // 빨강
        _ => Colors.Gray,
    });

    public string? RawText => Model.RawText;
    public string? ImagePath => Model.ImagePath;
    public bool HasImage => !string.IsNullOrEmpty(Model.ImagePath);
    public bool IsTextOriginal => !HasImage;   // 텍스트 원본 표시 여부(이미지 항목이 아니면 true)

    public string? AiTitle => Model.AiTitle;
    public string AiTagsText => Model.AiTags is { Length: > 0 } ? string.Join(", ", Model.AiTags) : "(없음)";
    public string AiMarkdown => Model.AiMarkdown ?? "";
    public bool HasAi => !string.IsNullOrWhiteSpace(Model.AiMarkdown);

    /// <summary>모델 값이 바뀌었을 때 모든 바인딩을 새로고침.</summary>
    public void RaiseAllChanged() => OnPropertyChanged(string.Empty);

    private static string Preview(string? s, int len)
    {
        if (string.IsNullOrWhiteSpace(s)) return "(빈 항목)";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > len ? s[..len] + "…" : s;
    }
}

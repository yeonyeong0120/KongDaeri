namespace KongDaeri.Core;

public interface IAiProcessor      // Gemini 분류·요약·마크다운
{
    Task<CaptureItem> ProcessAsync(CaptureItem item);
}

using System.IO;

namespace KongDaeri.Core;

/// <summary>
/// 캡처 → 저장 → AI 정리 → 노션 전송 파이프라인의 조정자.
/// 트레이/수집함 창이 공유하며, 항목 추가/변경/삭제와 상태 메시지를 이벤트로 알린다.
/// (기존 IStorage / IAiProcessor / NotionExporter 를 그대로 재사용)
/// </summary>
public sealed class CaptureService
{
    private readonly IStorage _storage;
    private IAiProcessor? _ai;
    private NotionExporter? _notion;

    public CaptureService(IStorage storage, IAiProcessor? ai, NotionExporter? notion)
    {
        _storage = storage;
        _ai = ai;
        _notion = notion;
    }

    public bool AiEnabled => _ai is not null;
    public bool NotionEnabled => _notion is not null;

    /// <summary>설정 변경 후 AI/노션 처리기를 교체(즉시 반영). 구독·저장소는 그대로 유지.</summary>
    public void SetProcessors(IAiProcessor? ai, NotionExporter? notion)
    {
        _ai = ai;
        _notion = notion;
    }

    public event EventHandler<CaptureItem>? ItemAdded;
    public event EventHandler<CaptureItem>? ItemUpdated;
    public event EventHandler<Guid>? ItemDeleted;
    public event EventHandler<string>? StatusMessage;   // 데스펫 말풍선용

    public Task<IReadOnlyList<CaptureItem>> ListAsync() => _storage.ListAsync();

    /// <summary>
    /// 새 캡처 처리: 원본을 Collected 로 저장만 한다(자동 AI 호출 없음).
    /// AI 정리는 수집함에서 [AI 정리]/[노션 공유]로 수동 트리거한다.
    /// </summary>
    public async Task HandleCapturedAsync(CaptureItem item)
    {
        // 텍스트 수집 경로에만 민감정보 필터 적용(스니핑 이미지는 RawText 없음 → 통과).
        if (!string.IsNullOrEmpty(item.RawText))
        {
            var filter = SensitiveDataFilter.Inspect(item.RawText);
            if (filter.Blocked)
            {
                AppLog.Line($"[민감정보 차단] 유형={filter.BlockType}");   // 값은 절대 기록하지 않음
                StatusMessage?.Invoke(this, "민감정보라 담지 않았어요");
                return;   // 저장하지 않음(DB·로그에 원문 안 남김)
            }
            if (filter.MaskedText != item.RawText)
            {
                item = item with { RawText = filter.MaskedText };   // 마스킹본으로 교체
            }
        }

        await _storage.SaveAsync(item);
        var count = (await _storage.ListAsync()).Count;
        LogCapture(count, item);

        ItemAdded?.Invoke(this, item);
        StatusMessage?.Invoke(this, "주워 담았어요");
    }

    /// <summary>AI 정리: Processing → (성공)Processed / (실패)Failed.</summary>
    public async Task ProcessWithAiAsync(CaptureItem item)
    {
        if (_ai is null) return;

        try
        {
            item.Status = CaptureStatus.Processing;
            await _storage.UpdateAsync(item);
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, "정리 중…");

            await _ai.ProcessAsync(item);
            await _storage.UpdateAsync(item);
            LogAiResult(item);
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, "정리 끝!");
        }
        catch (Exception ex)
        {
            item.Status = CaptureStatus.Failed;
            try { await _storage.UpdateAsync(item); } catch { /* 업데이트 실패 무시 */ }
            AppLog.Line($"[AI 실패] {ex.GetType().Name}: {ex.Message}");
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, "앗, 실패했어요");
        }
    }

    /// <summary>항목을 노션으로 전송. 정리 전이면 먼저 AI 정리부터 시도한다.</summary>
    public async Task ExportAsync(CaptureItem item)
    {
        if (_notion is null)
        {
            throw new InvalidOperationException(
                "노션이 비활성화됨(토큰/페이지ID 확인). settings.json 을 점검하세요.");
        }

        // 아직 정리 전이면 먼저 AI 정리.
        if (item.Status != CaptureStatus.Processed && item.Status != CaptureStatus.Exported && _ai is not null)
        {
            await ProcessWithAiAsync(item);
        }
        if (string.IsNullOrWhiteSpace(item.AiMarkdown))
        {
            throw new InvalidOperationException("아직 AI 정리가 안 된 항목이라 노션에 보낼 내용이 없습니다.");
        }

        StatusMessage?.Invoke(this, "노션에 올리는 중…");
        await _notion.ExportAsync(item);     // NotionPageId + Status=Exported
        await _storage.UpdateAsync(item);
        AppLog.Line($"[노션 전송 완료] title=\"{item.AiTitle}\"  url={_notion.LastExportedUrl}");

        ItemUpdated?.Invoke(this, item);
        StatusMessage?.Invoke(this, "노션에 올렸어요!");
    }

    /// <summary>항목 삭제(이미지 항목이면 연결 PNG도 정리).</summary>
    public async Task DeleteAsync(CaptureItem item)
    {
        await _storage.DeleteAsync(item.Id);

        if (!string.IsNullOrEmpty(item.ImagePath))
        {
            try { if (File.Exists(item.ImagePath)) File.Delete(item.ImagePath); }
            catch { /* 파일 정리 실패는 무시 */ }
        }

        ItemDeleted?.Invoke(this, item.Id);
    }

    private static void LogCapture(int count, CaptureItem item)
    {
        var preview = (item.RawText ?? "").Replace("\r", " ").Replace("\n", " ");
        if (preview.Length > 40) preview = preview[..40] + "…";
        AppLog.Line($"count={count}  [{item.SourceType}]  \"{preview}\"");
    }

    private static void LogAiResult(CaptureItem item)
    {
        var tags = item.AiTags is { Length: > 0 } ? string.Join(", ", item.AiTags) : "(없음)";
        AppLog.Line($"[AI {item.Status}]  title=\"{item.AiTitle}\"  tags=[{tags}]  md={item.AiMarkdown?.Length ?? 0}자");
    }
}

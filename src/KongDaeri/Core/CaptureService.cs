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
    public event EventHandler? Cleared;                 // 전체 삭제 시
    public event EventHandler<string>? StatusMessage;   // 데스펫 말풍선용
    public event EventHandler<string>? Error;           // 처리 실패 alert(분류된 안전 문구)

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
        StatusMessage?.Invoke(this, "콩대리가 주워 담았어요!");
    }

    /// <summary>AI 처리(정리/번역): Processing → (성공)Processed / (실패)Failed.</summary>
    public async Task ProcessWithAiAsync(CaptureItem item, AiTask task = AiTask.Organize, string? language = null)
    {
        if (_ai is null) return;
        bool translate = task == AiTask.Translate;

        try
        {
            item.Status = CaptureStatus.Processing;
            await _storage.UpdateAsync(item);
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, translate ? "콩대리가 번역 중…" : "콩대리가 정리 중…");

            await _ai.ProcessAsync(item, task, language);
            await _storage.UpdateAsync(item);
            LogAiResult(item);
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, translate ? "콩대리가 번역했어요!" : "콩대리가 정리했어요!");
        }
        catch (Exception ex)
        {
            item.Status = CaptureStatus.Failed;
            try { await _storage.UpdateAsync(item); } catch { /* 업데이트 실패 무시 */ }
            AppLog.Line($"[AI 실패] {ex.GetType().Name}");   // 타입만(메시지 미기록)
            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, "앗, 실패했어요");
            Error?.Invoke(this, FailureMessage.ToUserMessage(ex));
        }
    }

    /// <summary>항목을 노션으로 전송. 정리 전이면 먼저 AI 정리부터 시도. 실패는 내부에서 alert 처리(예외 전파 안 함).</summary>
    public async Task ExportAsync(CaptureItem item)
    {
        if (_notion is null)
        {
            Error?.Invoke(this, "노션이 비활성화됐어요. 설정에서 토큰과 페이지 ID를 확인해 주세요.");
            return;
        }

        // 아직 정리 전이면 먼저 AI 정리(실패 시 ProcessWithAiAsync 가 alert/sad 처리).
        if (item.Status != CaptureStatus.Processed && item.Status != CaptureStatus.Exported && _ai is not null)
        {
            await ProcessWithAiAsync(item);
        }
        if (string.IsNullOrWhiteSpace(item.AiMarkdown))
        {
            // AI 정리 실패면 직전에 이미 alert이 떴으므로 조용히 종료.
            if (item.Status != CaptureStatus.Failed)
            {
                Error?.Invoke(this, "먼저 [AI 정리] 또는 [번역]으로 내용을 정리해 주세요.");
            }
            return;
        }

        try
        {
            StatusMessage?.Invoke(this, "노션에 올리는 중…");
            await _notion.ExportAsync(item);     // NotionPageId + Status=Exported
            await _storage.UpdateAsync(item);
            AppLog.Line($"[노션 전송 완료] title=\"{item.AiTitle}\"  url={_notion.LastExportedUrl}");

            ItemUpdated?.Invoke(this, item);
            StatusMessage?.Invoke(this, "콩대리가 노션에 올렸어요!");
        }
        catch (Exception ex)
        {
            AppLog.Line($"[노션 전송 실패] {ex.GetType().Name}");   // 타입만(메시지 미기록)
            StatusMessage?.Invoke(this, "앗, 실패했어요");
            Error?.Invoke(this, FailureMessage.ToUserMessage(ex));
            // 상태는 유지(재시도 가능). DB status 변경 없음.
        }
    }

    /// <summary>모든 수집 항목 + 스니핑 이미지 삭제(설정은 건드리지 않음).</summary>
    public async Task ClearAllAsync()
    {
        var items = await _storage.ListAsync();
        foreach (var i in items)
        {
            await _storage.DeleteAsync(i.Id);
        }

        // snips 폴더의 PNG(썸네일 포함) 정리.
        try
        {
            var snipDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KongDaeri", "snips");
            if (Directory.Exists(snipDir))
            {
                foreach (var f in Directory.GetFiles(snipDir, "*.png"))
                {
                    try { File.Delete(f); } catch { /* 개별 실패 무시 */ }
                }
            }
        }
        catch { /* 폴더 정리 실패 무시 */ }

        Cleared?.Invoke(this, EventArgs.Empty);
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

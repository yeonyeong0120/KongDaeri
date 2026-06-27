using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KongDaeri.Core;

/// <summary>
/// Notion REST API로 정리된 마크다운을 하위 페이지로 생성한다. (기획서 §7)
/// POST /v1/pages 에 markdown 파라미터, Notion-Version: 2026-03-11.
/// 토큰/페이지ID는 AppSettings 에서만 주입하며 로그에 평문 노출하지 않는다.
/// </summary>
public sealed class NotionExporter : IExporter
{
    private const string Endpoint = "https://api.notion.com/v1/pages";
    private const string NotionVersion = "2026-03-11";

    private readonly string _token;
    private readonly string _parentPageId;
    private readonly HttpClient _http;

    /// <summary>마지막 전송에 성공한 페이지 URL(검증/로그용). 토큰은 포함되지 않음.</summary>
    public string? LastExportedUrl { get; private set; }

    public NotionExporter(AppSettings settings, HttpClient? http = null)
    {
        settings.EnsureNotionReady();          // 값 없으면 안내 예외(값은 노출 안 함)
        _token = settings.NotionToken!;
        _parentPageId = settings.NotionParentPageId!;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> ExportAsync(CaptureItem item)
    {
        if (string.IsNullOrWhiteSpace(item.AiMarkdown))
        {
            throw new InvalidOperationException(
                "AiMarkdown 이 비어 있어 노션에 보낼 내용이 없습니다. (AI 정리가 먼저 필요)");
        }

        // markdown 은 children/content 와 동시 사용 불가 — markdown 만 보낸다.
        var body = new
        {
            parent = new { page_id = _parentPageId },
            markdown = item.AiMarkdown
        };

        // 일시 장애(429/5xx)는 재시도. 401/404 등 영구 오류는 RetryPolicy 가 즉시 반환.
        using var response = await RetryPolicy.SendWithRetryAsync(_http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_token}");
            request.Headers.TryAddWithoutValidation("Notion-Version", NotionVersion);
            return request;
        }, log: AppLog.Line);

        if (!response.IsSuccessStatusCode)
        {
            // 404 는 데모 단골 사고: 대상 페이지가 통합에 Connect 안 된 경우.
            var hint = response.StatusCode == HttpStatusCode.NotFound
                ? " — 대상 페이지가 통합(Integration)에 연결(Connect)되지 않았을 수 있습니다. " +
                  "노션에서 부모 페이지의 '연결' 메뉴로 인티그레이션을 추가하세요."
                : "";
            throw new HttpRequestException(
                $"Notion 호출 실패: HTTP {(int)response.StatusCode} {response.StatusCode}{hint}");
        }

        var payload = await response.Content.ReadAsStringAsync();
        var (id, url) = ParseResponse(payload);

        item.NotionPageId = id;
        item.Status = CaptureStatus.Exported;
        LastExportedUrl = url;
        return id;
    }

    private static (string id, string? url) ParseResponse(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrEmpty(id))
        {
            throw new FormatException("Notion 응답에 page id 가 없습니다.");
        }

        var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
        return (id, url);
    }
}

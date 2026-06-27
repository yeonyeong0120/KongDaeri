using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace KongDaeri.Core;

/// <summary>
/// Google Generative Language REST API(gemini-2.5-flash-lite)를 HttpClient로 직접 호출해
/// 수집 텍스트를 분류·요약·마크다운으로 정리한다. (기획서 §6)
/// 별도 SDK 없이 REST. API 키는 AppSettings 에서만 주입한다.
/// </summary>
public sealed class GeminiProcessor : IAiProcessor
{
    private const string Model = "gemini-2.5-flash-lite";
    private const string Endpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent";

    private readonly string _apiKey;
    private readonly HttpClient _http;

    public GeminiProcessor(AppSettings settings, HttpClient? http = null)
    {
        settings.EnsureGeminiReady();          // 키 없으면 안내 예외(값은 노출 안 함)
        _apiKey = settings.GeminiApiKey!;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<CaptureItem> ProcessAsync(CaptureItem item, AiTask task = AiTask.Organize, string? targetLanguage = null)
    {
        var lang = string.IsNullOrWhiteSpace(targetLanguage) ? "English" : targetLanguage!;

        // 입력 분기: 이미지(스니핑) 항목이면 비전, 아니면 텍스트. (정리/번역 공통)
        object[] parts = !string.IsNullOrEmpty(item.ImagePath)
            ? BuildImageParts(item.ImagePath!, task, lang)
            : BuildTextParts(item.RawText ?? string.Empty, task, lang);

        var requestBody = new
        {
            contents = new[]
            {
                new { parts }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        // 일시 장애(503 등)는 지수 백오프로 재시도. 매 시도마다 새 요청 생성.
        using var response = await RetryPolicy.SendWithRetryAsync(_http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(requestBody)
            };
            // 키는 헤더로 전달(URL 로깅 노출 방지).
            request.Headers.Add("x-goog-api-key", _apiKey);
            return request;
        }, log: AppLog.Line);

        if (!response.IsSuccessStatusCode)
        {
            // 본문에 키가 들어갈 일은 없지만, 상태코드만 노출.
            throw new HttpRequestException(
                $"Gemini 호출 실패: HTTP {(int)response.StatusCode} {response.StatusCode}",
                null, response.StatusCode);
        }

        var payload = await response.Content.ReadAsStringAsync();
        var (title, tags, markdown) = ParseResponse(payload);

        item.AiTitle = title;
        item.AiTags = tags;
        item.AiMarkdown = markdown;
        item.Status = CaptureStatus.Processed;
        return item;
    }

    // 텍스트 항목용 파트.
    private static object[] BuildTextParts(string input, AiTask task, string lang)
    {
        var prompt = task == AiTask.Translate
            ? $$"""
                너는 수집한 정보를 번역해 노션에 아카이빙하는 비서야.
                아래 [수집 내용]을 {{lang}} 로 자연스럽게 번역한 뒤, 다음을 JSON 으로만 응답해. 다른 말은 붙이지 마.

                - title: 번역 결과를 한눈에 알 수 있는 짧은 제목({{lang}})
                - tags: 주제 태그 3~5개({{lang}}, 배열)
                - markdown: 노션 본문 마크다운. 맨 위 첫 줄에 "# 제목"(h1, {{lang}}) 포함. 본문은
                  {{lang}} 로 번역된 내용. 표/목록이 있으면 형식을 유지해 번역.

                응답 JSON 스키마:
                {"title": "string", "tags": ["string"], "markdown": "string"}

                [수집 내용]
                {{input}}
                """
            : $$"""
                너는 사용자가 수집한 정보를 노션에 아카이빙하기 좋게 정리하는 비서야.
                아래 [수집 내용]을 읽고 다음을 JSON 으로만 응답해. 다른 말은 절대 붙이지 마.

                - title: 내용을 한눈에 알 수 있는 짧은 한국어 제목 (한 줄)
                - tags: 주제를 나타내는 태그 3~5개 (한국어, 배열)
                - markdown: 노션 페이지 본문으로 쓸 마크다운. 반드시 맨 위 첫 줄에 "# 제목"(h1)을
                  포함하고, 그 아래에 요점을 정리. 원문이 길면 요약하고, 표/목록이 어울리면 사용해.

                응답 JSON 스키마:
                {"title": "string", "tags": ["string"], "markdown": "string"}

                [수집 내용]
                {{input}}
                """;

        return new object[] { new { text = prompt } };
    }

    // 스니핑 이미지용 파트: 프롬프트 + base64 inline_data(image/png).
    private static object[] BuildImageParts(string imagePath, AiTask task, string lang)
    {
        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"스니핑 이미지 파일을 찾을 수 없습니다: {imagePath}");
        }

        var bytes = File.ReadAllBytes(imagePath);
        var base64 = Convert.ToBase64String(bytes);

        var prompt = task == AiTask.Translate
            ? $$"""
                너는 캡처 이미지를 번역해 노션에 아카이빙하는 비서야.
                첨부 이미지에서 텍스트·표·핵심 정보를 추출한 뒤 {{lang}} 로 번역해 다음을 JSON 으로만 응답해. 다른 말은 붙이지 마.

                - title: 번역 결과의 짧은 제목({{lang}})
                - tags: 주제 태그 3~5개({{lang}}, 배열)
                - markdown: 노션 본문 마크다운. 맨 위 첫 줄에 "# 제목"(h1, {{lang}}) 포함. 표가 있으면
                  마크다운 표로 재현하되 내용은 {{lang}} 로 번역.

                응답 JSON 스키마:
                {"title": "string", "tags": ["string"], "markdown": "string"}
                """
            : """
                너는 사용자가 캡처한 화면 이미지를 노션에 아카이빙하기 좋게 정리하는 비서야.
                첨부된 이미지에서 텍스트·표·핵심 정보를 추출해 다음을 JSON 으로만 응답해. 다른 말은 절대 붙이지 마.

                - title: 이미지 내용을 한눈에 알 수 있는 짧은 한국어 제목 (한 줄)
                - tags: 주제를 나타내는 태그 3~5개 (한국어, 배열)
                - markdown: 노션 페이지 본문으로 쓸 마크다운. 반드시 맨 위 첫 줄에 "# 제목"(h1)을
                  포함하고, 이미지의 텍스트는 그대로 옮기되 표가 있으면 마크다운 표로 재현하고 핵심은 요약해.

                응답 JSON 스키마:
                {"title": "string", "tags": ["string"], "markdown": "string"}
                """;

        return new object[]
        {
            new { text = prompt },
            new { inline_data = new { mime_type = "image/png", data = base64 } },
        };
    }

    /// <summary>
    /// Gemini 응답(JSON)에서 candidates[0].content.parts[*].text 를 모아 안쪽 JSON을 파싱.
    /// </summary>
    private static (string title, string[] tags, string markdown) ParseResponse(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
        {
            throw new FormatException("Gemini 응답에 candidates 가 없습니다.");
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var t))
            {
                sb.Append(t.GetString());
            }
        }

        var innerJson = sb.ToString().Trim();
        if (string.IsNullOrEmpty(innerJson))
        {
            throw new FormatException("Gemini 응답 본문(text)이 비어 있습니다.");
        }

        using var inner = JsonDocument.Parse(innerJson);
        var ir = inner.RootElement;

        var title = ir.TryGetProperty("title", out var titleEl)
            ? titleEl.GetString() ?? "(제목 없음)"
            : "(제목 없음)";

        string[] tags = Array.Empty<string>();
        if (ir.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            tags = tagsEl.EnumerateArray()
                         .Select(x => x.GetString() ?? "")
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .ToArray();
        }

        var markdown = ir.TryGetProperty("markdown", out var mdEl)
            ? mdEl.GetString() ?? ""
            : "";

        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new FormatException("Gemini 응답에 markdown 이 없습니다.");
        }

        return (title, tags, markdown);
    }
}

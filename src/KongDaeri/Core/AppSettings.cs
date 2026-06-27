using System.IO;
using System.Text.Json;

namespace KongDaeri.Core;

/// <summary>
/// %LOCALAPPDATA%\KongDaeri\settings.json 에 보관되는 시크릿/설정.
/// 키 값은 절대 로그에 평문 출력하지 않는다.
/// </summary>
public sealed class AppSettings
{
    public string? GeminiApiKey { get; set; }
    public string? NotionToken { get; set; }
    public string? NotionParentPageId { get; set; }

    /// <summary>화면 스니퍼 전역 단축키(예: "Ctrl+Alt+S"). 비거나 파싱 실패 시 기본값 사용.</summary>
    public string? SnipHotkey { get; set; }

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KongDaeri", "settings.json");

    /// <summary>
    /// settings.json 을 읽는다. 파일이 없으면 빈 설정을 반환(예외 아님).
    /// </summary>
    public static AppSettings Load()
    {
        var path = SettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new AppSettings();
    }

    /// <summary>
    /// Gemini 사용에 필요한 키가 있는지 검사. 없으면 어떤 키가 비었는지 안내하는
    /// 예외를 던진다(값 자체는 노출하지 않음).
    /// </summary>
    public void EnsureGeminiReady()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            throw new InvalidOperationException(
                $"GeminiApiKey 가 설정되지 않았습니다. '{SettingsPath}' 에 " +
                "\"GeminiApiKey\": \"<키>\" 를 추가하세요. (docs/settings.example.json 참고)");
        }
    }

    /// <summary>
    /// 노션 전송에 필요한 토큰/부모 페이지ID가 있는지 검사. 없으면 어떤 값이 비었는지
    /// 안내하는 예외를 던진다(값 자체는 노출하지 않음).
    /// </summary>
    public void EnsureNotionReady()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(NotionToken)) missing.Add("NotionToken");
        if (string.IsNullOrWhiteSpace(NotionParentPageId)) missing.Add("NotionParentPageId");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", missing)} 가 설정되지 않았습니다. '{SettingsPath}' 에 " +
                "추가하세요. (docs/settings.example.json 참고)");
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using KongDaeri.Core;
using KongDaeri.Interop;

namespace KongDaeri.ViewModels;

/// <summary>
/// 설정 창 뷰모델. 시크릿(키/토큰)은 보안상 PasswordBox 에서 코드비하인드로 전달받아 처리하고,
/// 비시크릿(페이지ID/단축키)과 "설정됨" 표시·저장 검증 로직을 담당한다.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string notionPageIdInput = "";
    [ObservableProperty] private string snipHotkeyInput = "";
    [ObservableProperty] private string translateLanguageInput = "English";

    /// <summary>번역 언어 선택지.</summary>
    public string[] Languages { get; } = { "English", "Japanese", "Chinese", "Korean" };

    // 시크릿은 평문을 뿌리지 않고 "설정됨/없음"만 표시.
    [ObservableProperty] private bool geminiKeySet;
    [ObservableProperty] private bool notionTokenSet;

    // 보기/숨기기 토글
    [ObservableProperty] private bool showGeminiKey;
    [ObservableProperty] private bool showNotionToken;

    public string GeminiKeyStatus => GeminiKeySet ? "현재: 설정됨 (변경하려면 입력)" : "현재: 없음";
    public string NotionTokenStatus => NotionTokenSet ? "현재: 설정됨 (변경하려면 입력)" : "현재: 없음";

    partial void OnGeminiKeySetChanged(bool value) => OnPropertyChanged(nameof(GeminiKeyStatus));
    partial void OnNotionTokenSetChanged(bool value) => OnPropertyChanged(nameof(NotionTokenStatus));

    public SettingsViewModel()
    {
        var s = AppSettings.Load();
        GeminiKeySet = !string.IsNullOrWhiteSpace(s.GeminiApiKey);
        NotionTokenSet = !string.IsNullOrWhiteSpace(s.NotionToken);
        NotionPageIdInput = s.NotionParentPageId ?? "";
        SnipHotkeyInput = string.IsNullOrWhiteSpace(s.SnipHotkey) ? "Ctrl+Alt+S" : s.SnipHotkey!;
        TranslateLanguageInput = string.IsNullOrWhiteSpace(s.TranslateLanguage) ? "English" : s.TranslateLanguage!;
    }

    /// <summary>
    /// 저장. 시크릿 입력은 코드비하인드(PasswordBox)에서 전달. 빈 값은 기존값 유지.
    /// 단축키는 저장 전 유효성 검사. 반환: (성공, 오류메시지).
    /// </summary>
    public (bool ok, string? error) Apply(string geminiInput, string notionTokenInput)
    {
        var s = AppSettings.Load();   // 기존값 기준으로 빈칸 유지

        if (!string.IsNullOrWhiteSpace(SnipHotkeyInput))
        {
            if (!GlobalHotkey.TryParse(SnipHotkeyInput, out _))
            {
                return (false, "단축키 형식이 올바르지 않습니다. 예) Ctrl+Alt+S, Ctrl+Shift+F9");
            }
            s.SnipHotkey = SnipHotkeyInput.Trim();
        }

        if (!string.IsNullOrWhiteSpace(geminiInput)) s.GeminiApiKey = geminiInput.Trim();
        if (!string.IsNullOrWhiteSpace(notionTokenInput)) s.NotionToken = notionTokenInput.Trim();
        if (!string.IsNullOrWhiteSpace(NotionPageIdInput)) s.NotionParentPageId = NotionPageIdInput.Trim();
        if (!string.IsNullOrWhiteSpace(TranslateLanguageInput)) s.TranslateLanguage = TranslateLanguageInput;

        s.Save();
        return (true, null);
    }
}

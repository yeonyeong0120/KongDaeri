using System.Text.RegularExpressions;

namespace KongDaeri.Core;

/// <summary>필터 결과: 차단 여부 + (차단 시) 유형 + 마스킹된 텍스트.</summary>
public readonly record struct FilterResult(bool Blocked, string? BlockType, string MaskedText);

/// <summary>
/// 민감정보 필터(순수 함수). 정책: 고위험=수집 차단 / 중위험=마스킹 저장.
/// 텍스트 수집 경로 전용. 원본 평문은 반환값(마스킹본)에만 존재하며 어디에도 따로 저장/로그하지 않는다.
/// </summary>
public static partial class SensitiveDataFilter
{
    // ── 고위험(차단) ──
    // 카드 후보: 13~16자리(자리 사이 공백/하이픈 1개 허용). 추출 후 Luhn 으로 확정.
    [GeneratedRegex(@"(?<![\d-])\d(?:[ -]?\d){12,15}(?![\d-])")]
    private static partial Regex CardCandidateRegex();

    // 주민등록번호: 6자리-7자리
    [GeneratedRegex(@"(?<!\d)\d{6}-\d{7}(?!\d)")]
    private static partial Regex RrnRegex();

    // API 키/토큰: 알려진 접두사 + 길이 임계값
    [GeneratedRegex(@"(?:ntn_|sk-|AIza)[A-Za-z0-9_\-]{16,}")]
    private static partial Regex ApiKeyRegex();

    // ── 중위험(마스킹) ──
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    // 휴대폰(구분자 선택) + 유선(구분자 필수)
    [GeneratedRegex(@"(01[016789])([-. ]?)(\d{3,4})([-. ]?)(\d{4})")]
    private static partial Regex MobileRegex();

    [GeneratedRegex(@"(0[2-6]\d?)([-.])(\d{3,4})([-.])(\d{4})")]
    private static partial Regex LandlineRegex();

    // 비밀번호류 키워드 + 구분자(:/=) + 값. (구분자 필수 — '비밀번호를' 같은 일반어 오탐 방지)
    [GeneratedRegex(@"(?i)(비밀번호|비번|password|passwd|pw|otp|인증번호)(\s*[:=]\s*)(\S+)")]
    private static partial Regex KeywordValueRegex();

    public static FilterResult Inspect(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new FilterResult(false, null, text);
        }

        // 1) 고위험 → 차단 (값은 결과에 담지 않음)
        if (RrnRegex().IsMatch(text)) return new FilterResult(true, "주민등록번호", "");
        if (ApiKeyRegex().IsMatch(text)) return new FilterResult(true, "API키/토큰", "");
        if (HasCreditCard(text)) return new FilterResult(true, "카드번호", "");

        // 2) 중위험 → 마스킹 (키워드 먼저: 값에 포함된 이메일/전화도 함께 가려짐)
        var masked = KeywordValueRegex().Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + "****");
        masked = EmailRegex().Replace(masked, MaskEmail);
        masked = MobileRegex().Replace(masked, m => m.Groups[1].Value + m.Groups[2].Value + "****" + m.Groups[4].Value + m.Groups[5].Value);
        masked = LandlineRegex().Replace(masked, m => m.Groups[1].Value + m.Groups[2].Value + "****" + m.Groups[4].Value + m.Groups[5].Value);

        return new FilterResult(false, null, masked);
    }

    private static bool HasCreditCard(string text)
    {
        foreach (Match m in CardCandidateRegex().Matches(text))
        {
            var digits = new string(m.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 13 and <= 16 && LuhnValid(digits))
            {
                return true;
            }
        }
        return false;
    }

    private static bool LuhnValid(string digits)
    {
        int sum = 0;
        bool alt = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int d = digits[i] - '0';
            if (alt) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            alt = !alt;
        }
        return sum % 10 == 0;
    }

    private static string MaskEmail(Match m)
    {
        var s = m.Value;
        int at = s.IndexOf('@');
        var local = s[..at];
        var domain = s[at..];
        var keep = local.Length <= 2 ? local[..1] : local[..2];
        return keep + "****" + domain;
    }
}

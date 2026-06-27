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

    // API 키/토큰 (a-1): 영숫자 접미사 접두사. 토큰 시작에서만(앞이 영숫자면 제외).
    // 접미사를 순수 영숫자로 제한해 'secret_handshake_protocol' 같은 단어열 오탐 방지.
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:AIza|ntn_|secret_|gsk_|ghp_|github_pat_)[A-Za-z0-9]{16,}")]
    private static partial Regex ApiKeyAlnumRegex();

    // API 키/토큰 (a-2): 접미사에 . _ - 허용하는 접두사(AQ. / sk- / sk-ant- / Slack xox*-).
    // 토큰 시작에서만 + 충분한 길이로 'task-management...' 류 오탐 방지.
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:AQ\.|sk-ant-|sk-|xox[a-z]-)[A-Za-z0-9][A-Za-z0-9_.\-]{19,}")]
    private static partial Regex ApiKeyMixedRegex();

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
        if (ApiKeyAlnumRegex().IsMatch(text) || ApiKeyMixedRegex().IsMatch(text) || LooksLikeKey(text))
        {
            return new FilterResult(true, "API키/토큰", "");
        }
        if (HasCreditCard(text)) return new FilterResult(true, "카드번호", "");

        // 2) 중위험 → 마스킹 (키워드 먼저: 값에 포함된 이메일/전화도 함께 가려짐)
        var masked = KeywordValueRegex().Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + "****");
        masked = EmailRegex().Replace(masked, MaskEmail);
        masked = MobileRegex().Replace(masked, m => m.Groups[1].Value + m.Groups[2].Value + "****" + m.Groups[4].Value + m.Groups[5].Value);
        masked = LandlineRegex().Replace(masked, m => m.Groups[1].Value + m.Groups[2].Value + "****" + m.Groups[4].Value + m.Groups[5].Value);

        return new FilterResult(false, null, masked);
    }

    // 접두사 없는 키 휴리스틱(보수적). 오탐 방지를 위해 조건을 모두 만족할 때만 차단:
    //  - 공백 없는 토큰, 길이 30+ , 전부 키 알파벳([A-Za-z0-9_.-])
    //  - 대문자·소문자·숫자 모두 포함, 순수 16진수(해시) 아님
    //  - 샤논 엔트로피 높음(랜덤성)
    // URL/경로(슬래시·콜론 등 포함)·한글·일반 단어/숫자는 위 조건에서 자연히 제외됨.
    private static bool LooksLikeKey(string text)
    {
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length < 30) continue;
            if (!token.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')) continue;
            if (!token.Any(char.IsAsciiLetterUpper)) continue;
            if (!token.Any(char.IsAsciiLetterLower)) continue;
            if (!token.Any(char.IsAsciiDigit)) continue;
            if (token.All(Uri.IsHexDigit)) continue;          // 16진수 해시 제외
            if (ShannonEntropy(token) < 3.6) continue;        // 랜덤성 임계
            return true;
        }
        return false;
    }

    private static double ShannonEntropy(string s)
    {
        var counts = new Dictionary<char, int>();
        foreach (var c in s) counts[c] = counts.GetValueOrDefault(c) + 1;

        double entropy = 0;
        foreach (var n in counts.Values)
        {
            double p = (double)n / s.Length;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
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

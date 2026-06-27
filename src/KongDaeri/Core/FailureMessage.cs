using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace KongDaeri.Core;

/// <summary>
/// AI/노션 처리 실패 예외를 사용자 친화 한국어 메시지로 매핑.
/// 원본 예외 메시지(키/토큰이 섞일 수 있음)는 그대로 노출하지 않고, 안전 문구만 반환한다.
/// </summary>
public static class FailureMessage
{
    public static string ToUserMessage(Exception ex)
    {
        switch (ex)
        {
            case HttpRequestException { StatusCode: { } code }:
                return code switch
                {
                    // Gemini 는 무효 키에 400(INVALID_ARGUMENT)도 반환 → 키 안내로 처리
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest
                        => "API 키가 올바르지 않은 것 같아요. 설정에서 키를 확인해 주세요.",
                    HttpStatusCode.TooManyRequests
                        => "API 호출 한도를 초과했어요. 잠시 후 다시 시도해 주세요.",
                    HttpStatusCode.NotFound
                        => "노션 페이지를 찾을 수 없어요. 페이지가 통합에 연결(Connect)됐는지 확인해 주세요.",
                    _ => $"처리 중 문제가 생겼어요. (HTTP {(int)code})",
                };

            // StatusCode 없는 HttpRequestException = 네트워크/연결 실패(재시도 소진 포함)
            case HttpRequestException:
            case TaskCanceledException:           // 타임아웃
            case OperationCanceledException:
            case SocketException:
                return "인터넷 연결을 확인해 주세요.";

            // 우리가 직접 던진 안전한 안내(키 없음/마크다운 없음 등) — 메시지 그대로 사용
            case InvalidOperationException:
                return ex.Message;

            case FormatException:
                return "AI 응답을 이해하지 못했어요. 잠시 후 다시 시도해 주세요.";

            default:
                return "처리 중 문제가 생겼어요.";
        }
    }
}

using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace KongDaeri.Core;

/// <summary>
/// 일시적 네트워크 오류용 지수 백오프 재시도 헬퍼.
/// 재시도 대상: HTTP 429/500/502/503/504, 네트워크 타임아웃/연결 예외.
/// 영구 오류(401/404 등)는 재시도하지 않고 즉시 반환한다.
/// </summary>
public static class RetryPolicy
{
    private static readonly HttpStatusCode[] TransientStatuses =
    {
        HttpStatusCode.TooManyRequests,      // 429
        HttpStatusCode.InternalServerError,  // 500
        HttpStatusCode.BadGateway,           // 502
        HttpStatusCode.ServiceUnavailable,   // 503
        HttpStatusCode.GatewayTimeout,       // 504
    };

    public static bool IsTransientStatus(HttpStatusCode code) =>
        Array.IndexOf(TransientStatuses, code) >= 0;

    /// <summary>
    /// requestFactory 로 매 시도마다 새 요청을 만들어 전송한다(HttpRequestMessage 는 재사용 불가).
    /// 성공/영구오류면 그 응답을 반환하고, 일시오류는 maxAttempts 까지 재시도한다.
    /// </summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http,
        Func<HttpRequestMessage> requestFactory,
        Action<string>? log = null,
        int maxAttempts = 3)
    {
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            string reason;

            try
            {
                using var request = requestFactory();
                response = await http.SendAsync(request);

                // 성공 또는 영구 오류(401/404 등) → 즉시 반환(재시도 안 함).
                if (response.IsSuccessStatusCode || !IsTransientStatus(response.StatusCode))
                {
                    return response;
                }

                reason = $"{(int)response.StatusCode} {response.StatusCode}";
            }
            catch (Exception ex) when (IsTransientException(ex))
            {
                // 네트워크/타임아웃. 예외 타입명만 사용(메시지에 키/토큰 노출 방지).
                reason = ex.GetType().Name;
            }

            // 여기 도달 = 일시적 실패.
            if (attempt >= maxAttempts)
            {
                if (response != null)
                {
                    return response; // 마지막 응답을 호출부가 처리(IsSuccessStatusCode 검사)
                }
                throw new HttpRequestException($"재시도 {maxAttempts}회 모두 실패: {reason}");
            }

            response?.Dispose();
            var delay = BackoffDelay(attempt);
            log?.Invoke($"재시도 {attempt}/{maxAttempts} ({reason}, {delay.TotalSeconds:0.#}s 후)");
            await Task.Delay(delay);
        }
    }

    // 0.5s, 1s, 2s ... + 지터(0~200ms)
    private static TimeSpan BackoffDelay(int attempt)
    {
        var baseMs = 500 * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, 200);
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }

    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException
        || ex is TaskCanceledException      // HttpClient 타임아웃
        || ex is OperationCanceledException
        || ex is SocketException;
}

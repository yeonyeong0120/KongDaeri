using System.IO;

namespace KongDaeri.Core;

/// <summary>
/// 검증/진단용 단순 로그. %LOCALAPPDATA%\KongDaeri\capture.log 에 한 줄씩 남긴다.
/// 키/토큰 등 시크릿은 절대 이 함수에 넘기지 않는다(호출부 책임).
/// </summary>
public static class AppLog
{
    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KongDaeri", "capture.log");

    public static void Line(string text)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}");
        }
        catch
        {
            // 로그 실패는 무시.
        }
    }
}
